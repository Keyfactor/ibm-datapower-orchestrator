using Newtonsoft.Json;

namespace Keyfactor.Extensions.Orchestrator.DataPower.Models.SupportingObjects
{
    public class PublicFile
    {
        [JsonProperty("name")] public string Name { get; set; }
    }
}