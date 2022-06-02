using Newtonsoft.Json;

namespace Keyfactor.Extensions.Orchestrator.DataPower.Models.Requests
{
    public class SaveConfigRequest : Request
    {
        public SaveConfigRequest(string domain)
        {
            SaveConfig = "";
            Domain = domain;
            Method = "POST";
        }

        [JsonProperty("SaveConfig")] public string SaveConfig { get; set; }

        public new string GetResource()
        {
            return "/mgmt/actionqueue/" + Domain;
        }
    }
}