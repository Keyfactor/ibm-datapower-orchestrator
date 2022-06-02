using Keyfactor.Extensions.Orchestrator.DataPower.Models.SupportingObjects;
using Newtonsoft.Json;

namespace Keyfactor.Extensions.Orchestrator.DataPower.Models.Responses
{
    public class ViewCryptoCertificatesResponse
    {
        public ViewCryptoCertificatesResponse()
        {
            CryptoCertificates = new CryptoCertificate[0];
        }

        [JsonProperty("CryptoCertificate")] public CryptoCertificate[] CryptoCertificates { get; set; }
    }
}