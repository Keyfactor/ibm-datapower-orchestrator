using System;
using Keyfactor.Extensions.Orchestrator.DataPower.Client;
using Keyfactor.Extensions.Orchestrator.DataPower.Jobs;
using Keyfactor.Orchestrators.Extensions.Interfaces;

namespace DataPower.Tests.TestSupport
{
    // Lets tests substitute a mock IDataPowerClient for the one Discovery would
    // otherwise construct from real credentials.
    public class TestableDiscovery : Discovery
    {
        public Func<string, string, string, string, IDataPowerClient> ApiClientFactory { get; set; } =
            (_, _, _, _) => throw new InvalidOperationException("ApiClientFactory not configured for this test.");

        public TestableDiscovery(IPAMSecretResolver resolver) : base(resolver)
        {
        }

        protected internal override IDataPowerClient CreateApiClient(string user, string pass, string baseUrl,
            string domain)
        {
            return ApiClientFactory(user, pass, baseUrl, domain);
        }
    }
}
