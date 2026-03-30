namespace SharedBotKernel.Tests.Workers;

using SharedBotKernel.Workers;
using Xunit;

public sealed class BackgroundSyncWorkerBaseTests
{
    // ── CalculateDelay: base cases ────────────────────────────────────────────

    [Fact]
    public void CalculateDelay_ZeroFailures_ReturnsIntervalSeconds()
    {
        var delay = BackgroundSyncWorkerBase<object>.CalculateDelay(
            intervalSeconds: 30,
            maxBackoffSeconds: 300,
            backoffFactor: 2,
            failureStreak: 0);

        Assert.Equal(TimeSpan.FromSeconds(30), delay);
    }

    [Fact]
    public void CalculateDelay_OneFailure_DoublesInterval()
    {
        var delay = BackgroundSyncWorkerBase<object>.CalculateDelay(
            intervalSeconds: 30,
            maxBackoffSeconds: 300,
            backoffFactor: 2,
            failureStreak: 1);

        Assert.Equal(TimeSpan.FromSeconds(60), delay);
    }

    [Fact]
    public void CalculateDelay_TwoFailures_QuadruplesInterval()
    {
        var delay = BackgroundSyncWorkerBase<object>.CalculateDelay(
            intervalSeconds: 30,
            maxBackoffSeconds: 300,
            backoffFactor: 2,
            failureStreak: 2);

        Assert.Equal(TimeSpan.FromSeconds(120), delay);
    }

    [Fact]
    public void CalculateDelay_CapsAtMaxBackoff()
    {
        var delay = BackgroundSyncWorkerBase<object>.CalculateDelay(
            intervalSeconds: 30,
            maxBackoffSeconds: 60,
            backoffFactor: 2,
            failureStreak: 5);

        Assert.Equal(TimeSpan.FromSeconds(60), delay);
    }

    [Fact]
    public void CalculateDelay_NegativeFailureStreak_TreatedAsZero()
    {
        var delay = BackgroundSyncWorkerBase<object>.CalculateDelay(
            intervalSeconds: 30,
            maxBackoffSeconds: 300,
            backoffFactor: 2,
            failureStreak: -3);

        Assert.Equal(TimeSpan.FromSeconds(30), delay);
    }

    [Fact]
    public void CalculateDelay_BackoffFactorClampedToMin2()
    {
        // backoffFactor of 1 should be clamped to 2
        var delay = BackgroundSyncWorkerBase<object>.CalculateDelay(
            intervalSeconds: 10,
            maxBackoffSeconds: 200,
            backoffFactor: 1,
            failureStreak: 1);

        // clamped to 2, so 10 * 2 = 20
        Assert.Equal(TimeSpan.FromSeconds(20), delay);
    }

    [Fact]
    public void CalculateDelay_BackoffFactorClampedToMax4()
    {
        // backoffFactor of 10 should be clamped to 4
        var delay = BackgroundSyncWorkerBase<object>.CalculateDelay(
            intervalSeconds: 10,
            maxBackoffSeconds: 2000,
            backoffFactor: 10,
            failureStreak: 1);

        // clamped to 4, so 10 * 4 = 40
        Assert.Equal(TimeSpan.FromSeconds(40), delay);
    }

    [Fact]
    public void CalculateDelay_IntervalClampedTo1Second_WhenBelowMin()
    {
        var delay = BackgroundSyncWorkerBase<object>.CalculateDelay(
            intervalSeconds: 0,
            maxBackoffSeconds: 300,
            backoffFactor: 2,
            failureStreak: 0);

        Assert.Equal(TimeSpan.FromSeconds(1), delay);
    }

    [Fact]
    public void CalculateDelay_MaxFailureStreakOf10_DoesNotExceedMaxBackoff()
    {
        var delay = BackgroundSyncWorkerBase<object>.CalculateDelay(
            intervalSeconds: 30,
            maxBackoffSeconds: 300,
            backoffFactor: 2,
            failureStreak: 10);

        Assert.Equal(TimeSpan.FromSeconds(300), delay);
    }
}
