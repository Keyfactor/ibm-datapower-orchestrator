using Keyfactor.Extensions.Orchestrator.DataPower.Models.SupportingObjects;
using Newtonsoft.Json;

namespace Keyfactor.Extensions.Orchestrator.DataPower.Models.Responses
{
    public class ViewCertificateDetailResponse
    {
        [JsonProperty("value")] public string Result { get; set; }

        [JsonProperty("CryptoCertificate")] public CryptoCert CryptoCertObject { get; set; }
    }
}