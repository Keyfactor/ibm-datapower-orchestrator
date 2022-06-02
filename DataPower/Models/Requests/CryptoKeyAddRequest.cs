using Keyfactor.Extensions.Orchestrator.DataPower.Models.SupportingObjects;
using Newtonsoft.Json;

namespace Keyfactor.Extensions.Orchestrator.DataPower.Models.Requests
{
    public class CryptoKeyAddRequest : Request
    {
        public CryptoKeyAddRequest(string domain)
        {
            CryptoKey = new CryptoKey();
            Domain = domain;
            Method = "POST";
        }

        [JsonProperty("CryptoKey")] public CryptoKey CryptoKey { get; set; }

        public new string GetResource()
        {
            return "/mgmt/config/" + Domain + "/CryptoKey";
        }
    }
}