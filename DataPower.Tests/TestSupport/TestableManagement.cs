using Keyfactor.Extensions.Orchestrator.DataPower;
using Keyfactor.Extensions.Orchestrator.DataPower.Jobs;
using Keyfactor.Orchestrators.Extensions.Interfaces;

namespace DataPower.Tests.TestSupport
{
    public class TestableManagement : Management
    {
        public TestableManagement(IPAMSecretResolver resolver) : base(resolver)
        {
        }

        public RequestManager PublicCertManager => CertManager;
    }
}
