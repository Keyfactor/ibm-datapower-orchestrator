using Keyfactor.Extensions.Orchestrator.DataPower.Models.SupportingObjects;
using Newtonsoft.Json;

namespace Keyfactor.Extensions.Orchestrator.DataPower.Models.Responses
{
    public class ViewPublicCertificatesResponse
    {
        [JsonProperty("filestore")] public PublicFileStoreLocation PubFileStoreLocation { get; set; }
    }
}