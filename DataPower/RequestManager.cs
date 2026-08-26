// Copyright 2023 Keyfactor
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Keyfactor.Extensions.Orchestrator.DataPower.Client;
using Keyfactor.Extensions.Orchestrator.DataPower.Models.Requests;
using Keyfactor.Extensions.Orchestrator.DataPower.Models.Responses;
using Keyfactor.Extensions.Orchestrator.DataPower.Models.SupportingObjects;
using Keyfactor.Logging;
using Keyfactor.Orchestrators.Common.Enums;
using Keyfactor.Orchestrators.Extensions;
using Keyfactor.Orchestrators.Extensions.Interfaces;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Pkcs;
using CertificateRequest = Keyfactor.Extensions.Orchestrator.DataPower.Models.Requests.CertificateRequest;

namespace Keyfactor.Extensions.Orchestrator.DataPower
{
    public class RequestManager
    {
        private const string SharedCertStoreName = "sharedcert";
        private const string DefaultDomainName = "default";

        private readonly ILogger _logger;
        private string _protocol;
        private IPAMSecretResolver _resolver;
        private string ServerUserName { get; set; }
        private string ServerPassword { get; set; }

        // Unit-test seam: production code never sets this, so every RequestManager
        // constructs the real DataPowerClient. Tests substitute a factory that returns
        // a mock IDataPowerClient instead of making real HTTPS calls.
        public Func<string, string, string, string, IDataPowerClient> ClientFactory { get; set; } =
            (user, pass, baseUrl, domain) => new DataPowerClient(user, pass, baseUrl, domain);

        // sharedcert files physically live in the default domain's filestore - DataPower
        // rejects filestore writes to sharedcert:// from any other domain context. The
        // CryptoCertificate/CryptoKey config *objects* that reference those files, though,
        // can live in any domain (that's the whole reason sharedcert stores are now
        // per-domain). FileDomainFor() returns which domain a filestore write/delete must
        // target, as opposed to ci.Domain, which is where the config object lives.
        private static string FileDomainFor(CertStoreInfo ci)
        {
            return IsSharedCertStore(ci) ? DefaultDomainName : ci.Domain;
        }

        private static bool IsSharedCertStore(CertStoreInfo ci)
        {
            return string.Equals(ci?.CertificateStore?.Trim(), SharedCertStoreName, StringComparison.OrdinalIgnoreCase);
        }


        public RequestManager(IPAMSecretResolver resolver)
        {
            // Must use LogHandler so we plug into the orchestrator's NLog pipeline.
            // A bare-new LoggerFactory has no providers, so LogTrace/LogError calls
            // get silently dropped.
            _logger = LogHandler.GetClassLogger<RequestManager>();
            _resolver = resolver;
        }

        private string ResolvePamField(string name, string value)
        {
            _logger.LogTrace($"Attempting to resolved PAM eligible field {name}");
            return _resolver.Resolve(value);
        }

        // Parse the InventoryBlackList store property into a case-insensitive set.
        // Tolerates null/empty (Command may send the property as JSON null when the user
        // leaves the field blank, which DefaultValueHandling.Populate doesn't override).
        private static HashSet<string> ParseInventoryBlacklist(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            return new HashSet<string>(
                raw.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0),
                StringComparer.OrdinalIgnoreCase);
        }

        public bool DoesCryptoCertificateObjectExist(CertStoreInfo ci, string cryptoCertObjectName,
            IDataPowerClient apiClient)
        {
            var bUpdateCryptoCertificateObject = false;
            try
            {
                _logger.MethodEntry(LogLevel.Debug);
                _protocol = ci.Protocol;
                //Get a count of the crypto certificates that have the name we are looking for should be equal to one if it exists
                var viewAllCryptoCertRequest = new ViewCryptoCertificatesRequest(ci.Domain);
                _logger.LogTrace(
                    $"viewAllCryptoCertRequest JSON {JsonConvert.SerializeObject(viewAllCryptoCertRequest)}");
                var viewAllCryptoCertResponse = apiClient.ViewCertificates(viewAllCryptoCertRequest);
                _logger.LogTrace(
                    $"viewAllCryptoCertResponse JSON {JsonConvert.SerializeObject(viewAllCryptoCertResponse)}");

                if (viewAllCryptoCertResponse.CryptoCertificates.Count(x => x.Name == cryptoCertObjectName) == 1)
                {
                    _logger.LogTrace("Only One Found, we are good!");
                    bUpdateCryptoCertificateObject = true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    $"There was an issue receiving the certificates: {cryptoCertObjectName} Error {LogHandler.FlattenException(ex)}");
            }

            _logger.MethodExit(LogLevel.Debug);

            return bUpdateCryptoCertificateObject;
        }

        public void DisableCryptoCertificateObject(string cryptoCertObjectName, IDataPowerClient apiClient)
        {
            _logger.MethodEntry(LogLevel.Debug);
            _logger.LogTrace($"Disable State for Crypto Certificate Object: {cryptoCertObjectName}");
            try
            {
                var cryptoCertUpdateRequest =
                    new CryptoCertificateUpdateRequest(apiClient.Domain, cryptoCertObjectName)
                    {
                        CryptoCert = new CryptoCertificate
                        {
                            MAdminState = "disabled",
                            Name = cryptoCertObjectName,
                            CertFile = null,
                            IgnoreExpiration = null,
                            PasswordAlias = null
                        }
                    };
                _logger.LogTrace(
                    $"cryptoCertUpdateRequest JSON {JsonConvert.SerializeObject(cryptoCertUpdateRequest)}");
                apiClient.UpdateCryptoCertificate(cryptoCertUpdateRequest);
                _logger.LogTrace("Crypto Certificate Updated");
                _logger.MethodExit(LogLevel.Debug);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    $"There was an issue disabling the certificate object: {cryptoCertObjectName} Error {LogHandler.FlattenException(ex)}");
            }
        }

        public bool DoesCryptoKeyObjectExist(CertStoreInfo ci, string cryptoKeyObjectName, IDataPowerClient apiClient)
        {
            _logger.MethodEntry(LogLevel.Debug);
            var bUpdateCryptoKeyObject = false;
            try
            {
                //Look for CryptoKey
                var viewCryptoKeyRequest = new ViewCryptoKeysRequest(ci.Domain);
                _logger.LogTrace($"viewCryptoKeyRequest JSON {JsonConvert.SerializeObject(viewCryptoKeyRequest)}");
                var viewCryptoKeyResponse = apiClient.ViewCryptoKeys(viewCryptoKeyRequest);
                _logger.LogTrace($"viewCryptoKeyResponse JSON {JsonConvert.SerializeObject(viewCryptoKeyResponse)}");
                if (viewCryptoKeyResponse.CryptoKeys.Count(x => x.Name == cryptoKeyObjectName) == 1)
                {
                    _logger.LogTrace("Only One Found, we are good!");
                    bUpdateCryptoKeyObject = true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    $"Crypto Key Object does not exist: {cryptoKeyObjectName} : {LogHandler.FlattenException(ex)}");
            }

            _logger.MethodExit(LogLevel.Debug);
            return bUpdateCryptoKeyObject;
        }

        public void DisableCryptoKeyObject(string cryptoKeyObjectName, IDataPowerClient apiClient)
        {
            _logger.MethodEntry(LogLevel.Debug);
            _logger.LogTrace($"Disable State for Crypto Certificate Object: {cryptoKeyObjectName}");
            try
            {
                var cryptoKeyUpdateRequest = new CryptoKeyUpdateRequest(apiClient.Domain, cryptoKeyObjectName)
                {
                    CryptoKey = new CryptoKey
                    {
                        MAdminState = "disabled",
                        Name = cryptoKeyObjectName,
                        CertFile = null,
                        IgnoreExpiration = null,
                        PasswordAlias = null
                    }
                };
                _logger.LogTrace($"cryptoKeyUpdateRequest JSON {JsonConvert.SerializeObject(cryptoKeyUpdateRequest)}");
                apiClient.UpdateCryptoKey(cryptoKeyUpdateRequest);
                _logger.LogTrace("Crypto Key Updated!");
                _logger.MethodExit(LogLevel.Debug);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    $"There was an issue disabling the certificate *key*: {cryptoKeyObjectName} Error {LogHandler.FlattenException(ex)}");
            }
        }

        public void UpdatePrivateKey(CertStoreInfo ci, string cryptoKeyObjectName,
            IDataPowerClient apiClient, string keyFileName, string alias)
        {
            _logger.MethodEntry(LogLevel.Debug);
            _logger.LogTrace($"Updating Crypto Key Object: {cryptoKeyObjectName}");
            try
            {
                var cryptoKeyRequest = new CryptoKeyUpdateRequest(apiClient.Domain, cryptoKeyObjectName)
                {
                    CryptoKey = new CryptoKey
                    {
                        CertFile = ci.CertificateStore.Trim() + ":///" + keyFileName,
                        Name = cryptoKeyObjectName
                    }
                };
                _logger.LogTrace($"cryptoKeyRequest JSON {JsonConvert.SerializeObject(cryptoKeyRequest)}");
                apiClient.UpdateCryptoKey(cryptoKeyRequest);
                _logger.LogTrace("Private Key Updated!");
                _logger.MethodExit(LogLevel.Debug);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    $"There was an issue updating the private key: {cryptoKeyObjectName} Error {LogHandler.FlattenException(ex)}");
            }
        }

        public void AddCryptoKey(CertStoreInfo ci, string cryptoKeyObjectName, IDataPowerClient apiClient,
            string keyFileName,
            string alias)
        {
            _logger.MethodEntry(LogLevel.Debug);
            _logger.LogTrace(
                $"Adding CryptoKey Object for Private Key {alias} to CERT store with Filename {keyFileName} ");
            try
            {
                var cryptoKeyRequest = new CryptoKeyAddRequest(apiClient.Domain)
                {
                    CryptoKey = new CryptoKey
                    {
                        CertFile = ci.CertificateStore.Trim() + ":///" + keyFileName,
                        Name = cryptoKeyObjectName
                    }
                };
                _logger.LogTrace($"cryptoKeyRequest JSON {JsonConvert.SerializeObject(cryptoKeyRequest)}");
                apiClient.AddCryptoKey(cryptoKeyRequest);
                _logger.LogTrace("Private Key Added!");
                _logger.MethodExit(LogLevel.Debug);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    $"Error Adding CryptoKey Object for Private Key {alias}: {cryptoKeyObjectName} Error {LogHandler.FlattenException(ex)}");
            }
        }

        public JobResult RemovePrivateKeyFile(ManagementJobConfiguration addConfig, CertStoreInfo ci,
            string keyFileName, string fileDomain)
        {
            try
            {
                _logger.MethodEntry(LogLevel.Debug);
                _logger.LogTrace($"Removing Old Private Key File {keyFileName}");
                var removeFileResult = RemoveFile(addConfig, ci, keyFileName, fileDomain);
                _logger.LogTrace($"Private Key {keyFileName} is removed");
                _logger.MethodExit(LogLevel.Debug);
                return removeFileResult;
            }
            catch (Exception e)
            {
                _logger.LogError($"Error In CertManager.RemovePrivateKeyFile: {LogHandler.FlattenException(e)}");
                throw;
            }
        }

        public CertificateAddRequest AddPrivateKey(CertStoreInfo ci, string alias, string keyFileName,
            IDataPowerClient apiClient,
            string privateKeyString, string fileDomain)
        {
            _logger.MethodEntry(LogLevel.Debug);
            _logger.LogTrace($"Adding Private Key {alias} to CERT store with Filename {keyFileName} ");
            try
            {
                var certKeyRequest = new CertificateAddRequest(fileDomain, keyFileName, ci.CertificateStore)
                {
                    Certificate = new CertificateRequest
                    {
                        Name = keyFileName,
                        Content = privateKeyString
                    }
                };
                _logger.LogTrace($"certKeyRequest JSON {JsonConvert.SerializeObject(certKeyRequest)}");
                _logger.MethodExit(LogLevel.Debug);
                return certKeyRequest;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    $"Error Adding Private Key {alias} to CERT store with Filename {keyFileName} Error {LogHandler.FlattenException(ex)}");
            }

            return null;
        }

        public void UpdateCryptoCert(CertStoreInfo ci, string cryptoCertObjectName,
            IDataPowerClient apiClient, string certFileName, string alias)
        {
            _logger.MethodEntry(LogLevel.Debug);
            _logger.LogTrace($"Updating Crypto Certificate Object: {cryptoCertObjectName}");
            try
            {
                var cryptoCertRequest = new CryptoCertificateUpdateRequest(apiClient.Domain, cryptoCertObjectName)
                {
                    CryptoCert = new CryptoCertificate
                    {
                        CertFile = ci.CertificateStore.Trim() + ":///" + certFileName,
                        Name = cryptoCertObjectName
                    }
                };

                _logger.LogTrace($"certKeyRequest JSON {JsonConvert.SerializeObject(cryptoCertRequest)}");
                apiClient.UpdateCryptoCertificate(cryptoCertRequest);
                _logger.LogTrace("UpdateCryptoCert Updated !");
                _logger.MethodExit(LogLevel.Debug);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    $"Error Updating Crypto Certificate Object: {cryptoCertObjectName} Error {LogHandler.FlattenException(ex)}");
            }
        }

        public void AddCryptoCert(CertStoreInfo ci, string cryptoCertObjectName, IDataPowerClient apiClient,
            string certFileName,
            string alias)
        {
            _logger.MethodEntry(LogLevel.Debug);
            _logger.LogTrace(
                $"Adding Crypto Object for Certificate {alias} to CERT store with Filename {certFileName} ");
            try
            {
                var cryptoCertRequest = new CryptoCertificateAddRequest(apiClient.Domain)
                {
                    CryptoCert = new CryptoCertificate
                    {
                        CertFile = ci.CertificateStore.Trim() + ":///" + certFileName,
                        Name = cryptoCertObjectName
                    }
                };
                _logger.LogTrace($"cryptoCertRequest JSON {JsonConvert.SerializeObject(cryptoCertRequest)}");
                apiClient.AddCryptoCertificate(cryptoCertRequest);
                _logger.LogTrace("AddCryptoCert Added!");
                _logger.MethodExit(LogLevel.Debug);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    $"Error Adding Crypto Object for Certificate {alias} to CERT store with Filename {certFileName} Error {LogHandler.FlattenException(ex)}");
            }
        }

        public JobResult RemoveCertificate(ManagementJobConfiguration addConfig, CertStoreInfo ci, string certFileName,
            string fileDomain)
        {
            try
            {
                _logger.MethodEntry(LogLevel.Debug);
                _logger.LogTrace($"Removing Old Certificate File {certFileName}");
                var result = RemoveFile(addConfig, ci, certFileName, fileDomain);
                _logger.LogTrace($"Old Certificate File {certFileName} is removed");
                _logger.MethodExit(LogLevel.Debug);
                return result;
            }
            catch (Exception e)
            {
                _logger.LogError($"Error In CertManager.RemovePrivateKeyFile: {LogHandler.FlattenException(e)}");
                throw;
            }
        }

        public CertificateAddRequest CertificateAddRequest(CertStoreInfo ci, string alias, string certFileName,
            IDataPowerClient apiClient, string certPem, string fileDomain)
        {
            _logger.MethodEntry(LogLevel.Debug);
            _logger.LogTrace($"Adding Certificate {alias} with Filename {certFileName} ");
            try
            {
                var certRequest = new CertificateAddRequest(fileDomain, certFileName, ci.CertificateStore)
                {
                    Certificate = new CertificateRequest
                    {
                        Name = certFileName,
                        Content = certPem
                    }
                };
                _logger.LogTrace($"certRequest JSON {JsonConvert.SerializeObject(certRequest)}");
                _logger.MethodExit(LogLevel.Debug);
                return certRequest;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    $"Error Adding Certificate {alias} with Filename {certFileName} Error {LogHandler.FlattenException(ex)}");
            }

            return null;
        }

        public bool DoesKeyFileExist(CertStoreInfo ci, string keyFileName,
            ViewPublicCertificatesResponse viewCertificateCollection)
        {
            _logger.MethodEntry(LogLevel.Debug);
            var bRemoveKeyFile = false;
            try
            {
                var keyFile =
                    viewCertificateCollection.PubFileStoreLocation?.PubFileStore?.PubFiles?.FirstOrDefault(x =>
                        x?.Name == keyFileName);

                if (!(keyFile is null))
                {
                    _logger.LogTrace($"Matching Key File {keyFileName} was found in domain {ci.Domain}");
                    bRemoveKeyFile = true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    $"Error Matching Key File {keyFileName} was found in domain {ci.Domain} Error {LogHandler.FlattenException(ex)}");
            }

            _logger.MethodExit(LogLevel.Debug);
            return bRemoveKeyFile;
        }

        public bool DoesCertificateFileExist(CertStoreInfo ci, IDataPowerClient apiClient,
            string certFileName, ViewPublicCertificatesResponse viewCertificateCollection)
        {
            _logger.MethodEntry(LogLevel.Debug);
            var bRemoveCertificateFile = false;
            try
            {
                var publicCertificate =
                    viewCertificateCollection.PubFileStoreLocation?.PubFileStore?.PubFiles?.FirstOrDefault(x =>
                        x?.Name == certFileName);

                if (!(publicCertificate is null))
                {
                    _logger.LogTrace($"Matching Certificate File {certFileName} was found in domain {ci.Domain}");
                    bRemoveCertificateFile = true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    $"Error Matching Certificate File {certFileName} was found in domain {ci.Domain} Error {LogHandler.FlattenException(ex)}");
            }

            _logger.MethodExit(LogLevel.Debug);
            return bRemoveCertificateFile;
        }


        public string GetCertPem(ManagementJobConfiguration addConfig, string alias, ref string privateKeyString)
        {
            _logger.MethodEntry(LogLevel.Debug);
            _logger.LogTrace($"alias {alias} privateKeyString {privateKeyString}");
            string certPem = null;
            try
            {
                if (!string.IsNullOrEmpty(addConfig.JobCertificate.PrivateKeyPassword))
                {
                    _logger.LogTrace($"Certificate and Key exist for {alias}");
                    var certData = Convert.FromBase64String(addConfig.JobCertificate.Contents);

                    using var ms = new MemoryStream(certData);
                    var store = new Pkcs12Store(ms,
                        addConfig.JobCertificate.PrivateKeyPassword.ToCharArray());

                    string storeAlias;
                    TextWriter streamWriter;
                    using (var memoryStream = new MemoryStream())
                    {
                        streamWriter = new StreamWriter(memoryStream);
                        var pemWriter = new PemWriter(streamWriter);

                        storeAlias = store.Aliases.Cast<string>().SingleOrDefault(a => store.IsKeyEntry(a));
                        var publicKey = store.GetCertificate(storeAlias).Certificate.GetPublicKey();
                        var privateKey = store.GetKey(storeAlias).Key;
                        var keyPair = new AsymmetricCipherKeyPair(publicKey, privateKey);

                        var pkStart = "-----BEGIN RSA PRIVATE KEY-----\n";
                        var pkEnd = "\n-----END RSA PRIVATE KEY-----";


                        pemWriter.WriteObject(keyPair.Private);
                        streamWriter.Flush();
                        privateKeyString = Encoding.ASCII.GetString(memoryStream.GetBuffer()).Trim()
                            .Replace("\r", "")
                            .Replace("\0", "");
                        privateKeyString = privateKeyString.Replace(pkStart, "").Replace(pkEnd, "");

                        memoryStream.Close();
                    }

                    streamWriter.Close();

                    // Extract server certificate
                    certPem = Utility.Pemify(
                        Convert.ToBase64String(store.GetCertificate(storeAlias).Certificate.GetEncoded()));
                }
                else
                {
                    _logger.LogTrace($"Certificate ONLY for {alias}");
                    certPem = Utility.Pemify(addConfig.JobCertificate.Contents);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error Generating PEM: Error {LogHandler.FlattenException(ex)}");
            }

            _logger.LogTrace($"PEM {certPem}");
            _logger.MethodEntry(LogLevel.Debug);
            return certPem;
        }

        public JobResult AddPubCert(ManagementJobConfiguration addPubConfig, CertStoreInfo ci)
        {
            _logger.MethodEntry(LogLevel.Debug);
            ServerUserName = ResolvePamField("ServerUserName",addPubConfig.ServerUsername);
            ServerPassword = ResolvePamField("ServerPassword",addPubConfig.ServerPassword);

            _protocol = ci.Protocol;
            _logger.LogTrace(
                $"Entering AddPubCert for Domain: {ci.Domain} and Certificate Store: {ci.CertificateStore}");
            _logger.LogTrace(
                $"Creating API Client Created with user: {ServerUserName} password: {ServerPassword} protocol: {_protocol} ClientMachine: {addPubConfig.CertificateStoreDetails.ClientMachine.Trim()} Domain: {ci.Domain}");
            var apiClient = ClientFactory(ServerUserName, ServerPassword,
                $"{_protocol}://" + addPubConfig.CertificateStoreDetails.ClientMachine.Trim(), ci.Domain);
            _logger.LogTrace("API Client Created");

            var certAlias = addPubConfig.JobCertificate.Alias;

            if (string.IsNullOrEmpty(certAlias))
                certAlias = Guid.NewGuid().ToString();

            _logger.LogTrace($"certAlias {certAlias}");

            try
            {
                Pkcs12Store store;
                string certPem;
                var certData = Convert.FromBase64String(addPubConfig.JobCertificate.Contents);

                //If you have a password then you will get a PFX in return instead of the base64 encoded string
                if (!string.IsNullOrEmpty(addPubConfig.JobCertificate?.PrivateKeyPassword))
                {
                    _logger.LogTrace($"Has PFX Password");
                    using var ms = new MemoryStream(certData);
                    store = new Pkcs12Store(ms, addPubConfig.JobCertificate?.PrivateKeyPassword.ToCharArray());
                    var storeAlias = store.Aliases.Cast<string>().SingleOrDefault(a => store.IsKeyEntry(a));
                    certPem = Utility.Pemify(
                        Convert.ToBase64String(store.GetCertificate(storeAlias).Certificate.GetEncoded()));
                }
                else
                {
                    certPem = Utility.Pemify(addPubConfig.JobCertificate.Contents);
                }

                _logger.LogTrace($"certPem {certPem}");

                var certFileName = certAlias.Replace(".", "_") + ".pem";

                _logger.LogTrace(
                    $"Adding Public Cert Certificate {certAlias} to PubCert store with Filename {certFileName} ");
                var certRequest =
                    new CertificateAddRequest(apiClient.Domain, certFileName, ci.CertificateStore.Trim())
                    {
                        Certificate = new CertificateRequest
                        {
                            Name = certFileName,
                            Content = certPem
                        }
                    };
                _logger.LogTrace($"certRequest JSON {JsonConvert.SerializeObject(certRequest)}");
                apiClient.AddCertificateFile(certRequest);
                _logger.LogTrace("Certificate Added!");
                apiClient.SaveConfig();
                _logger.LogTrace("Configuration Saved!");
            }
            catch (Exception ex)
            {
                _logger.LogTrace($"Error on {certAlias}: {LogHandler.FlattenException(ex)}");
                apiClient.SaveConfig();
                return new JobResult
                {
                    Result = OrchestratorJobStatusJobResult.Failure,
                    JobHistoryId = addPubConfig.JobHistoryId,
                    FailureMessage = $"Error in Add Pub Cert {LogHandler.FlattenException(ex)}"
                };
            }

            _logger.MethodExit(LogLevel.Debug);
            return new JobResult
            {
                Result = OrchestratorJobStatusJobResult.Success,
                JobHistoryId = addPubConfig.JobHistoryId,
                FailureMessage = ""
            };
        }


        private JobResult RemoveCertFromDomain(ManagementJobConfiguration removeConfig, CertStoreInfo ci, NamePrefix np)
        {
            _logger.MethodEntry(LogLevel.Debug);
            ServerUserName = ResolvePamField("ServerUserName", removeConfig.ServerUsername);
            ServerPassword = ResolvePamField("ServerPassword", removeConfig.ServerPassword);
            _protocol = ci.Protocol;
            _logger.LogTrace($"Entering RemoveCertStore for {removeConfig.JobCertificate.Alias} ");
            _logger.LogTrace(
                $"Entering RemoveCertStore for Domain: {ci.Domain} and Certificate Store: {ci.CertificateStore}");
            _logger.LogTrace(
                $"Creating API Client Created with user: {ServerUserName} protocol: {_protocol} ClientMachine: {removeConfig.CertificateStoreDetails.ClientMachine.Trim()} Domain: {ci.Domain}");
            var apiClient = ClientFactory(ServerUserName, ServerPassword,
                $"{_protocol}://" + removeConfig.CertificateStoreDetails.ClientMachine.Trim(), ci.Domain);
            _logger.LogTrace("API Client Created!");

            // Config objects (CryptoCertificate/CryptoKey) live in ci.Domain, but a
            // sharedcert:// file itself can only be deleted through the default domain -
            // see FileDomainFor().
            var fileDomain = FileDomainFor(ci);
            var certFolder = ci.CertificateStore?.Trim() ?? "cert";

            try
            {
                _logger.LogTrace($"Checking to find CryptoCertObject {removeConfig.JobCertificate.Alias} ");
                var viewCert = new ViewCryptoCertificatesRequest(apiClient.Domain, removeConfig.JobCertificate.Alias);
                _logger.LogTrace($"viewCert JSON {JsonConvert.SerializeObject(viewCert)}");

                var viewCertificateSingle = apiClient.ViewCryptoCertificate(viewCert);
                _logger.LogTrace($"viewCert JSON {JsonConvert.SerializeObject(viewCertificateSingle)}");

                if (viewCertificateSingle != null &&
                    !string.IsNullOrEmpty(viewCertificateSingle.CryptoCertificate.Name))
                {
                    _logger.LogTrace($"Remove CryptoObject {viewCertificateSingle.CryptoCertificate.Name} ");
                    var request =
                        new DeleteCryptoCertificateRequest(apiClient.Domain, removeConfig.JobCertificate.Alias);
                    _logger.LogTrace($"request JSON {JsonConvert.SerializeObject(request)}");
                    apiClient.DeleteCryptoCertificate(request);
                    _logger.LogTrace($"Remove Certificate File {viewCertificateSingle.CryptoCertificate.CertFile} ");
                    var request2 = new DeleteCertificateRequest(fileDomain,
                        viewCertificateSingle.CryptoCertificate.CertFile.Replace(ci.CertificateStore + ":///", ""),
                        certFolder);
                    _logger.LogTrace($"request2 JSON {JsonConvert.SerializeObject(request2)}");
                    apiClient.DeleteCertificate(request2);
                    _logger.LogTrace("Certificate Deleted!");
                }

                var cryptoKeyObjectName = Utility.ReplaceFirstOccurrence(removeConfig.JobCertificate.Alias,
                    np.CryptoCertObjectPrefix?.Trim() ?? string.Empty,
                    np.CryptoKeyObjectPrefix?.Trim() ?? string.Empty);
                _logger.LogTrace($"Checking to find CryptoKeyObject {cryptoKeyObjectName} ");
                var viewKey = new ViewCryptoKeysRequest(apiClient.Domain);
                _logger.LogTrace($"viewKey JSON {JsonConvert.SerializeObject(viewKey)}");
                var viewKeyResponse = apiClient.ViewCryptoKeys(viewKey);
                _logger.LogTrace($"viewKeyResponse JSON {JsonConvert.SerializeObject(viewKeyResponse)}");
                var cryptoKey = viewKeyResponse.CryptoKeys.FirstOrDefault(x => x.Name == cryptoKeyObjectName);
                _logger.LogTrace($"cryptoKey JSON {JsonConvert.SerializeObject(cryptoKey)}");
                if (viewKeyResponse.CryptoKeys != null && !string.IsNullOrEmpty(cryptoKey?.Name))
                {
                    _logger.LogTrace($"Remove CryptoKeyObject {cryptoKey.Name} ");
                    var request = new DeleteCryptoKeyRequest(apiClient.Domain, cryptoKeyObjectName);
                    _logger.LogTrace($"request JSON {JsonConvert.SerializeObject(request)}");
                    apiClient.DeleteCryptoKey(request);
                    _logger.LogTrace($"Remove Key File {cryptoKey.CertFile} ");
                    var request2 = new DeleteCertificateRequest(fileDomain,
                        cryptoKey.CertFile.Replace(ci.CertificateStore + ":///", ""), certFolder);
                    _logger.LogTrace($"request2 JSON {JsonConvert.SerializeObject(request2)}");
                    apiClient.DeleteCertificate(request2);
                    _logger.LogTrace("Certificate Deleted!");
                }
            }
            catch (Exception ex)
            {
                _logger.LogTrace($"Error on {removeConfig.JobCertificate.Alias}: {LogHandler.FlattenException(ex)}");
            }

            _logger.LogTrace("Saving Config!");
            apiClient.SaveConfig();
            _logger.LogTrace("Config Saved!");
            _logger.MethodExit(LogLevel.Debug);

            return new JobResult
            {
                Result = OrchestratorJobStatusJobResult.Success,
                JobHistoryId = removeConfig.JobHistoryId,
                FailureMessage = ""
            };
        }

        private JobResult RemoveFile(ManagementJobConfiguration removeConfig, CertStoreInfo ci, string filename,
            string fileDomain)
        {
            _logger.MethodEntry(LogLevel.Debug);
            ServerUserName = ResolvePamField("ServerUserName", removeConfig.ServerUsername);
            ServerPassword = ResolvePamField("ServerPassword", removeConfig.ServerPassword);
            _logger.LogTrace($"Entering RemoveFile for {removeConfig.JobCertificate.Alias} filename {filename}");
            _logger.LogTrace(
                $"Entering RemoveFile for Domain: {ci.Domain} and Certificate Store: {ci.CertificateStore}");
            _logger.LogTrace(
                $"Creating API Client Created with user: {ServerUserName} password: {ServerPassword} protocol: {_protocol} ClientMachine: {removeConfig.CertificateStoreDetails.ClientMachine.Trim()} Domain: {ci.Domain}");
            var apiClient = ClientFactory(ServerUserName, ServerPassword,
                $"{_protocol}://" + removeConfig.CertificateStoreDetails.ClientMachine.Trim(), ci.Domain);
            _logger.LogTrace("Api Client Created!");
            try
            {
                _logger.LogTrace($"Deleting Actual File {filename} in domain {fileDomain}");
                var request2 = new DeleteCertificateRequest(fileDomain,
                    filename.Replace(ci.CertificateStore + ":///", ""), ci.CertificateStore?.Trim() ?? "cert");
                _logger.LogTrace($"request2 JSON {JsonConvert.SerializeObject(request2)}");
                apiClient.DeleteCertificate(request2);
                _logger.LogTrace("Certificate Deleted!");
            }
            catch (Exception ex)
            {
                _logger.LogTrace($"Error on {removeConfig.JobCertificate.Alias}: {LogHandler.FlattenException(ex)}");
            }

            _logger.LogTrace("Saving Config!");
            apiClient.SaveConfig();
            _logger.LogTrace("Config Saved!");

            _logger.MethodExit(LogLevel.Debug);
            return new JobResult
            {
                Result = OrchestratorJobStatusJobResult.Success,
                JobHistoryId = removeConfig.JobHistoryId,
                FailureMessage = ""
            };
        }

        public JobResult Remove(ManagementJobConfiguration removeConfig, CertStoreInfo ci, NamePrefix np)
        {
            try
            {
                _logger.MethodEntry(LogLevel.Debug);

                var publicCertStoreName = ci.PublicCertStoreName;
                var storePath = removeConfig.CertificateStoreDetails.StorePath;
                _logger.LogTrace($"publicCertStoreName: {publicCertStoreName} storePath: {storePath}");

                if (storePath.Contains(publicCertStoreName))
                {
                    _logger.LogTrace("Cannot Remove Public Certificates");
                    return new JobResult
                    {
                        Result = OrchestratorJobStatusJobResult.Failure,
                        JobHistoryId = removeConfig.JobHistoryId,
                        FailureMessage = "Cannot Remove Public Certificates"
                    };
                }

                var result = RemoveCertFromDomain(removeConfig, ci, np);
                _logger.MethodExit(LogLevel.Debug);
                _logger.LogTrace($"AnyErrors Return {JsonConvert.SerializeObject(result)}");
                return result;
            }
            catch (Exception e)
            {
                _logger.LogError($"Error In CertManager.Remove {LogHandler.FlattenException(e)}!");
                throw;
            }
        }

        public JobResult Add(ManagementJobConfiguration addConfig, CertStoreInfo ci, NamePrefix np)
        {
            _logger.MethodEntry(LogLevel.Debug);
            try
            {
                _logger.LogTrace("Entering Add");

                var publicCertStoreName = ci.PublicCertStoreName;
                var storePath = addConfig.CertificateStoreDetails.StorePath;
                _logger.LogTrace($"publicCertStoreName: {publicCertStoreName} storePath: {storePath}");

                // pubcert's underlying filestore is appliance-wide and owned by the default
                // domain - DataPower's REST mgmt rejects writes through any non-default
                // domain context with a 403. Reject upfront with a clear message instead of
                // letting the appliance's "Forbidden." come back. sharedcert's filestore is
                // also appliance-wide/default-owned, but unlike pubcert its CryptoCertificate
                // config objects are per-domain (that's why sharedcert stores are per-domain
                // now) - AddCertStore/FileDomainFor() route the filestore write to default
                // regardless of ci.Domain, so no upfront store-path restriction is needed here.
                if (storePath.Contains("pubcert"))
                {
                    if (storePath != publicCertStoreName && (storePath != "default\\" + publicCertStoreName))
                    {
                        return new JobResult
                        {
                            Result = OrchestratorJobStatusJobResult.Failure,
                            JobHistoryId = addConfig.JobHistoryId,
                            FailureMessage = "You can only add to pubcert on the default domain"
                        };
                    }
                }

                var result = storePath.Contains(publicCertStoreName)
                    ? AddPubCert(addConfig, ci)
                    : AddCertStore(addConfig, ci, np);
                _logger.LogTrace($"result Return {JsonConvert.SerializeObject(result)}");
                _logger.MethodExit(LogLevel.Debug);
                return result;
            }
            catch (Exception e)
            {
                _logger.LogError($"Error In CertManager.Add {LogHandler.FlattenException(e)}!");
                throw;
            }
        }

        private JobResult AddCertStore(ManagementJobConfiguration addConfig, CertStoreInfo ci, NamePrefix np)
        {
            _logger.MethodEntry(LogLevel.Debug);
            ServerUserName = ResolvePamField("ServerUserName", addConfig.ServerUsername);
            ServerPassword = ResolvePamField("ServerPassword", addConfig.ServerPassword);
            var privateKeyString = "";
            _protocol = ci.Protocol;
            _logger.LogTrace(
                $"Entering AddCertStore for Domain: {ci.Domain} and Certificate Store: {ci.CertificateStore}");
            _logger.LogTrace(
                $"Creating API Client Created with user: {ServerUserName} protocol: {_protocol} ClientMachine: {addConfig.CertificateStoreDetails.ClientMachine.Trim()} Domain: {ci.Domain}");
            var apiClient = ClientFactory(ServerUserName, ServerPassword,
                $"{_protocol}://" + addConfig.CertificateStoreDetails.ClientMachine.Trim(),
                ci.Domain);
            _logger.LogTrace("apiClient created!");

            // Config objects (CryptoCertificate/CryptoKey) are created/updated in
            // ci.Domain, but a sharedcert:// file can only be written through the default
            // domain - see FileDomainFor().
            var fileDomain = FileDomainFor(ci);

            var alias = addConfig.JobCertificate.Alias.ToLower();
            if (string.IsNullOrEmpty(alias))
                alias = Guid.NewGuid().ToString().ToLower();

            _logger.LogTrace($"alias: {alias}");

            try
            {
                if (!string.IsNullOrEmpty(addConfig.JobCertificate.PrivateKeyPassword))
                {
                    _logger.LogTrace($"Has Password");
                    var certPem = GetCertPem(addConfig, alias, ref privateKeyString);
                    _logger.LogTrace($"certPem: {certPem}");
                    var baseAlias = alias.ToLower();
                    _logger.LogTrace($"baseAlias: {baseAlias}");

                    var cryptoObjectPrefix = np.CryptoCertObjectPrefix?.Trim().ToLower() ?? string.Empty;
                    var keyFileNamePrefix = np.KeyFilePrefix?.Trim().ToLower() ?? string.Empty;
                    var certFileNamePrefix = np.CertFilePrefix?.Trim().ToLower() ?? string.Empty;
                    var cryptoKeyObjectPrefix = np.CryptoKeyObjectPrefix?.Trim().ToLower() ?? string.Empty;

                    _logger.LogTrace($"cryptoObjectPrefix: {cryptoObjectPrefix}");
                    _logger.LogTrace($"keyFileNamePrefix: {keyFileNamePrefix}");
                    _logger.LogTrace($"certFileNamePrefix: {certFileNamePrefix}");
                    _logger.LogTrace($"cryptoKeyObjectPrefix: {cryptoKeyObjectPrefix}");

                    if (alias.ToLower().StartsWith(cryptoObjectPrefix))
                        baseAlias = Utility.ReplaceAlias(alias.ToLower(), cryptoObjectPrefix,
                            "");

                    _logger.LogTrace($"baseAlias: {baseAlias}");

                    var certFileName = certFileNamePrefix + baseAlias + ".cer";
                    var keyFileName = keyFileNamePrefix + baseAlias + ".pem";
                    var cryptoCertObjectName = cryptoObjectPrefix + baseAlias;
                    var cryptoKeyObjectName = cryptoKeyObjectPrefix + baseAlias;

                    _logger.LogTrace($"certFileName: {certFileName}");
                    _logger.LogTrace($"keyFileName: {keyFileName}");
                    _logger.LogTrace($"cryptoCertObjectName: {cryptoCertObjectName}");
                    _logger.LogTrace($"cryptoKeyObjectName: {cryptoKeyObjectName}");

                    //Get the certificate collection to be used to check for cert files and private keys
                    var viewCert = new ViewPublicCertificatesRequest(fileDomain, ci.CertificateStore);
                    _logger.LogTrace($"viewCert JSON {JsonConvert.SerializeObject(viewCert)}");
                    var viewCertificateCollection = apiClient.ViewPublicCertificates(viewCert);
                    _logger.LogTrace(
                        $"viewCertificateCollection JSON {JsonConvert.SerializeObject(viewCertificateCollection)}");

                    _logger.LogTrace("Starting ReplaceCertificateFile!");
                    ReplaceCertificateFile(addConfig, ci, apiClient, certFileName, viewCertificateCollection, alias,
                        certPem, fileDomain);
                    _logger.LogTrace("Finished ReplaceCertificateFile!");
                    _logger.LogTrace("Starting ReplaceCryptoObject!");
                    ReplaceCryptoObject(ci, cryptoCertObjectName, apiClient, certFileName, alias);
                    _logger.LogTrace("Finished ReplaceCryptoObject!");
                    _logger.LogTrace("Starting ReplacePrivateKey!");
                    ReplacePrivateKey(addConfig, ci, keyFileName, viewCertificateCollection, alias, apiClient,
                        privateKeyString, fileDomain);
                    _logger.LogTrace("Finished ReplacePrivateKey!");
                    _logger.LogTrace("Starting ReplaceCryptoKeyObject!");
                    ReplaceCryptoKeyObject(ci, cryptoKeyObjectName, apiClient, keyFileName, alias);
                    _logger.LogTrace("Finished ReplaceCryptoKeyObject!");
                }

                _logger.LogTrace("Saving Config!");
                apiClient.SaveConfig();
                _logger.LogTrace("Config Saved!");
            }
            catch (Exception ex)
            {
                // Silent-failure trap fix: this catch used to log Trace, run SaveConfig
                // (persisting whatever partial state the appliance had reached when the
                // Add blew up), and fall through to return Success. So a 403 / 404 / 500
                // from DataPower would surface as a green checkmark in Command with no
                // cert anywhere on the appliance. Now: log Error and propagate Failure
                // with the appliance's response body when available.
                var apiEx = DataPowerApiException.Find(ex);
                var detail = apiEx != null
                    ? $"{apiEx.Operation} returned HTTP {(int)apiEx.StatusCode} {apiEx.StatusCode}: {apiEx.ResponseBody}"
                    : ex.Message;

                _logger.LogError(ex,
                    "Add to {Domain}\\{Store} failed for alias {Alias}: {ErrorMessage}",
                    ci.Domain, ci.CertificateStore, alias, LogHandler.FlattenException(ex));

                return new JobResult
                {
                    Result = OrchestratorJobStatusJobResult.Failure,
                    JobHistoryId = addConfig.JobHistoryId,
                    FailureMessage = $"Add failed for '{alias}' on {ci.Domain}\\{ci.CertificateStore}: {detail}"
                };
            }

            _logger.MethodExit(LogLevel.Debug);

            return new JobResult
            {
                Result = OrchestratorJobStatusJobResult.Success,
                JobHistoryId = addConfig.JobHistoryId,
                FailureMessage = ""
            };
        }

        private void ReplacePrivateKey(ManagementJobConfiguration addConfig, CertStoreInfo ci, string keyFileName,
            ViewPublicCertificatesResponse viewCertificateCollection, string alias, IDataPowerClient apiClient,
            string privateKeyString, string fileDomain)
        {
            _logger.MethodEntry(LogLevel.Debug);
            try
            {
                //See if KeyFile Exists if so remove and add a new one, if not just add a new one
                var bRemoveKeyFile = DoesKeyFileExist(ci, keyFileName, viewCertificateCollection);
                _logger.LogTrace($"bRemoveKeyFile {bRemoveKeyFile}");
                if (bRemoveKeyFile)
                {
                    _logger.LogTrace("Removing Private Key!");
                    RemovePrivateKeyFile(addConfig, ci, keyFileName, fileDomain);
                    _logger.LogTrace("Private Key Removed!");
                }

                var certKeyRequest =
                    AddPrivateKey(ci, alias, keyFileName, apiClient, privateKeyString, fileDomain);
                _logger.LogTrace($"certKeyRequest {JsonConvert.SerializeObject(certKeyRequest)}");
                _logger.LogTrace($"Adding Private File {keyFileName}");
                apiClient.AddCertificateFile(certKeyRequest);
                _logger.LogTrace("Certificate File Added!");
                _logger.MethodExit(LogLevel.Debug);
            }
            catch (Exception e)
            {
                _logger.LogError($"Error in CertManager.ReplacePrivateKey {LogHandler.FlattenException(e)}");
                throw;
            }
        }

        private void ReplaceCertificateFile(ManagementJobConfiguration addConfig, CertStoreInfo ci,
            IDataPowerClient apiClient,
            string certFileName, ViewPublicCertificatesResponse viewCertificateCollection, string alias,
            string certPem, string fileDomain)
        {
            try
            {
                _logger.MethodEntry(LogLevel.Debug);
                _logger.LogTrace($"Cert Store Info {JsonConvert.SerializeObject(ci)}");
                _logger.LogTrace($"Cert Pem {certPem}");
                _logger.LogTrace($"certFileName {certFileName}");
                _logger.LogTrace($"alias {alias}");

                //See if Certificate File Exists, if so remove it and add a new one, if not just add it
                var certificateFileExists =
                    DoesCertificateFileExist(ci, apiClient, certFileName, viewCertificateCollection);
                if (certificateFileExists)
                    RemoveCertificate(addConfig, ci, certFileName, fileDomain);

                _logger.LogTrace($"Adding Certificate File {certFileName}");
                var certRequest = CertificateAddRequest(ci, alias, certFileName, apiClient, certPem, fileDomain);
                apiClient.AddCertificateFile(certRequest);
            }
            catch (Exception e)
            {
                _logger.LogError($"Error in CertManager.ReplaceCertificateFile {LogHandler.FlattenException(e)}");
                throw;
            }
        }

        private void ReplaceCryptoKeyObject(CertStoreInfo ci, string cryptoKeyObjectName, IDataPowerClient apiClient,
            string keyFileName,
            string alias)
        {
            try
            {
                _logger.MethodEntry(LogLevel.Debug);

                _logger.LogTrace($"Cert Store Info {JsonConvert.SerializeObject(ci)}");
                _logger.LogTrace($"Crypto Key Object Name {cryptoKeyObjectName}");
                _logger.LogTrace($"keyFileName {keyFileName}");
                _logger.LogTrace($"alias {alias}");

                //Search to See if the Crypto *Key* Object Already Exists (If so, it needs disabled and updated, If not add a new one)
                //Crypto Objects can not be removed since they may be already referenced by sites and such so they need disabled instead
                var cryptoKeyExists =
                    DoesCryptoKeyObjectExist(ci, cryptoKeyObjectName, apiClient);
                _logger.LogTrace($"Crypto Object Exists equals {cryptoKeyExists}");

                if (cryptoKeyExists)
                {
                    _logger.LogTrace("Disabling Crypto Key Object...");
                    DisableCryptoKeyObject(cryptoKeyObjectName, apiClient);
                    _logger.LogTrace("Updating Crypto Key Object...");
                    UpdatePrivateKey(ci, cryptoKeyObjectName, apiClient, keyFileName, alias);
                    _logger.LogTrace("Crypto Key Object Updated...");
                }
                else
                {
                    AddCryptoKey(ci, cryptoKeyObjectName, apiClient, keyFileName, alias);
                }

                _logger.MethodExit(LogLevel.Debug);
            }
            catch (Exception e)
            {
                _logger.LogError($"Error in CertManager.ReplaceCryptoKeyObject {LogHandler.FlattenException(e)}");
                throw;
            }
        }

        private void ReplaceCryptoObject(CertStoreInfo ci, string cryptoCertObjectName, IDataPowerClient apiClient,
            string certFileName, string alias)
        {
            try
            {
                _logger.MethodEntry(LogLevel.Debug);
                //Search to See if the Crypto *Certificate* Object Already Exists (If so, it needs disabled and updated, If not add a new one)
                //Crypto Objects can not be removed since they may be already referenced by sites and such so they need disabled instead

                _logger.LogTrace($"Cert Store Info {JsonConvert.SerializeObject(ci)}");
                _logger.LogTrace($"Crypto Object Name {cryptoCertObjectName}");
                _logger.LogTrace($"certFileName {certFileName}");
                _logger.LogTrace($"alias {alias}");

                var cryptoObjectExists =
                    DoesCryptoCertificateObjectExist(ci, cryptoCertObjectName, apiClient);

                _logger.LogTrace($"Crypto Object Exists equals {cryptoObjectExists}");

                if (cryptoObjectExists)
                {
                    _logger.LogTrace("Disabling Crypto Certificate Object...");
                    DisableCryptoCertificateObject(cryptoCertObjectName, apiClient);
                    _logger.LogTrace("Updating Crypto Certificate Object...");
                    UpdateCryptoCert(ci, cryptoCertObjectName, apiClient,
                        certFileName, alias);
                    _logger.LogTrace("Disable and Update Complete..");
                }
                else
                {
                    _logger.LogTrace("Adding Crypto Certificate Object...");
                    AddCryptoCert(ci, cryptoCertObjectName, apiClient, certFileName, alias);
                }

                _logger.MethodExit(LogLevel.Debug);
            }
            catch (Exception e)
            {
                _logger.LogError($"Error in CertManager.ReplaceCryptoObject {LogHandler.FlattenException(e)}");
                throw;
            }
        }

        public JobResult GetPublicCerts(InventoryJobConfiguration config, IDataPowerClient apiClient,
            SubmitInventoryUpdate submitInventory, CertStoreInfo ci, FlowLogger flow = null)
        {
            try
            {
                _logger.LogTrace("GetPublicCerts");
                var viewCert = new ViewPublicCertificatesRequest();
                _logger.LogTrace($"Public Cert List Request {JsonConvert.SerializeObject(viewCert)}");
                var viewCertificateCollection = apiClient.ViewPublicCertificates(viewCert);
                _logger.LogTrace($"Public Cert List Response {JsonConvert.SerializeObject(viewCertificateCollection)}");

                var intCount = 0;
                var blackList = ParseInventoryBlacklist(ci.InventoryBlackList);
                var intMax = ci.InventoryPageSize;

                _logger.LogTrace($"Max Inventory: {intMax} Inventory Black List Count: {blackList.Count}");

                _logger.LogTrace("Got App Config Settings from File");

                // ReSharper disable once CollectionNeverQueried.Local
                var inventoryItems = new List<CurrentInventoryItem>();
                var pubFiles = viewCertificateCollection?.PubFileStoreLocation?.PubFileStore?.PubFiles;
                flow?.Step("GetPublicCerts.ParseResponse", $"pubFileCount={pubFiles?.Length ?? 0}, blacklistCount={blackList.Count}");
                if (pubFiles != null)
                    foreach (var pc in pubFiles)
                    {
                        _logger.LogTrace($"Looping through public files: {pc.Name}");
                        var viewCertDetail = new ViewPubCertificateDetailRequest(pc.Name);
                        _logger.LogTrace($"Cert Detail Request: {JsonConvert.SerializeObject(viewCertDetail)}");
                        try
                        {
                            var viewCertResponse = apiClient.ViewPublicCertificate(viewCertDetail);
                            _logger.LogTrace($"Cert Detail Response: {JsonConvert.SerializeObject(viewCertResponse)}");

                            _logger.LogTrace($"Add to List: {pc.Name}");
                            var pem = Convert.FromBase64String(viewCertResponse.File);
                            var pemString = Utility.GetPemFromResponse(pem);
                            var cert = new X509Certificate2(pem);

                            _logger.LogTrace($"Created X509Certificate2: {cert.SerialNumber} : {cert.Subject}");

                            if (pemString.Contains("BEGIN CERTIFICATE"))
                            {
                                _logger.LogTrace("Valid Pem File Adding to KF");

                                if (intCount < intMax)
                                {
                                    if (!blackList.Contains(pc.Name) && cert.Thumbprint != null)
                                        inventoryItems.Add(
                                            new CurrentInventoryItem
                                            {
                                                Alias = pc.Name,
                                                Certificates = new[] { pemString },
                                                ItemStatus = OrchestratorInventoryItemStatus.Unknown,
                                                PrivateKeyEntry = false,
                                                UseChainLevel = true
                                            });

                                    intCount++;

                                    _logger.LogTrace($"Inv-Certs: {pc.Name}");
                                    _logger.LogTrace($"Certificates: {viewCertResponse.File}");
                                }
                            }
                            else
                            {
                                _logger.LogTrace("Not a valid Pem File, Skipping the Add to Keyfactor...");
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError($"Error on {pc.Name}: {LogHandler.FlattenException(ex)}");
                        }
                    }

                _logger.LogTrace($"Inventory Items: {JsonConvert.SerializeObject(inventoryItems)}");
                flow?.Step("GetPublicCerts.SubmitInventory", $"itemCount={inventoryItems.Count}");
                submitInventory.Invoke(inventoryItems);
                _logger.LogTrace("Submitted Inventory Items...");

                return new JobResult
                {
                    Result = OrchestratorJobStatusJobResult.Success,
                    JobHistoryId = config.JobHistoryId,
                    FailureMessage = ""
                };
            }
            catch (Exception e)
            {
                _logger.LogError($"Error in CertManager.GetPublicCerts {LogHandler.FlattenException(e)}");
                throw;
            }
        }

        public JobResult GetCerts(InventoryJobConfiguration config, IDataPowerClient apiClient,
            SubmitInventoryUpdate submitInventory, CertStoreInfo ci, FlowLogger flow = null)
        {
            try
            {
                _logger.LogTrace("GetCerts");

                // Store paths are per-domain now (including sharedcert - see
                // Discovery.DiscoverSharedCertDomains), so ci.Domain is always the domain
                // that actually owns the CryptoCertificate objects for this store; no
                // cross-domain lookup needed here.
                var viewCert = new ViewCryptoCertificatesRequest(apiClient.Domain);
                _logger.LogTrace($"Get Certs Request: {JsonConvert.SerializeObject(viewCert)}");
                var viewCertificateCollection = apiClient.ViewCertificates(viewCert);
                _logger.LogTrace($"Get Certs Response: {JsonConvert.SerializeObject(viewCertificateCollection)}");
                // ReSharper disable once CollectionNeverQueried.Local
                var inventoryItems = new List<CurrentInventoryItem>();
                var blackList = ParseInventoryBlacklist(ci.InventoryBlackList);

                _logger.LogTrace("Start loop");

                var allCryptoCerts = viewCertificateCollection?.CryptoCertificates ?? Array.Empty<CryptoCertificate>();

                // /mgmt/config/{domain}/CryptoCertificate returns every CryptoCertificate
                // object in the domain regardless of which filestore directory its file
                // lives in. Filter to those whose Filename URI scheme matches the store
                // path we're inventorying so a "{domain}\sharedcert" job doesn't show
                // cert: entries (and vice versa).
                var storeScheme = (ci.CertificateStore ?? string.Empty).Trim() + ":";
                var cryptoCerts = allCryptoCerts
                    .Where(cc => cc?.CertFile != null &&
                                 cc.CertFile.StartsWith(storeScheme, StringComparison.OrdinalIgnoreCase))
                    .ToArray();

                flow?.Step("GetCerts.ParseResponse",
                    $"certCount={cryptoCerts.Length} (filtered from {allCryptoCerts.Length} by scheme '{storeScheme}'), blacklistCount={blackList.Count}");

                // Certs that throw during per-cert detail fetch/parse (e.g. a .p12 that
                // ViewCertificateDetail can't resolve to a bare X509Certificate2) are
                // logged and skipped rather than aborting the whole job - one bad cert
                // shouldn't sink inventory of the rest. But silently returning Success
                // when some certs were dropped hides that from operators watching only
                // Command's job history. Track them so the result can be downgraded to
                // Warning below.
                var unresolvedCerts = new List<string>();
                foreach (var cc in cryptoCerts)
                    if (cc != null && !string.IsNullOrEmpty(cc.Name))
                    {
                        _logger.LogTrace($"Looping through Certificate Store files: {cc.Name}");

                        try
                        {
                            var viewCertDetail = new ViewCertificateDetailRequest(apiClient.Domain)
                            {
                                CertObjectRequest = new CertificateObjectRequest
                                {
                                    ObjectName = cc.Name
                                }
                            };
                            _logger.LogTrace($"Get Cert Request: {JsonConvert.SerializeObject(viewCertDetail)}");
                            var viewCertResponse = apiClient.ViewCryptoCertificate(viewCertDetail);
                            _logger.LogTrace($"Get Cert Response: {JsonConvert.SerializeObject(viewCertResponse)}");

                            //check this is a valid cert, if not fall to the errors
                            var cert = new X509Certificate2(Encoding.UTF8.GetBytes(viewCertResponse.CryptoCertObject
                                .CertDetailsObject.EncodedCert.Value));

                            _logger.LogTrace($"Created X509Certificate2: {cert.SerialNumber} : {cert.Subject}");

                            _logger.LogTrace($"Add to list: {cc.Name}");
                            if (!blackList.Contains(cc.Name) && cert.Thumbprint != null)
                                inventoryItems.Add(
                                    new CurrentInventoryItem
                                    {
                                        Alias = cc.Name,
                                        Certificates = new[]
                                            {viewCertResponse.CryptoCertObject.CertDetailsObject.EncodedCert.Value},
                                        ItemStatus = OrchestratorInventoryItemStatus.Unknown,
                                        PrivateKeyEntry = true,
                                        UseChainLevel = false
                                    });
                        }
                        catch (Exception ex)
                        {
                            unresolvedCerts.Add(cc.Name);
                            _logger.LogError(
                                $"Certificate not retrievable: Error on {cc.Name}: {LogHandler.FlattenException(ex)}");
                        }
                    }

                _logger.LogTrace($"Submitting {inventoryItems.Count} inventory items");
                flow?.Step("GetCerts.SubmitInventory",
                    $"itemCount={inventoryItems.Count}, unresolvedCount={unresolvedCerts.Count}");
                submitInventory.Invoke(inventoryItems);
                _logger.LogTrace("Submitted Inventory Items.");

                if (unresolvedCerts.Count > 0)
                {
                    var sample = string.Join(", ", unresolvedCerts.Take(10));
                    var more = unresolvedCerts.Count - Math.Min(unresolvedCerts.Count, 10);
                    var suffix = more > 0 ? $" (+{more} more)" : "";
                    return new JobResult
                    {
                        Result = OrchestratorJobStatusJobResult.Warning,
                        JobHistoryId = config.JobHistoryId,
                        FailureMessage =
                            $"{inventoryItems.Count} cert(s) inventoried, but {unresolvedCerts.Count} could not be retrieved/parsed and were skipped: {sample}{suffix}. Check the orchestrator log for the per-cert error (a common cause is a .p12 that the appliance's ViewCertificateDetail response can't be parsed as a bare X.509 cert)."
                    };
                }

                return new JobResult
                {
                    Result = OrchestratorJobStatusJobResult.Success,
                    JobHistoryId = config.JobHistoryId,
                    FailureMessage = ""
                };
            }
            catch (Exception e)
            {
                _logger.LogError($"Error in CertManager.GetCerts {LogHandler.FlattenException(e)}");
                throw;
            }
        }
    }
}