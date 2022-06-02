using Newtonsoft.Json;

namespace Keyfactor.Extensions.Orchestrator.DataPower.Models.Requests
{
    public class CertificateRequest
    {
        public CertificateRequest()
        {
            Name = "";
            Content = "";
        }

        [JsonProperty("name")] public string Name { get; set; }

        [JsonProperty("content")] public string Content { get; set; }
    }
}