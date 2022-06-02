using Keyfactor.Extensions.Orchestrator.DataPower.Models.SupportingObjects;
using Newtonsoft.Json;

namespace Keyfactor.Extensions.Orchestrator.DataPower.Models.Requests
{
    public class CryptoCertificateAddRequest : Request
    {
        public CryptoCertificateAddRequest(string domain)
        {
            CryptoCert = new CryptoCertificate();
            Domain = domain;
            Method = "POST";
        }

        [JsonProperty("CryptoCertificate")] public CryptoCertificate CryptoCert { get; set; }

        public new string GetResource()
        {
            return "/mgmt/config/" + Domain + "/CryptoCertificate";
        }
    }
}