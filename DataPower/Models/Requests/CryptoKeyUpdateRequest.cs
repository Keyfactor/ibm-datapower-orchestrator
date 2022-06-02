using Keyfactor.Extensions.Orchestrator.DataPower.Models.SupportingObjects;
using Newtonsoft.Json;

namespace Keyfactor.Extensions.Orchestrator.DataPower.Models.Requests
{
    public class CryptoKeyUpdateRequest : Request
    {
        public CryptoKeyUpdateRequest(string domain, string name)
        {
            CryptoKey = new CryptoKey();
            Domain = domain;
            Method = "PUT";
            Name = name;
        }

        [JsonIgnore] private string Name { get; }

        [JsonProperty("CryptoKey")] public CryptoKey CryptoKey { get; set; }

        public new string GetResource()
        {
            return "/mgmt/config/" + Domain + "/CryptoKey/" + Name;
        }
    }
}