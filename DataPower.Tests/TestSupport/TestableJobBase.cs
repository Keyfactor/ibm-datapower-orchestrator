using System;
using Keyfactor.Extensions.Orchestrator.DataPower.Client;
using Keyfactor.Extensions.Orchestrator.DataPower.Jobs;
using Keyfactor.Logging;
using Keyfactor.Orchestrators.Extensions.Interfaces;

namespace DataPower.Tests.TestSupport
{
    // Exposes JobBase's protected members for direct unit testing, and lets tests
    // substitute a mock IDataPowerClient for CreateApiClient.
    public class TestableJobBase : JobBase
    {
        public Func<string, string, string, string, IDataPowerClient>? ApiClientFactory { get; set; }

        public TestableJobBase(IPAMSecretResolver resolver) : base(resolver)
        {
            Logger = LogHandler.GetClassLogger<TestableJobBase>();
        }

        public string PublicResolvePamField(string name, string value) => ResolvePamField(name, value);
        public Keyfactor.Orchestrators.Extensions.JobResult PublicSuccessResult(long id, string message = "") =>
            SuccessResult(id, message);
        public Keyfactor.Orchestrators.Extensions.JobResult PublicWarningResult(long id, string message) =>
            WarningResult(id, message);
        public Keyfactor.Orchestrators.Extensions.JobResult PublicFailureResult(long id, string message,
            Keyfactor.Extensions.Orchestrator.DataPower.FlowLogger? flow = null) =>
            FailureResult(id, message, flow);
        public static string PublicDescribeException(Exception ex) => DescribeException(ex);

        protected internal override IDataPowerClient CreateApiClient(string user, string pass, string baseUrl,
            string domain)
        {
            return ApiClientFactory != null
                ? ApiClientFactory(user, pass, baseUrl, domain)
                : base.CreateApiClient(user, pass, baseUrl, domain);
        }

        public IDataPowerClient CreateApiClientForTest(string user, string pass, string baseUrl, string domain) =>
            CreateApiClient(user, pass, baseUrl, domain);
    }
}
