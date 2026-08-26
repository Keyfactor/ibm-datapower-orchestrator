using System;
using DataPower.Tests.TestSupport;
using Keyfactor.Extensions.Orchestrator.DataPower;
using Keyfactor.Extensions.Orchestrator.DataPower.Client;
using Keyfactor.Extensions.Orchestrator.DataPower.Models.Requests;
using Keyfactor.Extensions.Orchestrator.DataPower.Models.Responses;
using Keyfactor.Extensions.Orchestrator.DataPower.Models.SupportingObjects;
using Keyfactor.Orchestrators.Extensions;
using Moq;

namespace DataPower.Tests
{
    // Direct calls into RequestManager's public helper methods to exercise their
    // defensive catch blocks - branches that swallow an appliance-call failure and
    // return a safe default (rather than aborting the whole Add/Remove job) don't
    // get hit by the happy-path Add/Remove tests, since those never make the
    // appliance throw at exactly these call sites.
    public class RequestManagerHelperMethodTests
    {
        private static RequestManager NewRequestManager() => new RequestManager(new FakePamResolver());

        private static Mock<IDataPowerClient> NewMockClient(string domain = "default")
        {
            var mock = new Mock<IDataPowerClient>();
            mock.SetupGet(c => c.Domain).Returns(domain);
            return mock;
        }

        [Fact]
        public void DoesCryptoCertificateObjectExist_ApplianceThrows_ReturnsFalseInsteadOfPropagating()
        {
            var client = NewMockClient();
            client.Setup(c => c.ViewCertificates(It.IsAny<ViewCryptoCertificatesRequest>()))
                .Throws(new InvalidOperationException("RBAC denied"));

            var exists = NewRequestManager().DoesCryptoCertificateObjectExist(
                new CertStoreInfo { Domain = "default" }, "mycert", client.Object);

            Assert.False(exists);
        }

        [Fact]
        public void DisableCryptoCertificateObject_ApplianceThrows_DoesNotPropagate()
        {
            var client = NewMockClient();
            client.Setup(c => c.UpdateCryptoCertificate(It.IsAny<CryptoCertificateUpdateRequest>()))
                .Throws(new InvalidOperationException("boom"));

            // Should not throw.
            NewRequestManager().DisableCryptoCertificateObject("mycert", client.Object);
        }

        [Fact]
        public void DoesCryptoKeyObjectExist_ApplianceThrows_ReturnsFalseInsteadOfPropagating()
        {
            var client = NewMockClient();
            client.Setup(c => c.ViewCryptoKeys(It.IsAny<ViewCryptoKeysRequest>()))
                .Throws(new InvalidOperationException("RBAC denied"));

            var exists = NewRequestManager().DoesCryptoKeyObjectExist(
                new CertStoreInfo { Domain = "default" }, "mykey", client.Object);

            Assert.False(exists);
        }

        [Fact]
        public void DisableCryptoKeyObject_ApplianceThrows_DoesNotPropagate()
        {
            var client = NewMockClient();
            client.Setup(c => c.UpdateCryptoKey(It.IsAny<CryptoKeyUpdateRequest>()))
                .Throws(new InvalidOperationException("boom"));

            NewRequestManager().DisableCryptoKeyObject("mykey", client.Object);
        }

        [Fact]
        public void UpdatePrivateKey_ApplianceThrows_DoesNotPropagate()
        {
            var client = NewMockClient();
            client.Setup(c => c.UpdateCryptoKey(It.IsAny<CryptoKeyUpdateRequest>()))
                .Throws(new InvalidOperationException("boom"));

            NewRequestManager().UpdatePrivateKey(
                new CertStoreInfo { Domain = "default", CertificateStore = "sharedcert" },
                "mykey", client.Object, "mykey.pem", "myalias");
        }

        [Fact]
        public void AddCryptoKey_ApplianceThrows_DoesNotPropagate()
        {
            var client = NewMockClient();
            client.Setup(c => c.AddCryptoKey(It.IsAny<CryptoKeyAddRequest>()))
                .Throws(new InvalidOperationException("boom"));

            NewRequestManager().AddCryptoKey(
                new CertStoreInfo { Domain = "default", CertificateStore = "sharedcert" },
                "mykey", client.Object, "mykey.pem", "myalias");
        }

        [Fact]
        public void UpdateCryptoCert_ApplianceThrows_DoesNotPropagate()
        {
            var client = NewMockClient();
            client.Setup(c => c.UpdateCryptoCertificate(It.IsAny<CryptoCertificateUpdateRequest>()))
                .Throws(new InvalidOperationException("boom"));

            NewRequestManager().UpdateCryptoCert(
                new CertStoreInfo { Domain = "default", CertificateStore = "sharedcert" },
                "mycert", client.Object, "mycert.cer", "myalias");
        }

        [Fact]
        public void AddCryptoCert_ApplianceThrows_DoesNotPropagate()
        {
            var client = NewMockClient();
            client.Setup(c => c.AddCryptoCertificate(It.IsAny<CryptoCertificateAddRequest>()))
                .Throws(new InvalidOperationException("boom"));

            NewRequestManager().AddCryptoCert(
                new CertStoreInfo { Domain = "default", CertificateStore = "sharedcert" },
                "mycert", client.Object, "mycert.cer", "myalias");
        }

        [Fact]
        public void AddPrivateKey_BadFilename_ReturnsNullInsteadOfThrowing()
        {
            var client = NewMockClient();

            // CertificateAddRequest's constructor calls filename.Trim() - a null
            // keyFileName throws inside the try, which AddPrivateKey should catch.
            var result = NewRequestManager().AddPrivateKey(
                new CertStoreInfo { Domain = "default", CertificateStore = "sharedcert" },
                "myalias", null!, client.Object, "keydata", "default");

            Assert.Null(result);
        }

        [Fact]
        public void CertificateAddRequestMethod_BadFilename_ReturnsNullInsteadOfThrowing()
        {
            var client = NewMockClient();

            var result = NewRequestManager().CertificateAddRequest(
                new CertStoreInfo { Domain = "default", CertificateStore = "sharedcert" },
                "myalias", null!, client.Object, "certdata", "default");

            Assert.Null(result);
        }

        [Fact]
        public void DoesKeyFileExist_NullViewCertificateCollection_ReturnsFalseInsteadOfThrowing()
        {
            var exists = NewRequestManager().DoesKeyFileExist(
                new CertStoreInfo { Domain = "default" }, "mykey.pem", null!);

            Assert.False(exists);
        }

        [Fact]
        public void DoesCertificateFileExist_NullViewCertificateCollection_ReturnsFalseInsteadOfThrowing()
        {
            var client = NewMockClient();

            var exists = NewRequestManager().DoesCertificateFileExist(
                new CertStoreInfo { Domain = "default" }, client.Object, "mycert.cer", null!);

            Assert.False(exists);
        }

        [Fact]
        public void DoesKeyFileExist_MatchingFileName_ReturnsTrue()
        {
            var collection = new ViewPublicCertificatesResponse
            {
                PubFileStoreLocation = new PublicFileStoreLocation
                {
                    PubFileStore = new PublicFileStore { PubFiles = new[] { new PublicFile { Name = "mykey.pem" } } }
                }
            };

            var exists = NewRequestManager().DoesKeyFileExist(
                new CertStoreInfo { Domain = "default" }, "mykey.pem", collection);

            Assert.True(exists);
        }

        [Fact]
        public void DoesCertificateFileExist_MatchingFileName_ReturnsTrue()
        {
            var client = NewMockClient();
            var collection = new ViewPublicCertificatesResponse
            {
                PubFileStoreLocation = new PublicFileStoreLocation
                {
                    PubFileStore = new PublicFileStore { PubFiles = new[] { new PublicFile { Name = "mycert.cer" } } }
                }
            };

            var exists = NewRequestManager().DoesCertificateFileExist(
                new CertStoreInfo { Domain = "default" }, client.Object, "mycert.cer", collection);

            Assert.True(exists);
        }

        [Fact]
        public void GetCertPem_NoPrivateKeyPassword_ReturnsPemifiedContentsDirectly()
        {
            // AddCertStore only calls GetCertPem when PrivateKeyPassword is set, so its
            // "cert-only" else branch is only reachable by calling it directly like this.
            var config = new ManagementJobConfiguration
            {
                JobCertificate = new ManagementJobCertificate
                {
                    Alias = "myalias",
                    Contents = Convert.ToBase64String(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }),
                    PrivateKeyPassword = ""
                }
            };
            var privateKeyString = "";

            var certPem = NewRequestManager().GetCertPem(config, "myalias", ref privateKeyString);

            Assert.Equal(config.JobCertificate.Contents, certPem);
            Assert.Equal("", privateKeyString);
        }

        [Fact]
        public void RemovePrivateKeyFile_SaveConfigThrows_PropagatesFailure()
        {
            var client = NewMockClient();
            client.Setup(c => c.SaveConfig()).Throws(new InvalidOperationException("save failed"));
            var rm = NewRequestManager();
            rm.ClientFactory = (_, _, _, _) => client.Object;

            var config = new ManagementJobConfiguration
            {
                CertificateStoreDetails = new CertificateStore { ClientMachine = "dp.example.com:5554" },
                JobCertificate = new ManagementJobCertificate { Alias = "mycert" }
            };

            Assert.Throws<InvalidOperationException>(() =>
                rm.RemovePrivateKeyFile(config, new CertStoreInfo { Domain = "default", CertificateStore = "sharedcert" },
                    "mycert.pem", "default"));
        }

        [Fact]
        public void RemoveCertificate_SaveConfigThrows_PropagatesFailure()
        {
            var client = NewMockClient();
            client.Setup(c => c.SaveConfig()).Throws(new InvalidOperationException("save failed"));
            var rm = NewRequestManager();
            rm.ClientFactory = (_, _, _, _) => client.Object;

            var config = new ManagementJobConfiguration
            {
                CertificateStoreDetails = new CertificateStore { ClientMachine = "dp.example.com:5554" },
                JobCertificate = new ManagementJobCertificate { Alias = "mycert" }
            };

            Assert.Throws<InvalidOperationException>(() =>
                rm.RemoveCertificate(config, new CertStoreInfo { Domain = "default", CertificateStore = "sharedcert" },
                    "mycert.cer", "default"));
        }

        [Fact]
        public void RemoveCertificate_DeleteCertificateThrows_IsSwallowedAndConfigStillSaved()
        {
            // RemoveFile's own inner catch swallows a DeleteCertificate failure (e.g. the
            // file was already gone) rather than aborting the replace-file flow.
            var client = NewMockClient();
            client.Setup(c => c.DeleteCertificate(It.IsAny<DeleteCertificateRequest>()))
                .Throws(new InvalidOperationException("file not found"));
            client.Setup(c => c.SaveConfig()).Returns(true);
            var rm = NewRequestManager();
            rm.ClientFactory = (_, _, _, _) => client.Object;

            var config = new ManagementJobConfiguration
            {
                CertificateStoreDetails = new CertificateStore { ClientMachine = "dp.example.com:5554" },
                JobCertificate = new ManagementJobCertificate { Alias = "mycert" }
            };

            var result = rm.RemoveCertificate(config,
                new CertStoreInfo { Domain = "default", CertificateStore = "sharedcert" }, "mycert.cer", "default");

            Assert.Equal(Keyfactor.Orchestrators.Common.Enums.OrchestratorJobStatusJobResult.Success, result.Result);
            client.Verify(c => c.SaveConfig(), Times.Once);
        }
    }
}
