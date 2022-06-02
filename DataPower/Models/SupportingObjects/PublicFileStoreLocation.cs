using Newtonsoft.Json;

namespace Keyfactor.Extensions.Orchestrator.DataPower.Models.SupportingObjects
{
    public class PublicFileStoreLocation
    {
        [JsonProperty("location")] public PublicFileStore PubFileStore { get; set; }
    }
}