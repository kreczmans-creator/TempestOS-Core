using Tempest.Core.Logging;

namespace Tempest.Core.Tests.Logging;

public class LogLevelTests
{
    [Fact]
    public void Values_AreOrderedByIncreasingSeverity()
    {
        var ordered = new[]
        {
            LogLevel.Trace,
            LogLevel.Debug,
            LogLevel.Information,
            LogLevel.Warning,
            LogLevel.Error,
            LogLevel.Critical,
            LogLevel.None,
        };

        for (var i = 0; i < ordered.Length - 1; i++)
            Assert.True(ordered[i] < ordered[i + 1], $"{ordered[i]} should be less than {ordered[i + 1]}");
    }
}
