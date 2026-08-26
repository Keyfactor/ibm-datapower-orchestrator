using System;
using DataPower.Tests.TestSupport;
using Keyfactor.Extensions.Orchestrator.DataPower.Client;
using Keyfactor.Orchestrators.Common.Enums;
using Moq;
using System.Net;

namespace DataPower.Tests
{
    public class JobBaseTests
    {
        private static TestableJobBase NewJob(Mock<Keyfactor.Orchestrators.Extensions.Interfaces.IPAMSecretResolver>? resolver = null)
        {
            resolver ??= new Mock<Keyfactor.Orchestrators.Extensions.Interfaces.IPAMSecretResolver>();
            return new TestableJobBase(resolver.Object);
        }

        [Fact]
        public void Constructor_ThrowsOnNullResolver()
        {
            Assert.Throws<ArgumentNullException>(() => new TestableJobBase(null!));
        }

        [Fact]
        public void ResolvePamField_EmptyValue_ReturnsAsIsWithoutCallingResolver()
        {
            var resolverMock = new Mock<Keyfactor.Orchestrators.Extensions.Interfaces.IPAMSecretResolver>();
            var job = NewJob(resolverMock);

            var result = job.PublicResolvePamField("ServerPassword", "");

            Assert.Equal("", result);
            resolverMock.Verify(r => r.Resolve(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public void ResolvePamField_NonEmptyValue_DelegatesToResolver()
        {
            var resolverMock = new Mock<Keyfactor.Orchestrators.Extensions.Interfaces.IPAMSecretResolver>();
            resolverMock.Setup(r => r.Resolve("pam-ref")).Returns("actual-secret");
            var job = NewJob(resolverMock);

            var result = job.PublicResolvePamField("ServerPassword", "pam-ref");

            Assert.Equal("actual-secret", result);
        }

        [Fact]
        public void SuccessResult_SetsSuccessAndMessage()
        {
            var job = NewJob();
            var result = job.PublicSuccessResult(42, "all good");

            Assert.Equal(OrchestratorJobStatusJobResult.Success, result.Result);
            Assert.Equal(42, result.JobHistoryId);
            Assert.Equal("all good", result.FailureMessage);
        }

        [Fact]
        public void WarningResult_SetsWarning()
        {
            var job = NewJob();
            var result = job.PublicWarningResult(7, "partial failure");

            Assert.Equal(OrchestratorJobStatusJobResult.Warning, result.Result);
            Assert.Equal("partial failure", result.FailureMessage);
        }

        [Fact]
        public void FailureResult_WithoutFlow_UsesMessageAsIs()
        {
            var job = NewJob();
            var result = job.PublicFailureResult(1, "boom");

            Assert.Equal(OrchestratorJobStatusJobResult.Failure, result.Result);
            Assert.Equal("boom", result.FailureMessage);
        }

        [Fact]
        public void FailureResult_WithNullMessage_DefaultsToUnknownError()
        {
            var job = NewJob();
            var result = job.PublicFailureResult(1, null!);

            Assert.Contains("Unknown error", result.FailureMessage);
        }

        [Fact]
        public void FailureResult_WithFlow_AppendsSummary()
        {
            var job = NewJob();
            using var flow = new Keyfactor.Extensions.Orchestrator.DataPower.FlowLogger(
                Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance, "Test");
            flow.Step("SomeStep");

            var result = job.PublicFailureResult(1, "boom", flow);

            Assert.Contains("boom", result.FailureMessage);
            Assert.Contains("SomeStep", result.FailureMessage);
        }

        [Fact]
        public void DescribeException_PrefersDataPowerApiExceptionDetail()
        {
            var apiEx = new DataPowerApiException("bad request", HttpStatusCode.BadRequest, "AddCryptoCertificate",
                "{\"error\":\"duplicate\"}");

            var described = TestableJobBase.PublicDescribeException(apiEx);

            Assert.Contains("400", described);
            Assert.Contains("AddCryptoCertificate", described);
            Assert.Contains("duplicate", described);
        }

        [Fact]
        public void DescribeException_FallsBackToPlainMessage_WhenNoApiException()
        {
            var ex = new InvalidOperationException("plain failure");
            var described = TestableJobBase.PublicDescribeException(ex);
            Assert.Equal("plain failure", described);
        }

        [Fact]
        public void DescribeException_ReturnsUnknownError_ForNull()
        {
            Assert.Equal("Unknown error", TestableJobBase.PublicDescribeException(null!));
        }

        [Fact]
        public void DescribeException_AggregateExceptionWithoutApiException_UsesFirstInnerMessage()
        {
            var agg = new AggregateException(new InvalidOperationException("first"), new Exception("second"));
            Assert.Equal("first", TestableJobBase.PublicDescribeException(agg));
        }

        [Fact]
        public void DescribeException_TruncatesLongResponseBody()
        {
            var longBody = new string('x', 600);
            var apiEx = new DataPowerApiException("bad", HttpStatusCode.BadRequest, "AddCryptoCertificate", longBody);

            var described = TestableJobBase.PublicDescribeException(apiEx);

            Assert.Contains("...", described);
            Assert.DoesNotContain(new string('x', 600), described);
        }

        [Fact]
        public void CreateApiClient_DefaultsToRealDataPowerClient()
        {
            var job = NewJob();
            var client = job.CreateApiClientForTest("user", "pass", "https://dp:5554", "default");

            Assert.IsType<Keyfactor.Extensions.Orchestrator.DataPower.Client.DataPowerClient>(client);
            Assert.Equal("default", client.Domain);
        }
    }
}
