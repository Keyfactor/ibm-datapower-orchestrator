namespace Keyfactor.Extensions.Orchestrator.DataPower.Models.Requests
{
    public class DeleteCertificateRequest : Request
    {
        public DeleteCertificateRequest(string domain, string filename)
        {
            Domain = domain;
            Filename = filename.Trim();
            Method = "DELETE";
        }

        public new string GetResource()
        {
            return "/mgmt/filestore/" + Domain + "/cert/" + Filename;
        }
    }
}