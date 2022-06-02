using Newtonsoft.Json;

namespace Keyfactor.Extensions.Orchestrator.DataPower.Models.Requests
{
    public class ViewPublicCertificatesRequest : Request
    {
        public ViewPublicCertificatesRequest()
        {
            Method = "GET";
            Domain = "default";
            Folder = "pubcert";
        }

        public ViewPublicCertificatesRequest(string domain, string folder)
        {
            Method = "GET";
            Domain = domain;
            Folder = folder.Trim();
        }

        [JsonIgnore] public string Folder { get; set; }

        public new string GetResource()
        {
            return "/mgmt/filestore/" + Domain + "/" + Folder;
        }
    }
}