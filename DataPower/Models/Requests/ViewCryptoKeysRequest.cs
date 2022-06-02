namespace Keyfactor.Extensions.Orchestrator.DataPower.Models.Requests
{
    public class ViewCryptoKeysRequest : Request
    {
        public ViewCryptoKeysRequest(string domain)
        {
            Domain = domain;
            Method = "GET";
        }

        public new string GetResource()
        {
            return "/mgmt/config/" + Domain + "/CryptoKey";
        }
    }
}