using Keyfactor.Extensions.Orchestrator.DataPower.Models.SupportingObjects;
using Newtonsoft.Json;

namespace Keyfactor.Extensions.Orchestrator.DataPower.Models.Responses
{
    public class ViewCryptoCertificateSingleResponse
    {
        [JsonProperty("CryptoCertificate")] public CryptoCertificate CryptoCertificate { get; set; }
    }
}