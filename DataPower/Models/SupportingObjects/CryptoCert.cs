using Newtonsoft.Json;

namespace Keyfactor.Extensions.Orchestrator.DataPower.Models.SupportingObjects
{
    public class CryptoCert
    {
        [JsonProperty("CertificateDetails")] public CertificateDetailsObject CertDetailsObject { get; set; }
    }
}