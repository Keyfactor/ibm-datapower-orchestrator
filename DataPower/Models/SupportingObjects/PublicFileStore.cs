using Newtonsoft.Json;

namespace Keyfactor.Extensions.Orchestrator.DataPower.Models.SupportingObjects
{
    public class PublicFileStore
    {
        [JsonProperty("file")] public PublicFile[] PubFiles { get; set; }
    }
}