using Newtonsoft.Json;

namespace Keyfactor.Extensions.Orchestrator.DataPower.Models.Responses
{
    public class ViewPubCertificateDetailResponse
    {
        [JsonProperty("file")] public string File { get; set; }
    }
}