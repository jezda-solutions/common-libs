using Jezda.Common.Integrations.Abstractions.Resilience;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Jezda.Common.Integrations.Tests.DependencyInjection;

public class PipelineKeyBudgetTests
{
    private readonly PipelineKeyBudget _budget = new("test-client", IntegrationPipelineKeys.Address);

    [Fact]
    public void UnprovenKeys_BeyondTheirBudget_ShareTheOverflowKey()
    {
        for (var i = 0; i < PipelineKeyBudget.MaxUnprovenKeys; i++)
        {
            Assert.Equal($"https://site{i}.example.test", Select($"https://site{i}.example.test/x"));
        }

        Assert.Equal(PipelineKeyBudget.OverflowKey, Select("https://late.example.test/x"));
    }

    [Fact]
    public void AdmittedKey_KeepsItsPipeline_AfterTheBudgetIsUsed()
    {
        Select("https://first.example.test/x");
        FillUnproven();

        Assert.Equal("https://first.example.test", Select("https://first.example.test/y"));
    }

    [Fact]
    public void ProvenKey_IsAdmitted_AfterTheUnprovenBudgetIsUsed()
    {
        FillUnproven();
        Assert.Equal(PipelineKeyBudget.OverflowKey, Select("https://up.example.test/x"));

        RecordSuccess("https://up.example.test/x");

        Assert.Equal("https://up.example.test", Select("https://up.example.test/x"));
    }

    [Fact]
    public void ProvenKeys_BeyondTheBudget_ShareTheOverflowKey()
    {
        // The bound on memory and on the metric series Polly tags with the key.
        FillUnproven();
        for (var i = PipelineKeyBudget.MaxUnprovenKeys; i < PipelineKeyBudget.MaxKeys; i++)
        {
            RecordSuccess($"https://site{i}.example.test/x");
            Assert.Equal($"https://site{i}.example.test", Select($"https://site{i}.example.test/x"));
        }

        RecordSuccess("https://late.example.test/x");

        Assert.Equal(PipelineKeyBudget.OverflowKey, Select("https://late.example.test/x"));
    }

    [Fact]
    public void ProvenRecords_StayWithinTheBudget()
    {
        // Successes recorded but never followed by a request would otherwise grow the record:
        // with 100 keys admitted, only 900 more fit.
        FillUnproven();
        for (var i = 0; i < PipelineKeyBudget.MaxKeys; i++)
        {
            RecordSuccess($"https://proven{i}.example.test/x");
        }

        Assert.Equal("https://proven0.example.test", Select("https://proven0.example.test/x"));
        Assert.Equal(PipelineKeyBudget.OverflowKey, Select("https://proven950.example.test/x"));
    }

    [Fact]
    public void FilledUnprovenBudget_LogsOneWarning()
    {
        var logger = new ListLogger();
        _budget.Logger = logger;
        FillUnproven();

        Select("https://late1.example.test/x");
        Select("https://late2.example.test/x");

        var warning = Assert.Single(logger.Messages);
        Assert.Contains("test-client", warning);
        Assert.Contains("never answered successfully", warning);
    }

    private sealed class ListLogger : ILogger
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Messages.Add(formatter(state, exception));
    }

    private void FillUnproven()
    {
        for (var i = 0; i < PipelineKeyBudget.MaxUnprovenKeys; i++)
        {
            Select($"https://site{i}.example.test/x");
        }
    }

    private string Select(string url)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        return _budget.Select(request);
    }

    private void RecordSuccess(string url)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        _budget.RecordSuccess(request);
    }
}
