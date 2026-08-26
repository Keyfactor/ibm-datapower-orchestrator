using System;
using Keyfactor.Extensions.Orchestrator.DataPower;
using Keyfactor.Orchestrators.Extensions;

namespace DataPower.Tests
{
    public class UtilityTests
    {
        private static ManagementJobConfiguration ManagementConfig(string storePath, string properties = "{}")
        {
            return new ManagementJobConfiguration
            {
                CertificateStoreDetails = new CertificateStore
                {
                    StorePath = storePath,
                    Properties = properties,
                    ClientMachine = "dp.example.com:5554"
                }
            };
        }

        private static InventoryJobConfiguration InventoryConfig(string storePath, string properties = "{}")
        {
            return new InventoryJobConfiguration
            {
                CertificateStoreDetails = new CertificateStore
                {
                    StorePath = storePath,
                    Properties = properties,
                    ClientMachine = "dp.example.com:5554"
                }
            };
        }

        [Theory]
        [InlineData(@"default\sharedcert", "default", "sharedcert")]
        [InlineData(@"test-domain-01\cert", "test-domain-01", "cert")]
        [InlineData(@"default\pubcert", "default", "pubcert")]
        public void ParseCertificateConfig_Management_SplitsBackslashPath(string storePath, string expectedDomain,
            string expectedStore)
        {
            var ci = Utility.ParseCertificateConfig(ManagementConfig(storePath));

            Assert.NotNull(ci);
            Assert.Equal(expectedDomain, ci.Domain);
            Assert.Equal(expectedStore, ci.CertificateStore);
        }

        [Fact]
        public void ParseCertificateConfig_Management_SplitsForwardSlashPath()
        {
            var ci = Utility.ParseCertificateConfig(ManagementConfig("default/sharedcert"));

            Assert.NotNull(ci);
            Assert.Equal("default", ci.Domain);
            Assert.Equal("sharedcert", ci.CertificateStore);
        }

        [Fact]
        public void ParseCertificateConfig_Management_NoDelimiter_LeavesCertificateStoreAsWholePath()
        {
            var ci = Utility.ParseCertificateConfig(ManagementConfig("sharedcert"));

            Assert.NotNull(ci);
            Assert.Equal("sharedcert", ci.CertificateStore);
        }

        [Fact]
        public void ParseCertificateConfig_Management_ReturnsNullOnNullProperties()
        {
            var config = ManagementConfig(@"default\sharedcert", "null");
            var ci = Utility.ParseCertificateConfig(config);

            Assert.Null(ci);
        }

        [Fact]
        public void ParseCertificateConfig_Management_AppliesDefaultsForMissingProperties()
        {
            var ci = Utility.ParseCertificateConfig(ManagementConfig(@"default\sharedcert", "{}"));

            Assert.NotNull(ci);
            Assert.Equal(100, ci.InventoryPageSize);
            Assert.Equal("pubcert", ci.PublicCertStoreName);
            Assert.Equal("https", ci.Protocol);
        }

        [Theory]
        [InlineData(@"test-domain-01\sharedcert", "test-domain-01", "sharedcert")]
        [InlineData(@"default\pubcert", "default", "pubcert")]
        public void ParseCertificateConfig_Inventory_SplitsBackslashPath(string storePath, string expectedDomain,
            string expectedStore)
        {
            var ci = Utility.ParseCertificateConfig(InventoryConfig(storePath));

            Assert.NotNull(ci);
            Assert.Equal(expectedDomain, ci.Domain);
            Assert.Equal(expectedStore, ci.CertificateStore);
        }

        [Fact]
        public void ParseCertificateConfig_Inventory_SplitsForwardSlashPath()
        {
            var ci = Utility.ParseCertificateConfig(InventoryConfig("default/sharedcert"));

            Assert.NotNull(ci);
            Assert.Equal("default", ci.Domain);
            Assert.Equal("sharedcert", ci.CertificateStore);
        }

        [Fact]
        public void ParseCertificateConfig_Inventory_ReturnsNullOnNullProperties()
        {
            var ci = Utility.ParseCertificateConfig(InventoryConfig(@"default\sharedcert", "null"));

            Assert.Null(ci);
        }

        [Theory]
        [InlineData(@"default\sharedcert", @"\", "default")]
        [InlineData(@"test-domain-01\cert", @"\", "test-domain-01")]
        [InlineData("default/pubcert", "/", "default")]
        public void GetDomain_ReturnsSegmentBeforeDelimiter(string source, string delimiter, string expected)
        {
            Assert.Equal(expected, Utility.GetDomain(source, delimiter));
        }

        [Theory]
        [InlineData(@"default\sharedcert", @"\", "sharedcert")]
        [InlineData(@"test-domain-01\cert", @"\", "cert")]
        [InlineData("default/pubcert", "/", "pubcert")]
        public void GetCertStore_ReturnsSegmentAfterDelimiter(string source, string delimiter, string expected)
        {
            Assert.Equal(expected, Utility.GetCertStore(source, delimiter));
        }

        [Fact]
        public void ReplaceAlias_ReplacesFirstMatch()
        {
            var result = Utility.ReplaceAlias("prefix-mycert", "prefix-", "");
            Assert.Equal("mycert", result);
        }

        [Fact]
        public void ReplaceAlias_ReturnsOriginal_WhenSearchNotFound()
        {
            var result = Utility.ReplaceAlias("mycert", "prefix-", "");
            Assert.Equal("mycert", result);
        }

        [Fact]
        public void ReplaceFirstOccurrence_SwapsPrefix()
        {
            var result = Utility.ReplaceFirstOccurrence("cert-mycert", "cert-", "key-");
            Assert.Equal("key-mycert", result);
        }

        [Fact]
        public void ParseStoreProperties_DeserializesNamePrefixFields()
        {
            var config = ManagementConfig(@"default\sharedcert",
                "{\"CryptoCertObjectPrefix\":\"cc-\",\"CryptoKeyObjectPrefix\":\"ck-\"}");

            var np = Utility.ParseStoreProperties(config);

            Assert.Equal("cc-", np.CryptoCertObjectPrefix);
            Assert.Equal("ck-", np.CryptoKeyObjectPrefix);
        }

        [Fact]
        public void GetPemFromResponse_ReturnsEmpty_ForGarbageBytes()
        {
            var result = Utility.GetPemFromResponse(new byte[] { 1, 2, 3, 4, 5 });
            Assert.Equal(string.Empty, result);
        }

        [Fact]
        public void GetPemFromResponse_ValidDerBytes_ReturnsPem()
        {
            // Raw DER bytes of a real self-signed cert - exercises the DERToPEM success
            // path rather than always falling through to the UTF8-fallback/empty branch.
            var der = Convert.FromBase64String(
                "MIICqDCCAZCgAwIBAgIIQZCCl/4po1swDQYJKoZIhvcNAQELBQAwFDESMBAGA1UE" +
                "AxMJdGVzdC1jZXJ0MB4XDTI2MDgyNjE0MDczNVoXDTI3MDgyNjE0MTIzNlowFDES" +
                "MBAGA1UEAxMJdGVzdC1jZXJ0MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKC" +
                "AQEA706LkJYH4bsTUBvKrOB7dQcvh7oIA0vfkwYYr1q968wOV1y13JT/dwBqGx4V" +
                "ZXE0CNuanDauXN1atafX2wSYj5Yd3RbN1tFcW/E+IFnnp9dIlQGXrHYoFpUjQ9D8" +
                "cc3eOewLcQwmUwBXyCqgvMF13W326W8ywQxOtZFEl50AIca4s1IRyg6AEEfSaHAs" +
                "5eBnGSTK3PAgOsV1Jag298caZYvIhHj8HcdsHhky+yuhXUFBYCBn8th1TC+FbvXA" +
                "FfX40CUCIVZWk+Rq3C9IYhdfGeYMiIe2yIOJYNkajDcceVxnLxA31OylbVybAvQc" +
                "aqEj8XDO+YraIjqXy8zLnVKYEQIDAQABMA0GCSqGSIb3DQEBCwUAA4IBAQBlC6WX" +
                "iEI/tO6VAZKPi42JnZ1gJbTGmbuBtxG3G+4V8EBNGRsT9t1Xw+dEOTmfFJEzXeNx" +
                "m4sMvQZw7oTP1FWJznmPghalcTvxDClx4Mg0uRsLOt7AwzCPH3ml39WyrsxcM8rQ" +
                "2s0lSgs72dXOgnqzZlpHOScOHSzwSWaLhDznFZDOGfeRhFRruI5qOoUbWlJhiPSX" +
                "AsOo5vatHKYF1s5G8lXS8Ik3qPbrQ8oYJngyl7SMVZagUHEuVJQQzq0kO4snBIBb" +
                "gLyNKcmK/hbhDlutpubjfs6Dwg2ZBfniTendJqNqXnzcdFgj6E9JxxwWu/F4qPtm" +
                "LgZ7MkO7qo7h9wD/");

            var result = Utility.GetPemFromResponse(der);

            Assert.Contains("BEGIN CERTIFICATE", result);
        }

        [Fact]
        public void ReplaceAlias_NullText_Throws()
        {
            Assert.ThrowsAny<Exception>(() => Utility.ReplaceAlias(null!, "x", "y"));
        }

        [Fact]
        public void ParseStoreProperties_MalformedJson_Throws()
        {
            var config = ManagementConfig(@"default\sharedcert", "{not valid json");
            Assert.ThrowsAny<Exception>(() => Utility.ParseStoreProperties(config));
        }

        [Fact]
        public void ParseCertificateConfig_Management_MalformedJson_Throws()
        {
            var config = ManagementConfig(@"default\sharedcert", "{not valid json");
            Assert.ThrowsAny<Exception>(() => Utility.ParseCertificateConfig(config));
        }

        [Fact]
        public void ParseCertificateConfig_Inventory_MalformedJson_Throws()
        {
            var config = InventoryConfig(@"default\sharedcert", "{not valid json");
            Assert.ThrowsAny<Exception>(() => Utility.ParseCertificateConfig(config));
        }

        [Fact]
        public void GetDomain_DelimiterNotFound_Throws()
        {
            // IndexOf returns -1, Substring(0, -1) throws ArgumentOutOfRangeException.
            Assert.ThrowsAny<Exception>(() => Utility.GetDomain("nodelimiterhere", @"\"));
        }

        [Fact]
        public void GetCertStore_DelimiterNotFound_ReturnsWholeTrimmedSource()
        {
            // Unlike GetDomain, a missing delimiter here doesn't throw: IndexOf(-1) + 1
            // resolves to start=0, so it just returns the whole (trimmed) source string.
            Assert.Equal("nodelimiterhere", Utility.GetCertStore("nodelimiterhere", @"\"));
        }

        [Fact]
        public void GetCertStore_NullSource_Throws()
        {
            Assert.ThrowsAny<Exception>(() => Utility.GetCertStore(null!, @"\"));
        }

        [Fact]
        public void ReplaceFirstOccurrence_FindNotPresent_Throws()
        {
            // IndexOf returns -1, source.Remove(-1, ...) throws ArgumentOutOfRangeException.
            Assert.ThrowsAny<Exception>(() => Utility.ReplaceFirstOccurrence("mycert", "notfound", "x"));
        }
    }
}
