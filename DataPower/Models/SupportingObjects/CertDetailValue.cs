using Newtonsoft.Json;

namespace Keyfactor.Extensions.Orchestrator.DataPower.Models.SupportingObjects
{
    public class CertDetailValue
    {
        [JsonProperty("value")] public string Value { get; set; }
    }
}