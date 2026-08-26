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
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Keyfactor.Extensions.Orchestrator.DataPower.Models.Requests;
using Keyfactor.Extensions.Orchestrator.DataPower.Models.Responses;
using Keyfactor.Extensions.Orchestrator.DataPower.Models.SupportingObjects;
using Keyfactor.Logging;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Keyfactor.Extensions.Orchestrator.DataPower.Client
{
    // This is the real network boundary - every method makes an actual HTTPS call
    // to the DataPower REST Management Interface via HttpWebRequest, which isn't
    // mockable without introducing an HTTP transport seam of its own. RequestManager
    // and the Job classes are unit-tested against IDataPowerClient instead (see
    // DataPower.Tests); this class's actual request/response handling is exercised
    // by the Postman collection in test/ against a live/lab appliance.
    [ExcludeFromCodeCoverage]
    public class DataPowerClient : IDataPowerClient
    {
        private readonly ILogger<DataPowerClient> _logger;

        public DataPowerClient(ILogger<DataPowerClient> logger)
        {
            _logger = logger;
        }

        public DataPowerClient(string url, string email, string key)
        {
            HttpClient = new HttpClient();
            HttpClient.DefaultRequestHeaders.Add("X-Auth-Email", email);
            HttpClient.DefaultRequestHeaders.Add("X-Auth-Key", key);
            var loggerFactory = (ILoggerFactory) new LoggerFactory();
            var dpLogger = loggerFactory.CreateLogger<DataPowerClient>();
            _logger = dpLogger;
            HttpClient.BaseAddress = new Uri(url);
        }


        #region Constructors

        public DataPowerClient(string user, string pass, string baseUrl, string domain)
        {
            var loggerFactory = (ILoggerFactory) new LoggerFactory();
            var dpLogger = loggerFactory.CreateLogger<DataPowerClient>();
            _logger = dpLogger;
            BaseUrl = baseUrl;
            Username = user;
            Password = pass;
            Domain = domain.Trim();
        }

        #endregion

        private HttpClient HttpClient { get; }

        //public string AuthenticationSignature { get; set; }
        public string BaseUrl { get; set; }
        public string Domain { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }

        #region Class Methods

        public List<DomainInfo> ListDomains()
        {
            try
            {
                var request = new ListDomainsRequest();
                var strResponse = ApiRequestString("ListDomains", request.GetResource(), request.Method,
                    string.Empty, false, true);

                var containerName = "domain";

                // DataPower returns a single object instead of array when only one domain exists
                if (strResponse.Contains($"\"{containerName}\"") &&
                    !strResponse.Contains($"\"{containerName}\" : [") &&
                    !strResponse.Contains($"\"{containerName}\":["))
                {
                    // Wrap single domain object in array for proper deserialization
                    var singleResponse = JsonConvert.DeserializeObject<ListDomainsSingleResponse>(strResponse);
                    return singleResponse?.Domain != null
                        ? new List<DomainInfo> { singleResponse.Domain }
                        : new List<DomainInfo>();
                }

                var response = JsonConvert.DeserializeObject<ListDomainsResponse>(strResponse);
                return response?.Domains?.ToList() ?? new List<DomainInfo>();
            }
            catch (Exception e)
            {
                _logger.LogError($"Error In DataPowerClient.ListDomains: {LogHandler.FlattenException(e)}");
                throw;
            }
        }

        public List<string> ListFileStoreDirectories(string domain)
        {
            try
            {
                var request = new ListFileStoreRequest(domain);
                var strResponse = ApiRequestString("ListFileStoreDirectories", request.GetResource(), request.Method,
                    string.Empty, false, true);

                var containerName = "location";

                // DataPower returns a single object instead of an array when only one location exists
                if (strResponse.Contains($"\"{containerName}\"") &&
                    !strResponse.Contains($"\"{containerName}\" : [") &&
                    !strResponse.Contains($"\"{containerName}\":["))
                {
                    strResponse = FixDataPowerBadJson(strResponse, containerName);
                }

                var response = JsonConvert.DeserializeObject<ListFileStoreResponse>(strResponse);
                if (response?.FileStore?.Locations == null)
                    return new List<string>();

                return response.FileStore.Locations
                    .Select(d => d.Name)
                    .ToList();
            }
            catch (Exception e)
            {
                _logger.LogError($"Error In DataPowerClient.ListFileStoreDirectories: {LogHandler.FlattenException(e)}");
                throw;
            }
        }

        public bool SaveConfig()
        {
            try
            {
                var saveConfig = new SaveConfigRequest(Domain);
                var strRequest = JsonConvert.SerializeObject(saveConfig);
                var strResponse = ApiRequestString("SaveConfig", saveConfig.GetResource(), saveConfig.Method,
                    strRequest, false, true);
                JsonConvert.DeserializeObject<SaveConfigResponse>(strResponse);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error Saving the Config: {LogHandler.FlattenException(ex)}");
                throw;
            }
        }

        public bool AddCertificateFile(CertificateAddRequest certAddRequest)
        {
            try
            {
                var strRequest = JsonConvert.SerializeObject(certAddRequest);
                var strResponse = ApiRequestString("CertFileAddRequest", certAddRequest.GetResource(),
                    certAddRequest.Method, strRequest, false, true);
                JsonConvert.DeserializeObject<CertificateAddResponse>(strResponse);
                return true;
            }
            catch (Exception e)
            {
                _logger.LogError($"Error In DataPowerClient.AddCertificateFile: {LogHandler.FlattenException(e)}");
                throw;
            }
        }

        public bool AddCryptoCertificate(CryptoCertificateAddRequest cryptoCertAddRequest)
        {
            try
            {
                var strRequest = JsonConvert.SerializeObject(cryptoCertAddRequest);
                ApiRequestString("CryptoCertAddRequest", cryptoCertAddRequest.GetResource(),
                    cryptoCertAddRequest.Method,
                    strRequest, false, true);
                return true;
            }
            catch (Exception e)
            {
                _logger.LogError($"Error In DataPowerClient.AddCryptoCertificate: {LogHandler.FlattenException(e)}");
                throw;
            }
        }

        public bool UpdateCryptoCertificate(CryptoCertificateUpdateRequest cryptoCertUpdateRequest)
        {
            try
            {
                var strRequest = JsonConvert.SerializeObject(cryptoCertUpdateRequest);
                ApiRequestString("CryptoCertUpdateRequest", cryptoCertUpdateRequest.GetResource(),
                    cryptoCertUpdateRequest.Method, strRequest, false, true);
                return true;
            }
            catch (Exception e)
            {
                _logger.LogError($"Error In DataPowerClient.UpdateCryptoCertificate: {LogHandler.FlattenException(e)}");
                throw;
            }
        }

        public bool AddCryptoKey(CryptoKeyAddRequest cryptoKeyAddRequest)
        {
            try
            {
                var strRequest = JsonConvert.SerializeObject(cryptoKeyAddRequest);
                ApiRequestString("CryptoKeyAddRequest", cryptoKeyAddRequest.GetResource(), cryptoKeyAddRequest.Method,
                    strRequest, false, true);
                return true;
            }
            catch (Exception e)
            {
                _logger.LogError($"Error In DataPowerClient.AddCryptoKey: {LogHandler.FlattenException(e)}");
                throw;
            }
        }

        public bool UpdateCryptoKey(CryptoKeyUpdateRequest cryptoKeyUpdateRequest)
        {
            try
            {
                var strRequest = JsonConvert.SerializeObject(cryptoKeyUpdateRequest);
                ApiRequestString("CryptoKeyAddRequest", cryptoKeyUpdateRequest.GetResource(),
                    cryptoKeyUpdateRequest.Method,
                    strRequest, false, true);
                return true;
            }
            catch (Exception e)
            {
                _logger.LogError($"Error In DataPowerClient.UpdateCryptoKey: {LogHandler.FlattenException(e)}");
                throw;
            }
        }

        public ViewCryptoCertificatesResponse ViewCertificates(ViewCryptoCertificatesRequest viewCertificates)
        {
            try
            {
                var strRequest = JsonConvert.SerializeObject(viewCertificates);
                var strResponse = ApiRequestString("ViewCertificates", viewCertificates.GetResource(),
                    viewCertificates.Method, strRequest, false, true);

                var viewCertificatesResponse = new ViewCryptoCertificatesResponse();

                if (strResponse.Contains("No configuration retrieved")) return viewCertificatesResponse;
                var responseCounter = strResponse;
                var strCheck = "PasswordAlias";
                var respCount = (responseCounter.Length - responseCounter.Replace(strCheck, "").Length) /
                                strCheck.Length;

                if (respCount == 1)
                {
                    var viewSingleCertificateResponse =
                        JsonConvert.DeserializeObject<ViewCryptoCertificateSingleResponse>(strResponse);
                    viewCertificatesResponse.CryptoCertificates = new CryptoCertificate[1];
                    viewCertificatesResponse.CryptoCertificates[0] = viewSingleCertificateResponse.CryptoCertificate;
                }
                else
                {
                    viewCertificatesResponse =
                        JsonConvert.DeserializeObject<ViewCryptoCertificatesResponse>(strResponse);
                }

                return viewCertificatesResponse;
            }
            catch (Exception e)
            {
                _logger.LogError($"Error In DataPowerClient.ViewCertificates: {LogHandler.FlattenException(e)}");
                throw;
            }
        }

        public ViewCertificateDetailResponse ViewCryptoCertificate(ViewCertificateDetailRequest viewCertificate)
        {
            try
            {
                var strRequest = JsonConvert.SerializeObject(viewCertificate);
                var strResponse = ApiRequestString("ViewCertificateDetail", viewCertificate.GetResource(),
                    viewCertificate.Method, strRequest, false, true);
                var viewCertificateDetailResponse =
                    JsonConvert.DeserializeObject<ViewCertificateDetailResponse>(strResponse);
                return viewCertificateDetailResponse;
            }
            catch (Exception e)
            {
                _logger.LogError($"Error In DataPowerClient.ViewCryptoCertificate: {LogHandler.FlattenException(e)}");
                throw;
            }
        }

        public ViewPublicCertificatesResponse ViewPublicCertificates(ViewPublicCertificatesRequest viewPubCertificates)
        {
            try
            {
                var strRequest = JsonConvert.SerializeObject(viewPubCertificates);

                var strResponse = ApiRequestString("ViewPublicCertificates", viewPubCertificates.GetResource(),
                    viewPubCertificates.Method, strRequest, false, true);

                var containerName = "file";

                //Datapower API does not return single item arrays correctly (missing brackets) need to add them in to deseralize properly
                if (strResponse.Contains($"\"{containerName}\" :") && !strResponse.Contains($"\"{containerName}\" : ["))
                    strResponse = FixDataPowerBadJson(strResponse, containerName);

                var viewPubCertificatesResponse =
                    JsonConvert.DeserializeObject<ViewPublicCertificatesResponse>(strResponse);
                return viewPubCertificatesResponse;
            }
            catch (Exception e)
            {
                _logger.LogError($"Error In DataPowerClient.ViewPublicCertificates: {LogHandler.FlattenException(e)}");
                throw;
            }
        }

        public ViewPubCertificateDetailResponse ViewPublicCertificate(
            ViewPubCertificateDetailRequest viewPubCertificate)
        {
            try
            {
                var strRequest = JsonConvert.SerializeObject(viewPubCertificate);
                var strResponse = ApiRequestString("ViewPublicCertificateDetail", viewPubCertificate.GetResource(),
                    viewPubCertificate.Method, strRequest, false, true);
                var viewPubCertificateDetailResponse =
                    JsonConvert.DeserializeObject<ViewPubCertificateDetailResponse>(strResponse);
                return viewPubCertificateDetailResponse;
            }
            catch (Exception e)
            {
                _logger.LogError($"Error In DataPowerClient.ViewPublicCertificate: {LogHandler.FlattenException(e)}");
                throw;
            }
        }

        //DeleteCryptoKeyRequest
        public void DeleteCryptoKey(DeleteCryptoKeyRequest request)
        {
            try
            {
                var strRequest = JsonConvert.SerializeObject(request);
                ApiRequestString("DeleteCryptoKey", request.GetResource(), request.Method, strRequest, false, true);
            }
            catch (Exception e)
            {
                _logger.LogError($"Error In DataPowerClient.DeleteCryptoKey: {LogHandler.FlattenException(e)}");
                throw;
            }
        }

        //DeleteCryptoCertificateRequest
        public void DeleteCryptoCertificate(DeleteCryptoCertificateRequest request)
        {
            try
            {
                var strRequest = JsonConvert.SerializeObject(request);
                ApiRequestString("DeleteCryptoCertificate", request.GetResource(), request.Method, strRequest, false,
                    true);
            }
            catch (Exception e)
            {
                _logger.LogError($"Error In DataPowerClient.DeleteCryptoCertificate: {LogHandler.FlattenException(e)}");
                throw;
            }
        }

        //DeleteCertificateRequest
        public void DeleteCertificate(DeleteCertificateRequest request)
        {
            try
            {
                var strRequest = JsonConvert.SerializeObject(request);
                ApiRequestString("DeleteCertificate", request.GetResource(), request.Method, strRequest, false, true);
            }
            catch (Exception e)
            {
                _logger.LogError($"Error In DataPowerClient.DeleteCertificate: {LogHandler.FlattenException(e)}");
                throw;
            }
        }

        //ViewCryptoKeyRequest
        public ViewCryptoKeysResponse ViewCryptoKeys(ViewCryptoKeysRequest request)
        {
            try
            {
                var strRequest = JsonConvert.SerializeObject(request);
                var strResponse = ApiRequestString("ViewCryptoKey", request.GetResource(), request.Method, strRequest,
                    false, true);
                var response = new ViewCryptoKeysResponse();

                var containerName = "CryptoKey";

                //Datapower API does not return single item arrays correctly (missing brackets) need to add them in to deseralize properly
                if (strResponse.Contains($"\"{containerName}\" :") && !strResponse.Contains($"\"{containerName}\" : ["))
                    strResponse = FixDataPowerBadJson(strResponse, containerName);

                if (!strResponse.Contains("error"))
                    response = JsonConvert.DeserializeObject<ViewCryptoKeysResponse>(strResponse);

                return response;
            }
            catch (Exception e)
            {
                _logger.LogError($"Error In DataPowerClient.ViewCryptoKeys: {LogHandler.FlattenException(e)}");
                throw;
            }
        }


        private string FixDataPowerBadJson(string json, string containerName)
        {
            try
            {
                json = json.Replace($"\"{containerName}\" :", $"\"{containerName}\" : [");

                if (containerName == "CryptoKey")
                {
                    var lastIndex = json.LastIndexOf("}", StringComparison.Ordinal);
                    var secondLastIndex =
                        lastIndex > 0 ? json.LastIndexOf("}", lastIndex - 1, StringComparison.Ordinal) : -1;
                    if (secondLastIndex >= 0) json = json.Remove(secondLastIndex).Insert(secondLastIndex, "}]}");
                }
                else
                {
                    var lastIndex = json.LastIndexOf(",", StringComparison.Ordinal);
                    json = json.Remove(lastIndex, 1).Insert(lastIndex, "],");
                }

                return json;
            }
            catch (Exception e)
            {
                _logger.LogError($"Error In DataPowerClient.FixDataPowerBadJson: {LogHandler.FlattenException(e)}");
                throw;
            }
        }

        //ViewCryptoCertificatesRequest
        public ViewCryptoCertificateSingleResponse ViewCryptoCertificate(ViewCryptoCertificatesRequest request)
        {
            try
            {
                var strRequest = JsonConvert.SerializeObject(request);
                var strResponse = ApiRequestString("ViewCryptoCertificate", request.GetResource(), request.Method,
                    strRequest, false, true);
                var response = new ViewCryptoCertificateSingleResponse();
                if (!strResponse.Contains("error"))
                    response = JsonConvert.DeserializeObject<ViewCryptoCertificateSingleResponse>(strResponse);

                return response;
            }
            catch (Exception e)
            {
                _logger.LogError($"Error In DataPowerClient.ViewCryptoCertificate: {LogHandler.FlattenException(e)}");
                throw;
            }
        }

        public string ApiRequestString(string strCall, string strPostUrl, string strMethod, string strQueryString,
            bool bWrite, bool bUseToken)
        {
            _logger.LogTrace($"BEGIN API Request: {strCall}");
            _logger.LogTrace($"BaseUrl: {BaseUrl}");
            _logger.LogTrace($"url: {strPostUrl}");
            _logger.LogTrace($"strMethod: {strMethod}");
            // Mask sensitive payload fields (cert/key material, passwords) before logging
            _logger.LogTrace($"strQueryString: {MaskSensitivePayload(strQueryString)}");

            HttpWebResponse objResponse = null;
            try
            {
                ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };

                var objRequest = (HttpWebRequest) WebRequest.Create(BaseUrl + strPostUrl);
                objRequest.Method = strMethod;
                objRequest.ContentType = "application/json";
                var encoded =
                    Convert.ToBase64String(Encoding.GetEncoding("ISO-8859-1").GetBytes(Username + ":" + Password));
                objRequest.Headers.Add("Authorization", "Basic " + encoded);

                if (!string.IsNullOrEmpty(strQueryString) &&
                    (objRequest.Method == "POST" || objRequest.Method == "PUT"))
                {
                    var postBytes = Encoding.UTF8.GetBytes(strQueryString);
                    objRequest.ContentLength = postBytes.Length;

                    using (var requestStream = objRequest.GetRequestStream())
                    {
                        requestStream.Write(postBytes, 0, postBytes.Length);
                    }
                }

                objResponse = (HttpWebResponse) objRequest.GetResponse();
                using (var stream = objResponse.GetResponseStream() ?? throw new InvalidOperationException("Response stream is null."))
                using (var reader = new StreamReader(stream))
                {
                    var strResponse = reader.ReadToEnd();
                    _logger.LogTrace($"strResponse: {strResponse}");
                    _logger.LogTrace("END APIRequestString");
                    return strResponse;
                }
            }
            catch (WebException webEx)
            {
                // Extract status code and response body from the failed response so the
                // typed DataPowerApiException can carry both back to the operator.
                var statusCode = HttpStatusCode.InternalServerError;
                var responseBody = string.Empty;

                if (webEx.Response is HttpWebResponse errorResponse)
                {
                    statusCode = errorResponse.StatusCode;
                    try
                    {
                        using (var stream = errorResponse.GetResponseStream())
                        {
                            if (stream != null)
                            {
                                using (var reader = new StreamReader(stream))
                                {
                                    responseBody = reader.ReadToEnd();
                                }
                            }
                        }
                    }
                    catch (Exception readEx)
                    {
                        _logger.LogWarning(readEx, "Failed to read error response body for {Operation}.", strCall);
                    }
                    finally
                    {
                        errorResponse.Dispose();
                    }
                }

                _logger.LogError(webEx,
                    "END APIRequestString error for {Operation}: HTTP {Status} {StatusName}, body: {Body}",
                    strCall, (int)statusCode, statusCode, responseBody);
                throw new DataPowerApiException(
                    $"DataPower API call '{strCall}' failed with HTTP {(int)statusCode} {statusCode}. Body: {responseBody}",
                    statusCode, strCall, responseBody, webEx);
            }
            catch (DataPowerApiException)
            {
                // Already typed - just rethrow
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "END APIRequestString error: {ErrorMessage}", LogHandler.FlattenException(ex));
                throw;
            }
            finally
            {
                objResponse?.Dispose();
            }
        }

        /// <summary>
        /// Masks sensitive fields (cert content, private keys, passwords) in a JSON payload
        /// before it goes into trace logs. Only the value portion is replaced - keys remain
        /// readable so operators can see the request shape.
        /// </summary>
        private static string MaskSensitivePayload(string payload)
        {
            if (string.IsNullOrEmpty(payload)) return payload;

            // Quick check: if no sensitive markers exist, skip the regex work
            if (payload.IndexOf("content", StringComparison.OrdinalIgnoreCase) < 0
                && payload.IndexOf("Password", StringComparison.OrdinalIgnoreCase) < 0
                && payload.IndexOf("BEGIN", StringComparison.Ordinal) < 0)
            {
                return payload;
            }

            // Replace string values for known-sensitive keys: "content", "Password", "PasswordAlias"
            var masked = System.Text.RegularExpressions.Regex.Replace(
                payload,
                "\"(content|Password|PasswordAlias)\"\\s*:\\s*\"[^\"]*\"",
                "\"$1\":\"***MASKED***\"",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            return masked;
        }

        #endregion
    }
}