using Keyfactor.Extensions.Orchestrator.DataPower.Models.Requests;

namespace DataPower.Tests
{
    // Direct coverage of every Request subclass's GetResource() URL-building logic.
    // These are pure, deterministic string builders - cheap to verify exhaustively,
    // and they encode real behavior (e.g. which domain/folder a filestore write or
    // delete actually targets) that RequestManager depends on.
    public class RequestModelsTests
    {
        [Fact]
        public void Request_BaseClass_DefaultGetResource_ReturnsEmptyString()
        {
            Assert.Equal("", new RequestStub().GetResource());
        }

        private class RequestStub : Request
        {
        }

        [Fact]
        public void CertificateAddRequest_BuildsFilestorePathWithFolderAndTrimmedFilename()
        {
            var req = new CertificateAddRequest("default", " mycert.pem ", " sharedcert ");
            Assert.Equal("/mgmt/filestore/default/sharedcert/mycert.pem", req.GetResource());
            Assert.Equal("PUT", req.Method);
        }

        [Fact]
        public void CryptoCertificateAddRequest_BuildsConfigPath()
        {
            var req = new CryptoCertificateAddRequest("test-domain-01");
            Assert.Equal("/mgmt/config/test-domain-01/CryptoCertificate", req.GetResource());
            Assert.Equal("POST", req.Method);
        }

        [Fact]
        public void CryptoCertificateUpdateRequest_BuildsConfigPathWithName()
        {
            var req = new CryptoCertificateUpdateRequest("test-domain-01", "mycert");
            Assert.Equal("/mgmt/config/test-domain-01/CryptoCertificate/mycert", req.GetResource());
            Assert.Equal("PUT", req.Method);
        }

        [Fact]
        public void CryptoKeyAddRequest_BuildsConfigPath()
        {
            var req = new CryptoKeyAddRequest("default");
            Assert.Equal("/mgmt/config/default/CryptoKey", req.GetResource());
        }

        [Fact]
        public void CryptoKeyUpdateRequest_BuildsConfigPathWithName()
        {
            var req = new CryptoKeyUpdateRequest("default", "mykey");
            Assert.Equal("/mgmt/config/default/CryptoKey/mykey", req.GetResource());
        }

        [Fact]
        public void DeleteCertificateRequest_DefaultFolder_IsCert()
        {
            var req = new DeleteCertificateRequest("test-domain-01", "mycert.pem");
            Assert.Equal("/mgmt/filestore/test-domain-01/cert/mycert.pem", req.GetResource());
            Assert.Equal("DELETE", req.Method);
        }

        [Fact]
        public void DeleteCertificateRequest_ExplicitFolder_UsesThatFolder_NotHardcodedCert()
        {
            // Regression test: this used to hardcode "cert" regardless of store type,
            // which would have misdirected sharedcert file deletes.
            var req = new DeleteCertificateRequest("default", "myshared.pem", "sharedcert");
            Assert.Equal("/mgmt/filestore/default/sharedcert/myshared.pem", req.GetResource());
        }

        [Fact]
        public void DeleteCryptoCertificateRequest_BuildsConfigPath()
        {
            var req = new DeleteCryptoCertificateRequest("default", "mycert");
            Assert.Equal("/mgmt/config/default/CryptoCertificate/mycert", req.GetResource());
        }

        [Fact]
        public void DeleteCryptoKeyRequest_BuildsConfigPath()
        {
            var req = new DeleteCryptoKeyRequest("default", "mykey");
            Assert.Equal("/mgmt/config/default/CryptoKey/mykey", req.GetResource());
        }

        [Fact]
        public void ListDomainsRequest_BuildsFixedPath()
        {
            var req = new ListDomainsRequest();
            Assert.Equal("/mgmt/domains/config/", req.GetResource());
            Assert.Equal("GET", req.Method);
        }

        [Fact]
        public void ListFileStoreRequest_BuildsPathForDomain()
        {
            var req = new ListFileStoreRequest("test-domain-01");
            Assert.Equal("/mgmt/filestore/test-domain-01", req.GetResource());
        }

        [Fact]
        public void SaveConfigRequest_BuildsActionQueuePath()
        {
            var req = new SaveConfigRequest("default");
            Assert.Equal("/mgmt/actionqueue/default", req.GetResource());
            Assert.Equal("POST", req.Method);
        }

        [Fact]
        public void ViewCertificateDetailRequest_BuildsActionQueuePath()
        {
            var req = new ViewCertificateDetailRequest("test-domain-01");
            Assert.Equal("/mgmt/actionqueue/test-domain-01", req.GetResource());
        }

        [Fact]
        public void ViewCryptoCertificatesRequest_WithoutAlias_ListsAllInDomain()
        {
            var req = new ViewCryptoCertificatesRequest("default");
            Assert.Equal("/mgmt/config/default/CryptoCertificate", req.GetResource());
        }

        [Fact]
        public void ViewCryptoCertificatesRequest_WithAlias_TargetsSingleObject()
        {
            var req = new ViewCryptoCertificatesRequest("default", "mycert");
            Assert.Equal("/mgmt/config/default/CryptoCertificate/mycert", req.GetResource());
        }

        [Fact]
        public void ViewCryptoKeyRequest_BuildsPathWithAlias()
        {
            var req = new ViewCryptoKeyRequest("default", "mykey");
            Assert.Equal("/mgmt/config/default/CryptoKey/mykey", req.GetResource());
        }

        [Fact]
        public void ViewCryptoKeysRequest_BuildsPathForDomain()
        {
            var req = new ViewCryptoKeysRequest("test-domain-01");
            Assert.Equal("/mgmt/config/test-domain-01/CryptoKey", req.GetResource());
        }

        [Fact]
        public void ViewPubCertificateDetailRequest_BuildsFixedDefaultPubcertPath()
        {
            var req = new ViewPubCertificateDetailRequest(" mypub.pem ");
            Assert.Equal("/mgmt/filestore/default/pubcert/mypub.pem", req.GetResource());
        }

        [Fact]
        public void ViewPublicCertificatesRequest_DefaultConstructor_TargetsDefaultPubcert()
        {
            var req = new ViewPublicCertificatesRequest();
            Assert.Equal("/mgmt/filestore/default/pubcert", req.GetResource());
        }

        [Fact]
        public void ViewPublicCertificatesRequest_ExplicitDomainAndFolder_BuildsPath()
        {
            var req = new ViewPublicCertificatesRequest("test-domain-01", " sharedcert ");
            Assert.Equal("/mgmt/filestore/test-domain-01/sharedcert", req.GetResource());
        }

        [Fact]
        public void CertificateObjectRequest_ObjectNameRoundTrips()
        {
            var req = new CertificateObjectRequest { ObjectName = "mycert" };
            Assert.Equal("mycert", req.ObjectName);
        }

        [Fact]
        public void CertificateRequest_DefaultsNameAndContentToEmptyString()
        {
            var req = new CertificateRequest();
            Assert.Equal("", req.Name);
            Assert.Equal("", req.Content);
        }
    }
}
