using Newtonsoft.Json;

namespace Keyfactor.Extensions.Orchestrator.DataPower.Models.Responses
{
    public class CertificateAddResponse
    {
        public CertificateAddResponse()
        {
            Result = "";
        }

        [JsonProperty("result")] public string Result { get; set; }
    }
}