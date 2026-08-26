using System;
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
    public class RequestManagerAddRemoveTests
    {
        private static (RequestManager rm, Mock<IDataPowerClient> client) NewRequestManager(string clientDomain)
        {
            var client = new Mock<IDataPowerClient>();
            client.SetupGet(c => c.Domain).Returns(clientDomain);
            var rm = new RequestManager(new FakePamResolver())
            {
                ClientFactory = (_, _, _, _) => client.Object
            };
            return (rm, client);
        }

        private static ManagementJobConfiguration NewAddConfig(string storePath, string alias)
        {
            return new ManagementJobConfiguration
            {
                JobHistoryId = 9,
                OperationType = CertStoreOperationType.Add,
                CertificateStoreDetails = new CertificateStore
                {
                    ClientMachine = "dp.example.com:5554",
                    StorePath = storePath
                },
                JobCertificate = new ManagementJobCertificate
                {
                    Alias = alias,
                    PrivateKeyPassword = "not-a-real-password",
                    Contents = Convert.ToBase64String(new byte[] { 1, 2, 3, 4 }) // garbage PKCS12 - GetCertPem logs & no-ops
                }
            };
        }

        private static void SetupEmptyExistenceChecks(Mock<IDataPowerClient> client)
        {
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
        }

        [Fact]
        public void Add_SharedcertPerDomainStore_RoutesFileWritesToDefaultAndConfigObjectsToOwningDomain()
        {
            var (rm, client) = NewRequestManager("test-domain-01");
            SetupEmptyExistenceChecks(client);

            var certFileDomains = new List<string>();
            client.Setup(c => c.AddCertificateFile(It.IsAny<CertificateAddRequest>()))
                .Callback<CertificateAddRequest>(r => certFileDomains.Add(r.Domain))
                .Returns(true);

            var cryptoCertDomains = new List<string>();
            client.Setup(c => c.AddCryptoCertificate(It.IsAny<CryptoCertificateAddRequest>()))
                .Callback<CryptoCertificateAddRequest>(r => cryptoCertDomains.Add(r.Domain))
                .Returns(true);

            var cryptoKeyDomains = new List<string>();
            client.Setup(c => c.AddCryptoKey(It.IsAny<CryptoKeyAddRequest>()))
                .Callback<CryptoKeyAddRequest>(r => cryptoKeyDomains.Add(r.Domain))
                .Returns(true);

            client.Setup(c => c.SaveConfig()).Returns(true);

            var ci = new CertStoreInfo { Domain = "test-domain-01", CertificateStore = "sharedcert", PublicCertStoreName = "pubcert" };
            var config = NewAddConfig(@"test-domain-01\sharedcert", "mycert");

            var result = rm.Add(config, ci, new NamePrefix());

            Assert.Equal(OrchestratorJobStatusJobResult.Success, result.Result);

            // The sharedcert:// file itself can only be written through default...
            Assert.All(certFileDomains, d => Assert.Equal("default", d));
            Assert.Equal(2, certFileDomains.Count); // cert file + key file

            // ...but the CryptoCertificate/CryptoKey config objects belong to the
            // domain that actually owns them (test-domain-01), not default.
            Assert.All(cryptoCertDomains, d => Assert.Equal("test-domain-01", d));
            Assert.All(cryptoKeyDomains, d => Assert.Equal("test-domain-01", d));
        }

        [Fact]
        public void Add_ExistingCryptoCertificateObject_UpdatesInPlaceInsteadOfCreatingDuplicate()
        {
            var (rm, client) = NewRequestManager("test-domain-01");
            SetupEmptyExistenceChecks(client);

            // Existing object matches the alias-derived name exactly.
            client.Setup(c => c.ViewCertificates(It.IsAny<ViewCryptoCertificatesRequest>()))
                .Returns(new ViewCryptoCertificatesResponse
                {
                    CryptoCertificates = new[] { new CryptoCertificate { Name = "mycert" } }
                });

            client.Setup(c => c.AddCertificateFile(It.IsAny<CertificateAddRequest>())).Returns(true);
            client.Setup(c => c.SaveConfig()).Returns(true);

            var ci = new CertStoreInfo { Domain = "test-domain-01", CertificateStore = "sharedcert", PublicCertStoreName = "pubcert" };
            var config = NewAddConfig(@"test-domain-01\sharedcert", "mycert");

            var result = rm.Add(config, ci, new NamePrefix());

            Assert.Equal(OrchestratorJobStatusJobResult.Success, result.Result);
            client.Verify(c => c.AddCryptoCertificate(It.IsAny<CryptoCertificateAddRequest>()), Times.Never);
            // Disable + Update against the existing object = two UpdateCryptoCertificate calls.
            client.Verify(c => c.UpdateCryptoCertificate(It.IsAny<CryptoCertificateUpdateRequest>()), Times.Exactly(2));
        }

        [Fact]
        public void Add_PubcertToNonDefaultDomain_IsRejectedBeforeAnyApiCall()
        {
            var (rm, client) = NewRequestManager("test-domain-01");

            var ci = new CertStoreInfo { Domain = "test-domain-01", CertificateStore = "pubcert", PublicCertStoreName = "pubcert" };
            var config = NewAddConfig(@"test-domain-01\pubcert", "mycert");

            var result = rm.Add(config, ci, new NamePrefix());

            Assert.Equal(OrchestratorJobStatusJobResult.Failure, result.Result);
            Assert.Contains("pubcert on the default domain", result.FailureMessage);
            client.Verify(c => c.AddCertificateFile(It.IsAny<CertificateAddRequest>()), Times.Never);
        }

        [Fact]
        public void Add_ApplianceReturnsError_PropagatesFailureInsteadOfSwallowingIt()
        {
            var (rm, client) = NewRequestManager("test-domain-01");
            SetupEmptyExistenceChecks(client);
            client.Setup(c => c.AddCertificateFile(It.IsAny<CertificateAddRequest>()))
                .Throws(new DataPowerApiException("boom", System.Net.HttpStatusCode.InternalServerError,
                    "AddCertificateFile", "{\"error\":\"disk full\"}"));

            var ci = new CertStoreInfo { Domain = "test-domain-01", CertificateStore = "sharedcert", PublicCertStoreName = "pubcert" };
            var config = NewAddConfig(@"test-domain-01\sharedcert", "mycert");

            var result = rm.Add(config, ci, new NamePrefix());

            Assert.Equal(OrchestratorJobStatusJobResult.Failure, result.Result);
            Assert.Contains("mycert", result.FailureMessage);
            // Should not have masked the failure by saving whatever partial state exists.
            client.Verify(c => c.SaveConfig(), Times.Never);
        }

        [Fact]
        public void Remove_DeletesCryptoObjectAndFile_RoutingSharedcertFileDeleteThroughDefault()
        {
            var (rm, client) = NewRequestManager("test-domain-01");

            client.Setup(c => c.ViewCryptoCertificate(It.IsAny<ViewCryptoCertificatesRequest>()))
                .Returns(new ViewCryptoCertificateSingleResponse
                {
                    CryptoCertificate = new CryptoCertificate
                    {
                        Name = "mycert",
                        CertFile = "sharedcert:///mycert.cer"
                    }
                });
            client.Setup(c => c.ViewCryptoKeys(It.IsAny<ViewCryptoKeysRequest>()))
                .Returns(new ViewCryptoKeysResponse { CryptoKeys = Array.Empty<CryptoKey>() });

            string? deleteFileDomain = null;
            client.Setup(c => c.DeleteCertificate(It.IsAny<DeleteCertificateRequest>()))
                .Callback<DeleteCertificateRequest>(r => deleteFileDomain = r.Domain);
            client.Setup(c => c.SaveConfig()).Returns(true);

            var ci = new CertStoreInfo { Domain = "test-domain-01", CertificateStore = "sharedcert", PublicCertStoreName = "pubcert" };
            var config = new ManagementJobConfiguration
            {
                JobHistoryId = 3,
                OperationType = CertStoreOperationType.Remove,
                CertificateStoreDetails = new CertificateStore
                {
                    ClientMachine = "dp.example.com:5554",
                    StorePath = @"test-domain-01\sharedcert"
                },
                JobCertificate = new ManagementJobCertificate { Alias = "mycert" }
            };

            var result = rm.Remove(config, ci, new NamePrefix());

            Assert.Equal(OrchestratorJobStatusJobResult.Success, result.Result);
            client.Verify(c => c.DeleteCryptoCertificate(It.IsAny<DeleteCryptoCertificateRequest>()), Times.Once);
            Assert.Equal("default", deleteFileDomain);
        }

        [Fact]
        public void Remove_PublicCertStore_IsRejected()
        {
            var (rm, _) = NewRequestManager("default");
            var ci = new CertStoreInfo { Domain = "default", CertificateStore = "pubcert", PublicCertStoreName = "pubcert" };
            var config = new ManagementJobConfiguration
            {
                JobHistoryId = 3,
                OperationType = CertStoreOperationType.Remove,
                CertificateStoreDetails = new CertificateStore
                {
                    ClientMachine = "dp.example.com:5554",
                    StorePath = @"default\pubcert"
                },
                JobCertificate = new ManagementJobCertificate { Alias = "mycert" }
            };

            var result = rm.Remove(config, ci, new NamePrefix());

            Assert.Equal(OrchestratorJobStatusJobResult.Failure, result.Result);
        }

        [Fact]
        public void Remove_ExistingCryptoKey_IsDeletedAlongsideTheCertificate()
        {
            var (rm, client) = NewRequestManager("test-domain-01");

            client.Setup(c => c.ViewCryptoCertificate(It.IsAny<ViewCryptoCertificatesRequest>()))
                .Returns(new ViewCryptoCertificateSingleResponse
                {
                    CryptoCertificate = new CryptoCertificate { Name = "mycert", CertFile = "sharedcert:///mycert.cer" }
                });
            client.Setup(c => c.ViewCryptoKeys(It.IsAny<ViewCryptoKeysRequest>()))
                .Returns(new ViewCryptoKeysResponse
                {
                    CryptoKeys = new[] { new CryptoKey { Name = "mycert", CertFile = "sharedcert:///mycert.pem" } }
                });
            client.Setup(c => c.SaveConfig()).Returns(true);

            var ci = new CertStoreInfo { Domain = "test-domain-01", CertificateStore = "sharedcert", PublicCertStoreName = "pubcert" };
            var config = new ManagementJobConfiguration
            {
                JobHistoryId = 3,
                OperationType = CertStoreOperationType.Remove,
                CertificateStoreDetails = new CertificateStore
                {
                    ClientMachine = "dp.example.com:5554",
                    StorePath = @"test-domain-01\sharedcert"
                },
                JobCertificate = new ManagementJobCertificate { Alias = "mycert" }
            };

            var result = rm.Remove(config, ci, new NamePrefix());

            Assert.Equal(OrchestratorJobStatusJobResult.Success, result.Result);
            client.Verify(c => c.DeleteCryptoKey(It.IsAny<DeleteCryptoKeyRequest>()), Times.Once);
            client.Verify(c => c.DeleteCertificate(It.IsAny<DeleteCertificateRequest>()), Times.Exactly(2));
        }

        [Fact]
        public void Add_EverythingAlreadyExists_ReplacesFilesAndUpdatesBothConfigObjects()
        {
            var (rm, client) = NewRequestManager("test-domain-01");

            // Existing cert + key files under the derived filenames (mycert.cer / mycert.pem).
            client.Setup(c => c.ViewPublicCertificates(It.IsAny<ViewPublicCertificatesRequest>()))
                .Returns(new ViewPublicCertificatesResponse
                {
                    PubFileStoreLocation = new PublicFileStoreLocation
                    {
                        PubFileStore = new PublicFileStore
                        {
                            PubFiles = new[]
                            {
                                new PublicFile { Name = "mycert.cer" },
                                new PublicFile { Name = "mycert.pem" }
                            }
                        }
                    }
                });
            // Existing CryptoCertificate and CryptoKey objects matching the alias.
            client.Setup(c => c.ViewCertificates(It.IsAny<ViewCryptoCertificatesRequest>()))
                .Returns(new ViewCryptoCertificatesResponse
                {
                    CryptoCertificates = new[] { new CryptoCertificate { Name = "mycert" } }
                });
            client.Setup(c => c.ViewCryptoKeys(It.IsAny<ViewCryptoKeysRequest>()))
                .Returns(new ViewCryptoKeysResponse
                {
                    CryptoKeys = new[] { new CryptoKey { Name = "mycert" } }
                });
            client.Setup(c => c.AddCertificateFile(It.IsAny<CertificateAddRequest>())).Returns(true);
            client.Setup(c => c.DeleteCertificate(It.IsAny<DeleteCertificateRequest>()));
            client.Setup(c => c.SaveConfig()).Returns(true);

            var ci = new CertStoreInfo { Domain = "test-domain-01", CertificateStore = "sharedcert", PublicCertStoreName = "pubcert" };
            var config = NewAddConfig(@"test-domain-01\sharedcert", "mycert");

            var result = rm.Add(config, ci, new NamePrefix());

            Assert.Equal(OrchestratorJobStatusJobResult.Success, result.Result);
            // Old cert file + old key file each get deleted (via RemoveFile) before re-add.
            client.Verify(c => c.DeleteCertificate(It.IsAny<DeleteCertificateRequest>()), Times.Exactly(2));
            // Both config objects get disabled+updated (2 calls each) rather than added fresh.
            client.Verify(c => c.UpdateCryptoCertificate(It.IsAny<CryptoCertificateUpdateRequest>()), Times.Exactly(2));
            client.Verify(c => c.UpdateCryptoKey(It.IsAny<CryptoKeyUpdateRequest>()), Times.Exactly(2));
            client.Verify(c => c.AddCryptoCertificate(It.IsAny<CryptoCertificateAddRequest>()), Times.Never);
            client.Verify(c => c.AddCryptoKey(It.IsAny<CryptoKeyAddRequest>()), Times.Never);
        }

        [Fact]
        public void Add_ValidPfxContents_ExtractsRealCertAndKeySuccessfully()
        {
            var (rm, client) = NewRequestManager("default");
            SetupEmptyExistenceChecks(client);

            string? certPemSubmitted = null;
            client.Setup(c => c.AddCertificateFile(It.IsAny<CertificateAddRequest>()))
                .Callback<CertificateAddRequest>(r =>
                {
                    if (r.Filename.EndsWith(".cer")) certPemSubmitted = r.Certificate.Content;
                })
                .Returns(true);
            client.Setup(c => c.SaveConfig()).Returns(true);

            var ci = new CertStoreInfo { Domain = "default", CertificateStore = "sharedcert", PublicCertStoreName = "pubcert" };
            var config = new ManagementJobConfiguration
            {
                JobHistoryId = 9,
                OperationType = CertStoreOperationType.Add,
                CertificateStoreDetails = new CertificateStore
                {
                    ClientMachine = "dp.example.com:5554",
                    StorePath = @"default\sharedcert"
                },
                JobCertificate = new ManagementJobCertificate
                {
                    Alias = "mycert",
                    PrivateKeyPassword = "test1234",
                    Contents = RequestManagerAddPubCertTests.ValidPfxBase64
                }
            };

            var result = rm.Add(config, ci, new NamePrefix());

            Assert.Equal(OrchestratorJobStatusJobResult.Success, result.Result);
            // Utility.Pemify just line-wraps the base64 (no BEGIN/END armor) - confirm the
            // PKCS12 parse actually extracted real certificate bytes, not an empty/garbage string.
            Assert.NotNull(certPemSubmitted);
            Assert.True(certPemSubmitted!.Length > 100);
        }

        [Fact]
        public void Add_EmptyAlias_StillSucceedsWithGeneratedName()
        {
            var (rm, client) = NewRequestManager("default");
            SetupEmptyExistenceChecks(client);
            client.Setup(c => c.AddCertificateFile(It.IsAny<CertificateAddRequest>())).Returns(true);
            client.Setup(c => c.SaveConfig()).Returns(true);

            var ci = new CertStoreInfo { Domain = "default", CertificateStore = "sharedcert", PublicCertStoreName = "pubcert" };
            var config = NewAddConfig(@"default\sharedcert", "");

            var result = rm.Add(config, ci, new NamePrefix());

            Assert.Equal(OrchestratorJobStatusJobResult.Success, result.Result);
        }

        [Fact]
        public void Add_NullStorePath_OuterCatchRethrows()
        {
            var (rm, _) = NewRequestManager("default");
            var ci = new CertStoreInfo { Domain = "default", CertificateStore = "sharedcert", PublicCertStoreName = "pubcert" };
            var config = NewAddConfig(@"default\sharedcert", "mycert");
            config.CertificateStoreDetails.StorePath = null!;

            Assert.ThrowsAny<Exception>(() => rm.Add(config, ci, new NamePrefix()));
        }

        [Fact]
        public void Remove_NullStorePath_OuterCatchRethrows()
        {
            var (rm, _) = NewRequestManager("default");
            var ci = new CertStoreInfo { Domain = "default", CertificateStore = "sharedcert", PublicCertStoreName = "pubcert" };
            var config = new ManagementJobConfiguration
            {
                JobHistoryId = 3,
                OperationType = CertStoreOperationType.Remove,
                CertificateStoreDetails = new CertificateStore
                {
                    ClientMachine = "dp.example.com:5554",
                    StorePath = null
                },
                JobCertificate = new ManagementJobCertificate { Alias = "mycert" }
            };

            Assert.ThrowsAny<Exception>(() => rm.Remove(config, ci, new NamePrefix()));
        }

        [Fact]
        public void Remove_ViewCryptoCertificateThrows_IsSwallowedAndStillSavesConfig()
        {
            var (rm, client) = NewRequestManager("test-domain-01");
            client.Setup(c => c.ViewCryptoCertificate(It.IsAny<ViewCryptoCertificatesRequest>()))
                .Throws(new InvalidOperationException("appliance error"));
            client.Setup(c => c.SaveConfig()).Returns(true);

            var ci = new CertStoreInfo { Domain = "test-domain-01", CertificateStore = "sharedcert", PublicCertStoreName = "pubcert" };
            var config = new ManagementJobConfiguration
            {
                JobHistoryId = 3,
                OperationType = CertStoreOperationType.Remove,
                CertificateStoreDetails = new CertificateStore
                {
                    ClientMachine = "dp.example.com:5554",
                    StorePath = @"test-domain-01\sharedcert"
                },
                JobCertificate = new ManagementJobCertificate { Alias = "mycert" }
            };

            // RemoveCertFromDomain's own catch swallows this rather than failing the
            // whole job - one inaccessible/erroring domain lookup shouldn't block Remove.
            var result = rm.Remove(config, ci, new NamePrefix());

            Assert.Equal(OrchestratorJobStatusJobResult.Success, result.Result);
            client.Verify(c => c.SaveConfig(), Times.Once);
        }

        [Fact]
        public void Add_ValidDefaultPubcertPath_RoutesThroughAddPubCert()
        {
            var (rm, client) = NewRequestManager("default");
            client.Setup(c => c.AddCertificateFile(It.IsAny<CertificateAddRequest>())).Returns(true);
            client.Setup(c => c.SaveConfig()).Returns(true);

            var ci = new CertStoreInfo { Domain = "default", CertificateStore = "pubcert", PublicCertStoreName = "pubcert" };
            var config = NewAddConfig(@"default\pubcert", "mypub");
            config.JobCertificate.PrivateKeyPassword = "";

            var result = rm.Add(config, ci, new NamePrefix());

            Assert.Equal(OrchestratorJobStatusJobResult.Success, result.Result);
            client.Verify(c => c.AddCertificateFile(It.Is<CertificateAddRequest>(r => r.Filename == "mypub.pem")),
                Times.Once);
        }

        [Fact]
        public void Add_KeyFileUploadFails_PropagatesFailureAfterCertFileSucceeded()
        {
            var (rm, client) = NewRequestManager("default");
            SetupEmptyExistenceChecks(client);

            // Cert file (.cer) succeeds; key file (.pem) fails - exercises
            // ReplacePrivateKey's own catch/rethrow specifically, distinct from
            // ReplaceCertificateFile's (which the AddCertificateFile-always-throws
            // test already covers).
            client.Setup(c => c.AddCertificateFile(It.Is<CertificateAddRequest>(r => r.Filename.EndsWith(".cer"))))
                .Returns(true);
            client.Setup(c => c.AddCertificateFile(It.Is<CertificateAddRequest>(r => r.Filename.EndsWith(".pem"))))
                .Throws(new InvalidOperationException("key upload failed"));

            var ci = new CertStoreInfo { Domain = "default", CertificateStore = "sharedcert", PublicCertStoreName = "pubcert" };
            var config = NewAddConfig(@"default\sharedcert", "mycert");

            var result = rm.Add(config, ci, new NamePrefix());

            Assert.Equal(OrchestratorJobStatusJobResult.Failure, result.Result);
            Assert.Contains("key upload failed", result.FailureMessage);
        }
    }
}
