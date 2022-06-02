namespace Keyfactor.Extensions.Orchestrator.DataPower.Models.Requests
{
    public class ViewCryptoCertificatesRequest : Request
    {
        public ViewCryptoCertificatesRequest(string domain)
        {
            Domain = domain;
            Method = "GET";
            Filename = "";
        }

        public ViewCryptoCertificatesRequest(string domain, string alias)
        {
            Domain = domain;
            Method = "GET";
            Filename = alias;
        }

        public new string GetResource()
        {
            if (string.IsNullOrEmpty(Filename))
                return "/mgmt/config/" + Domain + "/CryptoCertificate";
            return "/mgmt/config/" + Domain + "/CryptoCertificate/" + Filename;
        }
    }
}