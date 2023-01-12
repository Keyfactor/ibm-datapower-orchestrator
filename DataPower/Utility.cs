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
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Keyfactor.Extensions.Orchestrator.DataPower.Models.SupportingObjects;
using Keyfactor.Logging;
using Keyfactor.Orchestrators.Extensions;
using Keyfactor.PKI.PEM;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Keyfactor.Extensions.Orchestrator.DataPower
{
    internal static class Utility
    {
        public static Func<string, string> Pemify = ss =>
            ss.Length <= 64 ? ss : ss.Substring(0, 64) + "\n" + Pemify(ss.Substring(64));

        private static ILogger GetLogger()
        {
            ILoggerFactory loggerFactory = new LoggerFactory();
            var logger = loggerFactory.CreateLogger(typeof(Utility));
            return logger;
        }

        public static string ReplaceAlias(string text, string search, string replace)
        {
            var logger = GetLogger();
            logger.MethodEntry();
            try
            {
                var pos = text.IndexOf(search, StringComparison.Ordinal);
                logger.LogTrace($"Position is {pos}");
                var returnVal = pos < 0 ? text : text.Substring(0, pos) + replace + text.Substring(pos + search.Length);
                logger.LogTrace($"Return Value is {returnVal}");
                logger.MethodExit();
                return returnVal;
            }
            catch (Exception e)
            {
                logger.LogError($"Error In Utility.ReplaceAlias: {LogHandler.FlattenException(e)}");
                throw;
            }
        }

        public static NamePrefix ParseStoreProperties(ManagementJobConfiguration config)
        {
            var logger = GetLogger();
            logger.MethodEntry();
            try
            {
                return JsonConvert.DeserializeObject<NamePrefix>(config.CertificateStoreDetails.Properties);
            }
            catch (Exception e)
            {
                logger.LogError($"Error In Utility.ReplaceAlias: {LogHandler.FlattenException(e)}");
                throw;
            }
        }

        public static CertStoreInfo ParseCertificateConfig(ManagementJobConfiguration config)
        {
            var logger = GetLogger();
            logger.MethodEntry();
            try
            {
                var ci = JsonConvert.DeserializeObject<CertStoreInfo>(config.CertificateStoreDetails.Properties,
                    new JsonSerializerSettings {DefaultValueHandling = DefaultValueHandling.Populate});
                if (ci == null) return null;

                ci.CertificateStore = config.CertificateStoreDetails.StorePath;
                if (config.CertificateStoreDetails.StorePath.Contains(@"\"))
                {
                    ci.Domain = GetDomain(config.CertificateStoreDetails.StorePath, @"\");
                    ci.CertificateStore = GetCertStore(config.CertificateStoreDetails.StorePath, @"\");
                }
                else if (config.CertificateStoreDetails.StorePath.Contains(@"/"))
                {
                    ci.Domain = GetDomain(config.CertificateStoreDetails.StorePath, @"/");
                    ci.CertificateStore = GetCertStore(config.CertificateStoreDetails.StorePath, @"/");
                }

                return ci;
            }
            catch (Exception e)
            {
                logger.LogError($"Error In Utility.ParseCertificateConfig: {LogHandler.FlattenException(e)}");
                throw;
            }
        }

        public static CertStoreInfo ParseCertificateConfig(InventoryJobConfiguration config)
        {
            var logger = GetLogger();
            logger.MethodEntry();
            try
            {
                var ci = JsonConvert.DeserializeObject<CertStoreInfo>(config.CertificateStoreDetails.Properties,
                    new JsonSerializerSettings {DefaultValueHandling = DefaultValueHandling.Populate});
                if (ci == null) return null;

                ci.CertificateStore = config.CertificateStoreDetails.StorePath;

                if (config.CertificateStoreDetails.StorePath.Contains(@"\"))
                {
                    ci.Domain = GetDomain(config.CertificateStoreDetails.StorePath, @"\");
                    ci.CertificateStore = GetCertStore(config.CertificateStoreDetails.StorePath, @"\");
                }
                else if (config.CertificateStoreDetails.StorePath.Contains(@"/"))
                {
                    ci.Domain = GetDomain(config.CertificateStoreDetails.StorePath, @"/");
                    ci.CertificateStore = GetCertStore(config.CertificateStoreDetails.StorePath, @"/");
                }

                return ci;
            }
            catch (Exception e)
            {
                logger.LogError($"Error In Utility.ParseCertificateConfig: {LogHandler.FlattenException(e)}");
                throw;
            }
        }

        public static string GetDomain(string strSource, string delimiter)
        {
            var logger = GetLogger();
            logger.MethodEntry();
            try
            {
                var start = strSource.IndexOf(delimiter, 0, StringComparison.Ordinal);
                return strSource.Substring(0, start).Trim();
            }
            catch (Exception e)
            {
                logger.LogError($"Error In Utility.GetDomain: {LogHandler.FlattenException(e)}");
                throw;
            }
        }

        public static string GetCertStore(string strSource, string delimiter)
        {
            var logger = GetLogger();
            logger.MethodEntry();
            try
            {
                var start = strSource.IndexOf(delimiter, 0, StringComparison.Ordinal) + 1;
                var end = strSource.Length;
                return strSource.Substring(start, end - start).Trim();
            }
            catch (Exception e)
            {
                logger.LogError($"Error In Utility.GetDomain: {LogHandler.FlattenException(e)}");
                throw;
            }
        }

        public static string ReplaceFirstOccurrence(string source, string find, string replace)
        {
            var logger = GetLogger();
            logger.MethodEntry();
            try
            {
                var place = source.IndexOf(find, StringComparison.Ordinal);
                var result = source.Remove(place, find.Length).Insert(place, replace);
                return result;
            }
            catch (Exception e)
            {
                logger.LogError($"Error In Utility.GetDomain: {LogHandler.FlattenException(e)}");
                throw;
            }
        }

        public static string GetPemFromResponse(byte[] pem)
        {

            string pemString;
            try
            {
                pemString = PemUtilities.DERToPEM(pem, PemUtilities.PemObjectType.Certificate);
                var ba = Encoding.ASCII.GetBytes(pemString);
                var _ = new X509Certificate2(ba);
            }
            catch (Exception)
            {
                pemString = String.Empty;
            }

            if (pemString.Length == 0)
            {
                try
                {
                    pemString = Encoding.UTF8.GetString(pem);
                    var ba = Encoding.ASCII.GetBytes(pemString);
                    var _ = new X509Certificate2(ba);
                }
                catch (Exception)
                {
                    pemString = String.Empty;
                }
            }

            return pemString;
        }
    }
}