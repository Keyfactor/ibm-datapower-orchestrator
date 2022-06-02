using Newtonsoft.Json;

namespace Keyfactor.Extensions.Orchestrator.DataPower.Models.Requests
{
    public class CertificateAddRequest : Request
    {
        public CertificateAddRequest(string domain, string filename, string folder)
        {
            Certificate = new CertificateRequest();
            Domain = domain;
            Filename = filename.Trim();
            Folder = folder.Trim();
            Method = "PUT";
        }

        [JsonIgnore] public string Folder { get; set; }

        [JsonProperty("file")] public CertificateRequest Certificate { get; set; }

        public new string GetResource()
        {
            return "/mgmt/filestore/" + Domain + "/" + Folder + "/" + Filename;
        }
    }
}