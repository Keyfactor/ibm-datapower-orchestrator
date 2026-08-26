using System;
using DataPower.Tests.TestSupport;
using Keyfactor.Extensions.Orchestrator.DataPower;
using Keyfactor.Extensions.Orchestrator.DataPower.Client;
using Keyfactor.Extensions.Orchestrator.DataPower.Models.Requests;
using Keyfactor.Orchestrators.Common.Enums;
using Keyfactor.Orchestrators.Extensions;
using Moq;

namespace DataPower.Tests
{
    public class RequestManagerAddPubCertTests
    {
        // A real self-signed PFX (password "test1234"), generated purely for test
        // fixtures via CertificateRequest.CreateSelfSigned + X509Certificate2.Export.
        private const string ValidPfxBase64 =
            "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";

        private static (RequestManager rm, Mock<IDataPowerClient> client) NewRequestManager()
        {
            var client = new Mock<IDataPowerClient>();
            client.SetupGet(c => c.Domain).Returns("default");
            var rm = new RequestManager(new FakePamResolver())
            {
                ClientFactory = (_, _, _, _) => client.Object
            };
            return (rm, client);
        }

        private static ManagementJobConfiguration NewConfig(string contents, string alias, string? pfxPassword)
        {
            return new ManagementJobConfiguration
            {
                JobHistoryId = 4,
                CertificateStoreDetails = new CertificateStore { ClientMachine = "dp.example.com:5554" },
                JobCertificate = new ManagementJobCertificate
                {
                    Alias = alias,
                    Contents = contents,
                    PrivateKeyPassword = pfxPassword!
                }
            };
        }

        [Fact]
        public void AddPubCert_PlainCertContents_Succeeds()
        {
            var (rm, client) = NewRequestManager();
            client.Setup(c => c.AddCertificateFile(It.IsAny<CertificateAddRequest>())).Returns(true);
            client.Setup(c => c.SaveConfig()).Returns(true);

            var ci = new CertStoreInfo { Domain = "default", CertificateStore = "pubcert", PublicCertStoreName = "pubcert" };
            var config = NewConfig(Convert.ToBase64String(new byte[] { 1, 2, 3 }), "mypub", null);

            var result = rm.AddPubCert(config, ci);

            Assert.Equal(OrchestratorJobStatusJobResult.Success, result.Result);
            client.Verify(c => c.AddCertificateFile(It.Is<CertificateAddRequest>(r => r.Filename == "mypub.pem")),
                Times.Once);
        }

        [Fact]
        public void AddPubCert_PfxWithPassword_ExtractsCertificateAndSucceeds()
        {
            var (rm, client) = NewRequestManager();
            client.Setup(c => c.AddCertificateFile(It.IsAny<CertificateAddRequest>())).Returns(true);
            client.Setup(c => c.SaveConfig()).Returns(true);

            var ci = new CertStoreInfo { Domain = "default", CertificateStore = "pubcert", PublicCertStoreName = "pubcert" };
            var config = NewConfig(ValidPfxBase64, "my.pfx.cert", "test1234");

            var result = rm.AddPubCert(config, ci);

            Assert.Equal(OrchestratorJobStatusJobResult.Success, result.Result);
            // Alias dots get replaced with underscores in the generated filename.
            client.Verify(c => c.AddCertificateFile(It.Is<CertificateAddRequest>(r => r.Filename == "my_pfx_cert.pem")),
                Times.Once);
        }

        [Fact]
        public void AddPubCert_EmptyAlias_GeneratesGuidBasedFilename()
        {
            var (rm, client) = NewRequestManager();
            client.Setup(c => c.AddCertificateFile(It.IsAny<CertificateAddRequest>())).Returns(true);
            client.Setup(c => c.SaveConfig()).Returns(true);

            var ci = new CertStoreInfo { Domain = "default", CertificateStore = "pubcert", PublicCertStoreName = "pubcert" };
            var config = NewConfig(Convert.ToBase64String(new byte[] { 1, 2, 3 }), "", null);

            var result = rm.AddPubCert(config, ci);

            Assert.Equal(OrchestratorJobStatusJobResult.Success, result.Result);
        }

        [Fact]
        public void AddPubCert_ApplianceRejectsUpload_ReturnsFailureAndStillSavesConfig()
        {
            var (rm, client) = NewRequestManager();
            client.Setup(c => c.AddCertificateFile(It.IsAny<CertificateAddRequest>()))
                .Throws(new InvalidOperationException("appliance rejected upload"));
            client.Setup(c => c.SaveConfig()).Returns(true);

            var ci = new CertStoreInfo { Domain = "default", CertificateStore = "pubcert", PublicCertStoreName = "pubcert" };
            var config = NewConfig(Convert.ToBase64String(new byte[] { 1, 2, 3 }), "mypub", null);

            var result = rm.AddPubCert(config, ci);

            Assert.Equal(OrchestratorJobStatusJobResult.Failure, result.Result);
            Assert.Contains("appliance rejected upload", result.FailureMessage);
            client.Verify(c => c.SaveConfig(), Times.Once);
        }
    }
}
