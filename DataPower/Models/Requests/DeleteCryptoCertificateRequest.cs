namespace Keyfactor.Extensions.Orchestrator.DataPower.Models.Requests
{
    public class DeleteCryptoCertificateRequest : Request
    {
        public DeleteCryptoCertificateRequest(string domain, string filename)
        {
            Domain = domain;
            Filename = filename.Trim();
            Method = "DELETE";
        }

        public new string GetResource()
        {
            return "/mgmt/config/" + Domain + "/CryptoCertificate/" + Filename;
        }
    }
}