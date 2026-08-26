using System;
using System.Collections.Generic;
using System.Linq;
using DataPower.Tests.TestSupport;
using Keyfactor.Extensions.Orchestrator.DataPower.Client;
using Keyfactor.Extensions.Orchestrator.DataPower.Models.Requests;
using Keyfactor.Extensions.Orchestrator.DataPower.Models.Responses;
using Keyfactor.Extensions.Orchestrator.DataPower.Models.SupportingObjects;
using Keyfactor.Orchestrators.Common.Enums;
using Keyfactor.Orchestrators.Extensions;
using Moq;

namespace DataPower.Tests
{
    public class DiscoveryTests
    {
        private static DiscoveryJobConfiguration NewConfig(Dictionary<string, object>? jobProperties = null) =>
            new DiscoveryJobConfiguration
            {
                ClientMachine = "dp.example.com:5554",
                ServerUsername = "admin",
                ServerPassword = "secret",
                JobHistoryId = 1,
                JobProperties = jobProperties
            };

        private static DomainInfo Domain(string name) => new DomainInfo { Name = name };

        private static (TestableDiscovery discovery, Mock<IDataPowerClient> client, List<string> submitted) NewDiscovery()
        {
            var client = new Mock<IDataPowerClient>();
            var discovery = new TestableDiscovery(new FakePamResolver())
            {
                ApiClientFactory = (_, _, _, _) => client.Object
            };
            return (discovery, client, new List<string>());
        }

        private static CryptoCertificate CertRefFile(string filename) =>
            new CryptoCertificate { Name = "obj", CertFile = filename };

        [Fact]
        public void PerformDiscovery_EmitsPerDomainCertAndDefaultOnlyPubcert()
        {
            var (discovery, client, submitted) = NewDiscovery();

            client.Setup(c => c.ListDomains()).Returns(new List<DomainInfo> { Domain("default"), Domain("test-domain-01") });
            client.Setup(c => c.ListFileStoreDirectories("default")).Returns(new List<string> { "cert:", "pubcert:", "sharedcert:" });
            client.Setup(c => c.ListFileStoreDirectories("test-domain-01")).Returns(new List<string> { "cert:", "sharedcert:" });
            client.Setup(c => c.ViewCertificates(It.IsAny<ViewCryptoCertificatesRequest>()))
                .Returns(new ViewCryptoCertificatesResponse { CryptoCertificates = Array.Empty<CryptoCertificate>() });

            var result = discovery.ProcessJob(NewConfig(), items =>
            {
                submitted.AddRange(items);
                return true;
            });

            Assert.Equal(OrchestratorJobStatusJobResult.Success, result.Result);
            Assert.Contains(@"default\cert", submitted);
            Assert.Contains(@"default\pubcert", submitted);
            Assert.Contains(@"test-domain-01\cert", submitted);
            Assert.DoesNotContain(@"test-domain-01\pubcert", submitted);
        }

        [Fact]
        public void PerformDiscovery_SharedcertOnlyEmittedForDomainsOwningAMatchingCryptoCertificateObject()
        {
            var (discovery, client, submitted) = NewDiscovery();

            client.Setup(c => c.ListDomains()).Returns(new List<DomainInfo>
            {
                Domain("default"), Domain("test-domain-01"), Domain("test-domain-02")
            });
            client.Setup(c => c.ListFileStoreDirectories(It.IsAny<string>()))
                .Returns(new List<string> { "cert:", "sharedcert:" });

            client.Setup(c => c.ViewCertificates(It.Is<ViewCryptoCertificatesRequest>(r => r.Domain == "default")))
                .Returns(new ViewCryptoCertificatesResponse
                {
                    CryptoCertificates = new[] { CertRefFile("sharedcert:///a.pem") }
                });
            client.Setup(c => c.ViewCertificates(It.Is<ViewCryptoCertificatesRequest>(r => r.Domain == "test-domain-01")))
                .Returns(new ViewCryptoCertificatesResponse
                {
                    CryptoCertificates = new[] { CertRefFile("sharedcert:///b.pem") }
                });
            client.Setup(c => c.ViewCertificates(It.Is<ViewCryptoCertificatesRequest>(r => r.Domain == "test-domain-02")))
                .Returns(new ViewCryptoCertificatesResponse
                {
                    CryptoCertificates = new[] { CertRefFile("cert:///c.pem") } // no sharedcert reference
                });

            var result = discovery.ProcessJob(NewConfig(), items =>
            {
                submitted.AddRange(items);
                return true;
            });

            Assert.Equal(OrchestratorJobStatusJobResult.Success, result.Result);
            Assert.Contains(@"default\sharedcert", submitted);
            Assert.Contains(@"test-domain-01\sharedcert", submitted);
            Assert.DoesNotContain(@"test-domain-02\sharedcert", submitted);
        }

        [Fact]
        public void PerformDiscovery_UserDirsToSearch_RestrictsToRequestedDirectories()
        {
            var (discovery, client, submitted) = NewDiscovery();

            client.Setup(c => c.ListDomains()).Returns(new List<DomainInfo> { Domain("default") });
            client.Setup(c => c.ListFileStoreDirectories("default"))
                .Returns(new List<string> { "cert:", "pubcert:", "sharedcert:" });

            var config = NewConfig(new Dictionary<string, object> { ["dirs"] = "cert" });

            var result = discovery.ProcessJob(config, items =>
            {
                submitted.AddRange(items);
                return true;
            });

            Assert.Equal(OrchestratorJobStatusJobResult.Success, result.Result);
            Assert.Contains(@"default\cert", submitted);
            Assert.DoesNotContain(@"default\pubcert", submitted);
            client.Verify(c => c.ViewCertificates(It.IsAny<ViewCryptoCertificatesRequest>()), Times.Never);
        }

        [Fact]
        public void PerformDiscovery_OneDomainFailingFilestoreListing_DoesNotAbortDiscovery()
        {
            var (discovery, client, submitted) = NewDiscovery();

            client.Setup(c => c.ListDomains()).Returns(new List<DomainInfo> { Domain("default"), Domain("broken-domain") });
            client.Setup(c => c.ListFileStoreDirectories("default")).Returns(new List<string> { "cert:" });
            client.Setup(c => c.ListFileStoreDirectories("broken-domain")).Throws(new InvalidOperationException("RBAC denied"));
            client.Setup(c => c.ViewCertificates(It.IsAny<ViewCryptoCertificatesRequest>()))
                .Returns(new ViewCryptoCertificatesResponse { CryptoCertificates = Array.Empty<CryptoCertificate>() });

            var result = discovery.ProcessJob(NewConfig(), items =>
            {
                submitted.AddRange(items);
                return true;
            });

            Assert.Equal(OrchestratorJobStatusJobResult.Success, result.Result);
            Assert.Contains(@"default\cert", submitted);
        }

        [Fact]
        public void PerformDiscovery_OneDomainFailingSharedcertProbe_DoesNotAbortDiscovery()
        {
            var (discovery, client, submitted) = NewDiscovery();

            client.Setup(c => c.ListDomains()).Returns(new List<DomainInfo> { Domain("default"), Domain("broken-domain") });
            client.Setup(c => c.ListFileStoreDirectories(It.IsAny<string>())).Returns(new List<string> { "sharedcert:" });
            client.Setup(c => c.ViewCertificates(It.Is<ViewCryptoCertificatesRequest>(r => r.Domain == "default")))
                .Returns(new ViewCryptoCertificatesResponse
                {
                    CryptoCertificates = new[] { CertRefFile("sharedcert:///a.pem") }
                });
            client.Setup(c => c.ViewCertificates(It.Is<ViewCryptoCertificatesRequest>(r => r.Domain == "broken-domain")))
                .Throws(new InvalidOperationException("RBAC denied"));

            var result = discovery.ProcessJob(NewConfig(), items =>
            {
                submitted.AddRange(items);
                return true;
            });

            Assert.Equal(OrchestratorJobStatusJobResult.Success, result.Result);
            Assert.Contains(@"default\sharedcert", submitted);
        }

        [Fact]
        public void ProcessJob_NullConfig_ReturnsFailure()
        {
            var (discovery, _, _) = NewDiscovery();
            var result = discovery.ProcessJob(null!, items => true);
            Assert.Equal(OrchestratorJobStatusJobResult.Failure, result.Result);
        }

        [Fact]
        public void ProcessJob_NullSubmitDelegate_ReturnsFailure()
        {
            var (discovery, _, _) = NewDiscovery();
            var result = discovery.ProcessJob(NewConfig(), null!);
            Assert.Equal(OrchestratorJobStatusJobResult.Failure, result.Result);
        }

        [Fact]
        public void ProcessJob_EmptyClientMachine_ReturnsFailure()
        {
            var (discovery, _, _) = NewDiscovery();
            var config = NewConfig();
            config.ClientMachine = "";

            var result = discovery.ProcessJob(config, items => true);

            Assert.Equal(OrchestratorJobStatusJobResult.Failure, result.Result);
        }

        [Fact]
        public void PerformDiscovery_NoDomainsReturned_SubmitsEmptyListAsSuccess()
        {
            var (discovery, client, submitted) = NewDiscovery();
            client.Setup(c => c.ListDomains()).Returns(new List<DomainInfo>());

            var result = discovery.ProcessJob(NewConfig(), items =>
            {
                submitted.AddRange(items);
                return true;
            });

            Assert.Equal(OrchestratorJobStatusJobResult.Success, result.Result);
            Assert.Empty(submitted);
        }
    }
}
