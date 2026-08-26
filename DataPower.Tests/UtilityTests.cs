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
    }
}
