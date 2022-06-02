using Newtonsoft.Json;

namespace Keyfactor.Extensions.Orchestrator.DataPower.Models.SupportingObjects
{
    public class CertificateDetailsObject
    {
        [JsonProperty("SerialNumber")] public CertDetailValue SerialNumber { get; set; }

        [JsonProperty("SignatureAlgorithm")] public CertDetailValue SignatureAlgorithm { get; set; }

        [JsonProperty("Issuer")] public CertDetailValue Issuer { get; set; }

        [JsonProperty("NotBefore")] public CertDetailValue NotBefore { get; set; }

        [JsonProperty("NotAfter")] public CertDetailValue NotAfter { get; set; }

        [JsonProperty("Subject")] public CertDetailValue Subject { get; set; }

        [JsonProperty("SubjectPublicKeyAlgorithm")]
        public CertDetailValue SubjectPublicKeyAlgorithm { get; set; }

        [JsonProperty("SubjectPublicKeyBitLength")]
        public CertDetailValue SubjectPublicKeyBitLength { get; set; }

        [JsonProperty("Base64")] public CertDetailValue EncodedCert { get; set; }
    }
}