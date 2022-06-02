using Newtonsoft.Json;

namespace Keyfactor.Extensions.Orchestrator.DataPower.Models.Requests
{
    public class ViewCertificateDetailRequest : Request
    {
        public ViewCertificateDetailRequest(string domain)
        {
            Domain = domain;
            Method = "POST";
        }

        [JsonProperty("ViewCertificateDetails")]
        public CertificateObjectRequest CertObjectRequest { get; set; }

        public new string GetResource()
        {
            return "/mgmt/actionqueue/" + Domain;
        }
    }
}