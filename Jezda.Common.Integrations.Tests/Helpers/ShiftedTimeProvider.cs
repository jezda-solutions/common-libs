namespace Jezda.Common.Integrations.Tests.Helpers;

/// <summary>
/// The system clock moved forward by <see cref="Shift"/>, so a test can pass a circuit breaker's
/// break duration without waiting. Timers and timestamps stay real.
/// </summary>
public sealed class ShiftedTimeProvider : TimeProvider
{
    public TimeSpan Shift { get; set; }

    public override DateTimeOffset GetUtcNow() => System.GetUtcNow() + Shift;
}
