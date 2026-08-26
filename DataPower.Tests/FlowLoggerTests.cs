using System;
using System.Threading.Tasks;
using Keyfactor.Extensions.Orchestrator.DataPower;
using Microsoft.Extensions.Logging.Abstractions;

namespace DataPower.Tests
{
    public class FlowLoggerTests
    {
        private static FlowLogger NewLogger() => new FlowLogger(NullLogger.Instance, "Test-Flow");

        [Fact]
        public async Task StepAsync_RunsActionAndRecordsSuccess()
        {
            using var flow = NewLogger();
            var ran = false;
            await flow.StepAsync("AsyncStep", async () =>
            {
                await Task.Yield();
                ran = true;
            }, "detail");

            Assert.True(ran);
            Assert.False(flow.HasFailures);
        }

        [Fact]
        public async Task StepAsync_ThrowingAction_RecordsFailureAndRethrows()
        {
            using var flow = NewLogger();
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                flow.StepAsync("AsyncBoom", () => throw new InvalidOperationException("async kaboom")));

            Assert.True(flow.HasFailures);
        }

        [Fact]
        public void Step_WithoutAction_RecordsSuccess()
        {
            using var flow = NewLogger();
            flow.Step("SimpleStep", "some detail");

            Assert.False(flow.HasFailures);
            Assert.Contains("SimpleStep", flow.GetSummary());
            Assert.Contains("some detail", flow.GetSummary());
        }

        [Fact]
        public void Step_WithAction_RunsActionAndRecordsSuccess()
        {
            using var flow = NewLogger();
            var ran = false;
            flow.Step("DoThing", () => { ran = true; });

            Assert.True(ran);
            Assert.False(flow.HasFailures);
        }

        [Fact]
        public void Step_WithThrowingAction_RecordsFailureAndRethrows()
        {
            using var flow = NewLogger();

            Assert.Throws<InvalidOperationException>(() =>
                flow.Step("Boom", () => throw new InvalidOperationException("kaboom")));

            Assert.True(flow.HasFailures);
            Assert.Contains("kaboom", flow.GetSummary());
        }

        [Fact]
        public void StepT_ReturnsActionResult()
        {
            using var flow = NewLogger();
            var result = flow.Step("Compute", () => 42);
            Assert.Equal(42, result);
        }

        [Fact]
        public void StepT_WithThrowingFunc_RecordsFailureAndRethrows()
        {
            using var flow = NewLogger();
            Assert.Throws<InvalidOperationException>(() =>
                flow.Step<int>("Boom", () => throw new InvalidOperationException("nope")));
            Assert.True(flow.HasFailures);
        }

        [Fact]
        public void Fail_RecordsFailureWithoutThrowing()
        {
            using var flow = NewLogger();
            flow.Fail("SomeStep", "it broke");

            Assert.True(flow.HasFailures);
            Assert.Contains("it broke", flow.GetSummary());
        }

        [Fact]
        public void Skip_RecordsSkippedStep_NotCountedAsFailure()
        {
            using var flow = NewLogger();
            flow.Skip("SkippedStep", "not applicable");

            Assert.False(flow.HasFailures);
            Assert.Contains("SkippedStep", flow.GetSummary());
            Assert.Contains("[SKIP]", flow.GetSummary());
        }

        [Fact]
        public void BranchAndEndBranch_DoNotThrow_AndAreBalanced()
        {
            using var flow = NewLogger();
            flow.Branch("PerDomain");
            flow.Step("Inner");
            flow.EndBranch();
            flow.EndBranch(); // extra EndBranch on empty stack should be a no-op, not throw

            Assert.False(flow.HasFailures);
        }

        [Fact]
        public void GetSummary_IncludesCountsForEachStatus()
        {
            using var flow = NewLogger();
            flow.Step("Ok1");
            flow.Skip("Skip1", "reason");
            flow.Fail("Fail1", "reason");

            var summary = flow.GetSummary();
            Assert.Contains("3 total", summary);
            Assert.Contains("1 ok", summary);
            Assert.Contains("1 failed", summary);
            Assert.Contains("1 skipped", summary);
        }

        [Fact]
        public void GetSummary_TruncatesWhenOverMaxLength()
        {
            using var flow = NewLogger();
            for (var i = 0; i < 500; i++)
                flow.Step($"Step-{i}", $"some reasonably long detail string number {i}");

            var summary = flow.GetSummary();
            Assert.True(summary.Length <= 3500 + 200);
            Assert.Contains("truncated", summary);
        }

        [Fact]
        public void Constructor_ThrowsOnNullLogger()
        {
            Assert.Throws<ArgumentNullException>(() => new FlowLogger(null!, "flow"));
        }

        [Fact]
        public void Constructor_ThrowsOnNullFlowName()
        {
            Assert.Throws<ArgumentNullException>(() => new FlowLogger(NullLogger.Instance, null!));
        }
    }
}
