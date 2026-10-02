using Tempest.Core.Invoicing.Xero.Sync.Purchasing;

namespace Tempest.Core.Invoicing.Xero.Sync;

/// <summary>
/// How long the sync engine (`v0.24.0` X6, design §6.3, §6.5) waits before
/// sending an entry again: exponential backoff with jitter for a transport
/// failure or a 5xx; Xero's own <c>Retry-After</c> (+1 s) for a 429; and a
/// short, capped schedule for a write whose answer may have been lost, so it
/// is recovered while Xero still holds its <c>Idempotency-Key</c>.
/// </summary>
/// <remarks>
/// Pure: the jitter is a number in <c>[0, 1)</c> the caller supplies (the
/// engine draws it from an injectable source), so tests are exact.
/// </remarks>
public static class XeroBackoff
{
    /// <summary>The first retry's nominal delay (design §6.3: 30 s).</summary>
    public static readonly TimeSpan BaseDelay = TimeSpan.FromSeconds(30);

    /// <summary>The longest nominal delay (design §6.3: 30 minutes).</summary>
    public static readonly TimeSpan MaximumDelay = TimeSpan.FromMinutes(30);

    /// <summary>The jitter either side of the nominal delay (design §6.3: ±20 %).</summary>
    public const double JitterFraction = 0.2;

    /// <summary>The margin added to a 429's <c>Retry-After</c> (design §6.5: +1 s).</summary>
    public static readonly TimeSpan RetryAfterMargin = TimeSpan.FromSeconds(1);

    /// <summary>The pause after a 429 that carried no <c>Retry-After</c> (design §6.5: 60 s).</summary>
    public static readonly TimeSpan DefaultRateLimitPause = TimeSpan.FromSeconds(60);

    /// <summary>The first retry of a write whose answer may have been lost (<see cref="RecoveryDelay"/>).</summary>
    public static readonly TimeSpan RecoveryBaseDelay = TimeSpan.FromSeconds(5);

    /// <summary>
    /// The longest wait between two recovery attempts of a write whose answer
    /// may have been lost (<see cref="RecoveryDelay"/>): short enough that
    /// several attempts fit inside
    /// <see cref="XeroPurchasingOwnership.IdempotencyKeyLifetime"/>.
    /// </summary>
    public static readonly TimeSpan RecoveryMaximumDelay = TimeSpan.FromSeconds(45);

    /// <summary>
    /// The backoff before attempt <paramref name="attempts"/> + 1 of an entry
    /// that failed for a transport reason or a 5xx:
    /// <c>min(30 s × 2^(attempts−1), 30 min)</c> ± 20 % jitter.
    /// </summary>
    /// <param name="attempts">How many times the entry has been sent (1 after its first attempt). Values below 1 count as 1.</param>
    /// <param name="jitter">A number in <c>[0, 1)</c>: 0 gives −20 %, 0.5 the nominal delay, just under 1 almost +20 %. Clamped into range.</param>
    public static TimeSpan Delay(int attempts, double jitter)
    {
        var exponent = Math.Clamp(attempts, 1, 32) - 1;
        var nominalSeconds = Math.Min(BaseDelay.TotalSeconds * Math.Pow(2, exponent), MaximumDelay.TotalSeconds);
        return Jittered(nominalSeconds, jitter);
    }

    /// <summary>
    /// The wait before re-trying a write whose answer may have been lost (an
    /// entry found <see cref="XeroOutboxState.InFlight"/> at start-up, an
    /// <see cref="XeroPushOutcome.Unknown"/> answer, or a create that failed
    /// in transport): <c>min(5 s × 2^(n−1), 45 s)</c> ± 20 %, so the record
    /// is looked up — and, for a purchase order or bill, its create replayed
    /// — while Xero still holds the key.
    /// </summary>
    /// <param name="recoveryAttempts">How many recovery attempts have been made so far (1 after the first). Values below 1 count as 1.</param>
    /// <param name="jitter">A number in <c>[0, 1)</c>, as for <see cref="Delay"/>.</param>
    public static TimeSpan RecoveryDelay(int recoveryAttempts, double jitter)
    {
        var exponent = Math.Clamp(recoveryAttempts, 1, 16) - 1;
        var nominalSeconds = Math.Min(RecoveryBaseDelay.TotalSeconds * Math.Pow(2, exponent), RecoveryMaximumDelay.TotalSeconds);
        return Jittered(nominalSeconds, jitter);
    }

    /// <summary>How long every send pauses after a 429: its <c>Retry-After</c> + 1 s, or 60 s when it gave none.</summary>
    /// <param name="retryAfter">The 429's <c>Retry-After</c>; <see langword="null"/> when absent.</param>
    public static TimeSpan RateLimitPause(TimeSpan? retryAfter) =>
        retryAfter is { } wait && wait >= TimeSpan.Zero ? wait + RetryAfterMargin : DefaultRateLimitPause;

    private static TimeSpan Jittered(double nominalSeconds, double jitter)
    {
        var unit = double.IsNaN(jitter) ? 0.5 : Math.Clamp(jitter, 0d, 1d);
        var factor = 1d + ((unit * 2d) - 1d) * JitterFraction;
        return TimeSpan.FromMilliseconds(Math.Round(nominalSeconds * factor * 1000d));
    }
}
