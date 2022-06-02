using Newtonsoft.Json;

namespace Keyfactor.Extensions.Orchestrator.DataPower.Models.Requests
{
    public class CertificateObjectRequest
    {
        [JsonProperty("CertificateObject")] public string ObjectName { get; set; }
    }
}