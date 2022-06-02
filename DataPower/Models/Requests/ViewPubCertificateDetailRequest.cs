namespace Keyfactor.Extensions.Orchestrator.DataPower.Models.Requests
{
    public class ViewPubCertificateDetailRequest : Request
    {
        public ViewPubCertificateDetailRequest(string filename)
        {
            Domain = "pubcert";
            Filename = filename.Trim();
            Method = "GET";
        }

        public new string GetResource()
        {
            return "/mgmt/filestore/default/pubcert/" + Filename;
        }
    }
}