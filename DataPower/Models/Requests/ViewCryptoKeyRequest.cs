namespace Keyfactor.Extensions.Orchestrator.DataPower.Models.Requests
{
    public class ViewCryptoKeyRequest : Request
    {
        public ViewCryptoKeyRequest(string domain, string alias)
        {
            Domain = domain;
            Method = "GET";
            Filename = alias;
        }

        public new string GetResource()
        {
            return "/mgmt/config/" + Domain + "/CryptoKey/" + Filename;
        }
    }
}