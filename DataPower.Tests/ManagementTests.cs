using System;
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
    public class ManagementTests
    {
        private static (TestableManagement job, Mock<IDataPowerClient> client) NewManagement()
        {
            var client = new Mock<IDataPowerClient>();
            client.SetupGet(c => c.Domain).Returns("default");
            var job = new TestableManagement(new FakePamResolver());
            job.PublicCertManager.ClientFactory = (_, _, _, _) => client.Object;
            return (job, client);
        }

        private static ManagementJobConfiguration NewConfig(CertStoreOperationType operation, string storePath)
        {
            return new ManagementJobConfiguration
            {
                JobHistoryId = 11,
                OperationType = operation,
                CertificateStoreDetails = new CertificateStore
                {
                    ClientMachine = "dp.example.com:5554",
                    StorePath = storePath,
                    Properties = JsonConvert.SerializeObject(new { })
                },
                JobCertificate = new ManagementJobCertificate { Alias = "mycert" }
            };
        }

        [Fact]
        public void ProcessJob_NullConfig_ReturnsFailure()
        {
            var (job, _) = NewManagement();
            var result = job.ProcessJob(null!);
            Assert.Equal(OrchestratorJobStatusJobResult.Failure, result.Result);
        }

        [Fact]
        public void ProcessJob_MissingClientMachine_ReturnsFailure()
        {
            var (job, _) = NewManagement();
            var config = NewConfig(CertStoreOperationType.Add, @"default\sharedcert");
            config.CertificateStoreDetails.ClientMachine = "";

            var result = job.ProcessJob(config);

            Assert.Equal(OrchestratorJobStatusJobResult.Failure, result.Result);
        }

        [Fact]
        public void ProcessJob_UnrecognizedOperation_ReturnsFailure()
        {
            var (job, _) = NewManagement();
            var config = NewConfig(CertStoreOperationType.Reenrollment, @"default\sharedcert");

            var result = job.ProcessJob(config);

            Assert.Equal(OrchestratorJobStatusJobResult.Failure, result.Result);
            Assert.Contains("Unrecognized Operation", result.FailureMessage);
        }

        [Fact]
        public void ProcessJob_AddOperation_DelegatesToRequestManagerAndReturnsSuccess()
        {
            var (job, client) = NewManagement();
            client.Setup(c => c.ViewPublicCertificates(It.IsAny<ViewPublicCertificatesRequest>()))
                .Returns(new ViewPublicCertificatesResponse
                {
                    PubFileStoreLocation = new PublicFileStoreLocation
                    {
                        PubFileStore = new PublicFileStore { PubFiles = Array.Empty<PublicFile>() }
                    }
                });
            client.Setup(c => c.ViewCertificates(It.IsAny<ViewCryptoCertificatesRequest>()))
                .Returns(new ViewCryptoCertificatesResponse { CryptoCertificates = Array.Empty<CryptoCertificate>() });
            client.Setup(c => c.ViewCryptoKeys(It.IsAny<ViewCryptoKeysRequest>()))
                .Returns(new ViewCryptoKeysResponse { CryptoKeys = Array.Empty<CryptoKey>() });

            var config = NewConfig(CertStoreOperationType.Add, @"default\sharedcert");
            config.JobCertificate.PrivateKeyPassword = "pw";
            config.JobCertificate.Contents = Convert.ToBase64String(new byte[] { 1, 2, 3 });

            var result = job.ProcessJob(config);

            Assert.Equal(OrchestratorJobStatusJobResult.Success, result.Result);
            Assert.Contains("Flow: Management-ProcessJob", result.FailureMessage);
        }

        [Fact]
        public void ProcessJob_RemoveOperation_DelegatesToRequestManager()
        {
            var (job, client) = NewManagement();
            client.Setup(c => c.ViewCryptoCertificate(It.IsAny<ViewCryptoCertificatesRequest>()))
                .Returns(new ViewCryptoCertificateSingleResponse { CryptoCertificate = new CryptoCertificate() });
            client.Setup(c => c.ViewCryptoKeys(It.IsAny<ViewCryptoKeysRequest>()))
                .Returns(new ViewCryptoKeysResponse { CryptoKeys = Array.Empty<CryptoKey>() });

            var config = NewConfig(CertStoreOperationType.Remove, @"default\sharedcert");

            var result = job.ProcessJob(config);

            Assert.Equal(OrchestratorJobStatusJobResult.Success, result.Result);
        }
    }
}
