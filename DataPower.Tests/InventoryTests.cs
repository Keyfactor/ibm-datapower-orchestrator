using System.Collections.Generic;
using DataPower.Tests.TestSupport;
using Keyfactor.Extensions.Orchestrator.DataPower.Client;
using Keyfactor.Extensions.Orchestrator.DataPower.Models.Requests;
using Keyfactor.Extensions.Orchestrator.DataPower.Models.Responses;
using Keyfactor.Extensions.Orchestrator.DataPower.Models.SupportingObjects;
using Keyfactor.Orchestrators.Common.Enums;
using Keyfactor.Orchestrators.Extensions;
using Moq;
using Newtonsoft.Json;

namespace DataPower.Tests
{
    public class InventoryTests
    {
        private static InventoryJobConfiguration NewConfig(string storePath) => new InventoryJobConfiguration
        {
            JobHistoryId = 5,
            ServerUsername = "admin",
            ServerPassword = "secret",
            CertificateStoreDetails = new CertificateStore
            {
                ClientMachine = "dp.example.com:5554",
                StorePath = storePath,
                Properties = JsonConvert.SerializeObject(new { })
            }
        };

        private static (TestableInventory job, Mock<IDataPowerClient> client) NewInventory()
        {
            var client = new Mock<IDataPowerClient>();
            client.SetupGet(c => c.Domain).Returns("default");
            var job = new TestableInventory(new FakePamResolver())
            {
                ApiClientFactory = (_, _, _, _) => client.Object
            };
            return (job, client);
        }

        [Fact]
        public void ProcessJob_PubcertStorePath_RoutesToGetPublicCerts()
        {
            var (job, client) = NewInventory();
            client.Setup(c => c.ViewPublicCertificates(It.IsAny<ViewPublicCertificatesRequest>()))
                .Returns(new ViewPublicCertificatesResponse
                {
                    PubFileStoreLocation = new PublicFileStoreLocation
                    {
                        PubFileStore = new PublicFileStore { PubFiles = System.Array.Empty<PublicFile>() }
                    }
                });

            var result = job.ProcessJob(NewConfig(@"default\pubcert"), items => true);

            Assert.Equal(OrchestratorJobStatusJobResult.Success, result.Result);
            client.Verify(c => c.ViewPublicCertificates(It.IsAny<ViewPublicCertificatesRequest>()), Times.Once);
            client.Verify(c => c.ViewCertificates(It.IsAny<ViewCryptoCertificatesRequest>()), Times.Never);
        }

        [Fact]
        public void ProcessJob_SharedcertStorePath_RoutesToGetCerts()
        {
            var (job, client) = NewInventory();
            client.Setup(c => c.ViewCertificates(It.IsAny<ViewCryptoCertificatesRequest>()))
                .Returns(new ViewCryptoCertificatesResponse { CryptoCertificates = System.Array.Empty<CryptoCertificate>() });

            var result = job.ProcessJob(NewConfig(@"default\sharedcert"), items => true);

            Assert.Equal(OrchestratorJobStatusJobResult.Success, result.Result);
            client.Verify(c => c.ViewCertificates(It.IsAny<ViewCryptoCertificatesRequest>()), Times.Once);
            client.Verify(c => c.ViewPublicCertificates(It.IsAny<ViewPublicCertificatesRequest>()), Times.Never);
        }

        [Fact]
        public void ProcessJob_UnresolvedCertInGetCerts_ReturnsWarningWithFlowSummaryAppended()
        {
            var (job, client) = NewInventory();
            client.Setup(c => c.ViewCertificates(It.IsAny<ViewCryptoCertificatesRequest>()))
                .Returns(new ViewCryptoCertificatesResponse
                {
                    CryptoCertificates = new[]
                        { new CryptoCertificate { Name = "bad-p12", CertFile = "sharedcert:///bad-p12.p12" } }
                });
            client.Setup(c => c.ViewCryptoCertificate(It.IsAny<ViewCertificateDetailRequest>()))
                .Returns(new ViewCertificateDetailResponse { CryptoCertObject = null! });

            var result = job.ProcessJob(NewConfig(@"default\sharedcert"), items => true);

            Assert.Equal(OrchestratorJobStatusJobResult.Warning, result.Result);
            Assert.Contains("bad-p12", result.FailureMessage);
            Assert.Contains("Flow: Inventory-ProcessJob", result.FailureMessage);
        }

        [Fact]
        public void ProcessJob_NullConfig_ReturnsFailure()
        {
            var (job, _) = NewInventory();
            var result = job.ProcessJob(null!, items => true);
            Assert.Equal(OrchestratorJobStatusJobResult.Failure, result.Result);
        }

        [Fact]
        public void ProcessJob_NullSubmitDelegate_ReturnsFailure()
        {
            var (job, _) = NewInventory();
            var result = job.ProcessJob(NewConfig(@"default\sharedcert"), null!);
            Assert.Equal(OrchestratorJobStatusJobResult.Failure, result.Result);
        }

        [Fact]
        public void ProcessJob_MissingStorePath_ReturnsFailure()
        {
            var (job, _) = NewInventory();
            var config = NewConfig("");
            var result = job.ProcessJob(config, items => true);
            Assert.Equal(OrchestratorJobStatusJobResult.Failure, result.Result);
        }

        [Fact]
        public void ProcessJob_ApiExceptionFromClient_ReturnsFailureWithDescribedMessage()
        {
            var (job, client) = NewInventory();
            client.Setup(c => c.ViewCertificates(It.IsAny<ViewCryptoCertificatesRequest>()))
                .Throws(new DataPowerApiException("bad", System.Net.HttpStatusCode.Forbidden, "ViewCertificates", "{}"));

            var result = job.ProcessJob(NewConfig(@"default\sharedcert"), items => true);

            Assert.Equal(OrchestratorJobStatusJobResult.Failure, result.Result);
            Assert.Contains("403", result.FailureMessage);
        }
    }
}
