namespace Keyfactor.Extensions.Orchestrator.DataPower.Models.Requests
{
    public class DeleteCryptoKeyRequest : Request
    {
        public DeleteCryptoKeyRequest(string domain, string filename)
        {
            Domain = domain;
            Filename = filename.Trim();
            Method = "DELETE";
        }

        public new string GetResource()
        {
            return "/mgmt/config/" + Domain + "/CryptoKey/" + Filename;
        }
    }
}