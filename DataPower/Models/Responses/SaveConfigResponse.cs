using Newtonsoft.Json;

namespace Keyfactor.Extensions.Orchestrator.DataPower.Models.Responses
{
    public class SaveConfigResponse
    {
        [JsonProperty("SaveConfig")] public string SaveConfig { get; set; }
    }
}