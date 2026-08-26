using System.Collections.Generic;
using DataPower.Tests.TestSupport;
using Keyfactor.Extensions.Orchestrator.DataPower;
using Keyfactor.Extensions.Orchestrator.DataPower.Client;
using Keyfactor.Extensions.Orchestrator.DataPower.Models.Requests;
using Keyfactor.Extensions.Orchestrator.DataPower.Models.Responses;
using Keyfactor.Extensions.Orchestrator.DataPower.Models.SupportingObjects;
using Keyfactor.Orchestrators.Common.Enums;
using Keyfactor.Orchestrators.Extensions;
using Moq;

namespace DataPower.Tests
{
    public class RequestManagerGetCertsTests
    {
        private static RequestManager NewRequestManager() =>
            new RequestManager(new FakePamResolver());

        private static Mock<IDataPowerClient> NewMockClient(string domain = "default")
        {
            var mock = new Mock<IDataPowerClient>();
            mock.SetupGet(c => c.Domain).Returns(domain);
            return mock;
        }

        private static CryptoCertificate Cert(string name, string filename) =>
            new CryptoCertificate { Name = name, CertFile = filename };

        private static ViewCertificateDetailResponse DetailResponse(string base64Pem) =>
            new ViewCertificateDetailResponse
            {
                CryptoCertObject = new CryptoCert
                {
                    CertDetailsObject = new CertificateDetailsObject
                    {
                        EncodedCert = new CertDetailValue { Value = base64Pem }
                    }
                }
            };

        // A real self-signed cert, PEM-encoded, generated purely for test fixtures
        // (openssl/PowerShell CertificateRequest.CreateSelfSigned - not a production key).
        private const string ValidCertPem =
            "-----BEGIN CERTIFICATE-----\n" +
            "MIICqDCCAZCgAwIBAgIIQZCCl/4po1swDQYJKoZIhvcNAQELBQAwFDESMBAGA1UE\n" +
            "AxMJdGVzdC1jZXJ0MB4XDTI2MDgyNjE0MDczNVoXDTI3MDgyNjE0MTIzNlowFDES\n" +
            "MBAGA1UEAxMJdGVzdC1jZXJ0MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKC\n" +
            "AQEA706LkJYH4bsTUBvKrOB7dQcvh7oIA0vfkwYYr1q968wOV1y13JT/dwBqGx4V\n" +
            "ZXE0CNuanDauXN1atafX2wSYj5Yd3RbN1tFcW/E+IFnnp9dIlQGXrHYoFpUjQ9D8\n" +
            "cc3eOewLcQwmUwBXyCqgvMF13W326W8ywQxOtZFEl50AIca4s1IRyg6AEEfSaHAs\n" +
            "5eBnGSTK3PAgOsV1Jag298caZYvIhHj8HcdsHhky+yuhXUFBYCBn8th1TC+FbvXA\n" +
            "FfX40CUCIVZWk+Rq3C9IYhdfGeYMiIe2yIOJYNkajDcceVxnLxA31OylbVybAvQc\n" +
            "aqEj8XDO+YraIjqXy8zLnVKYEQIDAQABMA0GCSqGSIb3DQEBCwUAA4IBAQBlC6WX\n" +
            "iEI/tO6VAZKPi42JnZ1gJbTGmbuBtxG3G+4V8EBNGRsT9t1Xw+dEOTmfFJEzXeNx\n" +
            "m4sMvQZw7oTP1FWJznmPghalcTvxDClx4Mg0uRsLOt7AwzCPH3ml39WyrsxcM8rQ\n" +
            "2s0lSgs72dXOgnqzZlpHOScOHSzwSWaLhDznFZDOGfeRhFRruI5qOoUbWlJhiPSX\n" +
            "AsOo5vatHKYF1s5G8lXS8Ik3qPbrQ8oYJngyl7SMVZagUHEuVJQQzq0kO4snBIBb\n" +
            "gLyNKcmK/hbhDlutpubjfs6Dwg2ZBfniTendJqNqXnzcdFgj6E9JxxwWu/F4qPtm\n" +
            "LgZ7MkO7qo7h9wD/\n" +
            "-----END CERTIFICATE-----";

        [Fact]
        public void GetCerts_FiltersByStoreScheme()
        {
            var client = NewMockClient("default");
            client.Setup(c => c.ViewCertificates(It.IsAny<ViewCryptoCertificatesRequest>()))
                .Returns(new ViewCryptoCertificatesResponse
                {
                    CryptoCertificates = new[]
                    {
                        Cert("shared-1", "sharedcert:///shared-1.pem"),
                        Cert("pub-1", "pubcert:///pub-1.pem"),
                        Cert("cert-1", "cert:///cert-1.pem")
                    }
                });

            var submitted = new List<CurrentInventoryItem>();
            client.Setup(c => c.ViewCryptoCertificate(It.IsAny<ViewCertificateDetailRequest>()))
                .Returns(new ViewCertificateDetailResponse { CryptoCertObject = null! });

            var ci = new CertStoreInfo { Domain = "default", CertificateStore = "sharedcert" };
            var config = new InventoryJobConfiguration { JobHistoryId = 1 };
            var rm = NewRequestManager();

            // Only sharedcert should be matched, and its detail fetch will throw
            // (CryptoCertObject is null) - which exercises the unresolved-cert path.
            var result = rm.GetCerts(config, client.Object, items => { submitted.AddRange(items); return true; }, ci);

            Assert.Equal(OrchestratorJobStatusJobResult.Warning, result.Result);
            Assert.Contains("shared-1", result.FailureMessage);
            Assert.Empty(submitted);
        }

        [Fact]
        public void GetCerts_ResolvedCert_IsSubmittedAsSuccess()
        {
            var client = NewMockClient("default");
            client.Setup(c => c.ViewCertificates(It.IsAny<ViewCryptoCertificatesRequest>()))
                .Returns(new ViewCryptoCertificatesResponse
                {
                    CryptoCertificates = new[] { Cert("shared-1", "sharedcert:///shared-1.pem") }
                });
            client.Setup(c => c.ViewCryptoCertificate(It.IsAny<ViewCertificateDetailRequest>()))
                .Returns(DetailResponse(ValidCertPem));

            var submitted = new List<CurrentInventoryItem>();
            var ci = new CertStoreInfo { Domain = "default", CertificateStore = "sharedcert" };
            var config = new InventoryJobConfiguration { JobHistoryId = 1 };
            var rm = NewRequestManager();

            var result = rm.GetCerts(config, client.Object, items => { submitted.AddRange(items); return true; }, ci);

            Assert.Equal(OrchestratorJobStatusJobResult.Success, result.Result);
            Assert.Single(submitted);
            Assert.Equal("shared-1", submitted[0].Alias);
            Assert.True(submitted[0].PrivateKeyEntry);
        }

        [Fact]
        public void GetCerts_BlacklistedAlias_IsExcludedFromSubmission()
        {
            var client = NewMockClient("default");
            client.Setup(c => c.ViewCertificates(It.IsAny<ViewCryptoCertificatesRequest>()))
                .Returns(new ViewCryptoCertificatesResponse
                {
                    CryptoCertificates = new[] { Cert("shared-1", "sharedcert:///shared-1.pem") }
                });
            client.Setup(c => c.ViewCryptoCertificate(It.IsAny<ViewCertificateDetailRequest>()))
                .Returns(DetailResponse(ValidCertPem));

            var submitted = new List<CurrentInventoryItem>();
            var ci = new CertStoreInfo
            {
                Domain = "default", CertificateStore = "sharedcert", InventoryBlackList = "shared-1"
            };
            var config = new InventoryJobConfiguration { JobHistoryId = 1 };
            var rm = NewRequestManager();

            var result = rm.GetCerts(config, client.Object, items => { submitted.AddRange(items); return true; }, ci);

            Assert.Equal(OrchestratorJobStatusJobResult.Success, result.Result);
            Assert.Empty(submitted);
        }

        [Fact]
        public void GetCerts_NoMatchingCerts_SubmitsEmptyAndSucceeds()
        {
            var client = NewMockClient("default");
            client.Setup(c => c.ViewCertificates(It.IsAny<ViewCryptoCertificatesRequest>()))
                .Returns(new ViewCryptoCertificatesResponse { CryptoCertificates = new[] { Cert("cert-1", "cert:///cert-1.pem") } });

            var submitted = new List<CurrentInventoryItem>();
            var ci = new CertStoreInfo { Domain = "default", CertificateStore = "sharedcert" };
            var config = new InventoryJobConfiguration { JobHistoryId = 1 };
            var rm = NewRequestManager();

            var result = rm.GetCerts(config, client.Object, items => { submitted.AddRange(items); return true; }, ci);

            Assert.Equal(OrchestratorJobStatusJobResult.Success, result.Result);
            Assert.Empty(submitted);
        }

        [Fact]
        public void GetPublicCerts_ReturnsSubmittedItemsForEachPubFile()
        {
            var client = NewMockClient("default");
            client.Setup(c => c.ViewPublicCertificates(It.IsAny<ViewPublicCertificatesRequest>()))
                .Returns(new ViewPublicCertificatesResponse
                {
                    PubFileStoreLocation = new PublicFileStoreLocation
                    {
                        PubFileStore = new PublicFileStore
                        {
                            PubFiles = new[] { new PublicFile { Name = "pub-1.pem" } }
                        }
                    }
                });

            var certBytes = System.Text.Encoding.ASCII.GetBytes(ValidCertPem);
            client.Setup(c => c.ViewPublicCertificate(It.IsAny<ViewPubCertificateDetailRequest>()))
                .Returns(new ViewPubCertificateDetailResponse
                {
                    File = System.Convert.ToBase64String(certBytes)
                });

            var submitted = new List<CurrentInventoryItem>();
            var ci = new CertStoreInfo { Domain = "default", CertificateStore = "pubcert", InventoryPageSize = 100 };
            var config = new InventoryJobConfiguration { JobHistoryId = 1 };
            var rm = NewRequestManager();

            var result = rm.GetPublicCerts(config, client.Object, items => { submitted.AddRange(items); return true; }, ci);

            Assert.Equal(OrchestratorJobStatusJobResult.Success, result.Result);
            Assert.Single(submitted);
            Assert.False(submitted[0].PrivateKeyEntry);
        }

        [Fact]
        public void GetPublicCerts_RespectsPageSizeLimit()
        {
            var client = NewMockClient("default");
            var files = new[]
            {
                new PublicFile { Name = "pub-1.pem" },
                new PublicFile { Name = "pub-2.pem" }
            };
            client.Setup(c => c.ViewPublicCertificates(It.IsAny<ViewPublicCertificatesRequest>()))
                .Returns(new ViewPublicCertificatesResponse
                {
                    PubFileStoreLocation = new PublicFileStoreLocation
                    {
                        PubFileStore = new PublicFileStore { PubFiles = files }
                    }
                });

            var certBytes = System.Text.Encoding.ASCII.GetBytes(ValidCertPem);
            client.Setup(c => c.ViewPublicCertificate(It.IsAny<ViewPubCertificateDetailRequest>()))
                .Returns(new ViewPubCertificateDetailResponse { File = System.Convert.ToBase64String(certBytes) });

            var submitted = new List<CurrentInventoryItem>();
            var ci = new CertStoreInfo { Domain = "default", CertificateStore = "pubcert", InventoryPageSize = 1 };
            var config = new InventoryJobConfiguration { JobHistoryId = 1 };
            var rm = NewRequestManager();

            rm.GetPublicCerts(config, client.Object, items => { submitted.AddRange(items); return true; }, ci);

            Assert.Single(submitted);
        }
    }
}
