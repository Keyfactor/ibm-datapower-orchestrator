using System;
using Keyfactor.Extensions.Orchestrator.DataPower.Client;
using Keyfactor.Extensions.Orchestrator.DataPower.Jobs;
using Keyfactor.Orchestrators.Extensions.Interfaces;

namespace DataPower.Tests.TestSupport
{
    public class TestableInventory : Inventory
    {
        public Func<string, string, string, string, IDataPowerClient> ApiClientFactory { get; set; } =
            (_, _, _, _) => throw new InvalidOperationException("ApiClientFactory not configured for this test.");

        public TestableInventory(IPAMSecretResolver resolver) : base(resolver)
        {
        }

        protected internal override IDataPowerClient CreateApiClient(string user, string pass, string baseUrl,
            string domain)
        {
            return ApiClientFactory(user, pass, baseUrl, domain);
        }
    }
}
