using Keyfactor.Extensions.Orchestrator.DataPower.Models.SupportingObjects;
using Newtonsoft.Json;

namespace Keyfactor.Extensions.Orchestrator.DataPower.Models.Requests
{
    public class CryptoCertificateUpdateRequest : Request
    {
        public CryptoCertificateUpdateRequest(string domain, string name)
        {
            CryptoCert = new CryptoCertificate();
            Domain = domain;
            Method = "PUT";
            Name = name;
        }

        [JsonIgnore] private string Name { get; }

        [JsonProperty("CryptoCertificate")] public CryptoCertificate CryptoCert { get; set; }

        public new string GetResource()
        {
            return "/mgmt/config/" + Domain + "/CryptoCertificate/" + Name;
        }
    }
}