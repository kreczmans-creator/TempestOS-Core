using System.Globalization;
using System.Net;

namespace Tempest.Core.Invoicing.Xero.Api;

/// <summary>
/// TempestOS's own side of Xero's rate limits (`v0.24.0` task B1,
/// <c>docs/releases/v0.24.0/Xero Technical Design.md</c> §6.5): a
/// <see cref="DelegatingHandler"/> in the Xero <see cref="HttpClient"/>
/// pipeline, below <see cref="XeroWriteSafetyHandler"/>, so every caller —
/// the sync drain, the dashboard refresh, the invoice send — shares one
/// count. It never waits: a call it holds back is answered at once with a
/// synthetic <b>429</b> (<see cref="ThrottledHeader"/>, <c>Retry-After</c>,
/// <c>X-Rate-Limit-Problem</c>), which every caller already treats as
/// "retry later".
/// </summary>
/// <remarks>
/// <para>
/// <b>Holds back a call when</b> TempestOS has made
/// <see cref="ClientCallsPerMinute"/> calls in the last minute (Xero allows
/// 60 per tenant; 50 leaves room for a refresh racing a drain); Xero's last
/// <c>X-MinLimit-Remaining</c> was <see cref="MinuteRemainingFloor"/> or
/// fewer (paused until the oldest call of the minute ages out); or Xero
/// answered 429 (paused for exactly its <c>Retry-After</c> + 1 s, or 60 s
/// when absent).
/// </para>
/// <para>
/// <b>Records</b> every response's <c>X-MinLimit-Remaining</c>,
/// <c>X-DayLimit-Remaining</c> and <c>X-AppMinLimit-Remaining</c> as
/// <see cref="LastReading"/>. One connected tenant at a time, so one count.
/// Time is the injected <see cref="TimeProvider"/>, so tests never sleep.
/// </para>
/// </remarks>
public sealed class XeroRateLimiter : DelegatingHandler
{
    /// <summary>The most calls TempestOS makes in any rolling minute.</summary>
    public const int ClientCallsPerMinute = 50;

    /// <summary>At or below this <c>X-MinLimit-Remaining</c>, TempestOS pauses to the next minute.</summary>
    public const int MinuteRemainingFloor = 2;

    /// <summary>The header on a synthetic 429 that TempestOS (not Xero) held the call back.</summary>
    public const string ThrottledHeader = "X-Tempest-Throttled";

    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan RetryAfterMargin = TimeSpan.FromSeconds(1);

    private readonly TimeProvider _time;
    private readonly object _gate = new();
    private readonly Queue<DateTimeOffset> _calls = new();
    private DateTimeOffset? _pausedUntil;
    private string? _pauseProblem;
    private XeroRateLimitReading? _lastReading;

    /// <summary>Initialises a new instance of the <see cref="XeroRateLimiter"/> class. Its <see cref="DelegatingHandler.InnerHandler"/> is set by the pipeline's composer.</summary>
    /// <param name="timeProvider">The clock; <see langword="null"/> for the system clock.</param>
    public XeroRateLimiter(TimeProvider? timeProvider = null)
    {
        _time = timeProvider ?? TimeProvider.System;
    }

    /// <summary>The rate-limit headers of the last Xero response; <see langword="null"/> before the first.</summary>
    public XeroRateLimitReading? LastReading
    {
        get { lock (_gate) return _lastReading; }
    }

    /// <summary>Until when every call is held back (a 429 or a nearly spent minute); <see langword="null"/> when not paused.</summary>
    public DateTimeOffset? PausedUntilUtc
    {
        get
        {
            lock (_gate)
            {
                var now = _time.GetUtcNow();
                return _pausedUntil is { } until && until > now ? until : null;
            }
        }
    }

    /// <summary>How long until a call would be let through (<see cref="TimeSpan.Zero"/> when one would be now) — what the sync drain waits for before its next request.</summary>
    public TimeSpan DelayBeforeNextCall()
    {
        lock (_gate)
            return Wait(_time.GetUtcNow(), out _) ?? TimeSpan.Zero;
    }

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        TimeSpan? wait;
        string? problem;

        lock (_gate)
        {
            var now = _time.GetUtcNow();
            wait = Wait(now, out problem);
            if (wait is null)
                _calls.Enqueue(now);
        }

        if (wait is { } delay)
            return Throttled(request, delay, problem ?? "minute");

        var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        Observe(response);

        return response;
    }

    /// <summary>The wait before the next call, or <see langword="null"/> when it may go now. Caller holds <see cref="_gate"/>.</summary>
    private TimeSpan? Wait(DateTimeOffset now, out string? problem)
    {
        while (_calls.Count > 0 && _calls.Peek() <= now - Window)
            _calls.Dequeue();

        TimeSpan? wait = null;
        problem = null;

        if (_pausedUntil is { } until && until > now)
        {
            wait = until - now;
            problem = _pauseProblem;
        }

        if (_calls.Count >= ClientCallsPerMinute)
        {
            var slotFrees = _calls.Peek() + Window - now;
            if (wait is null || slotFrees > wait)
            {
                wait = slotFrees;
                problem = "minute";
            }
        }

        return wait;
    }

    private void Observe(HttpResponseMessage response)
    {
        var reading = new XeroRateLimitReading(
            ReadInt(response, "X-MinLimit-Remaining"),
            ReadInt(response, "X-DayLimit-Remaining"),
            ReadInt(response, "X-AppMinLimit-Remaining"),
            _time.GetUtcNow());

        lock (_gate)
        {
            var now = reading.ReadAtUtc;

            if (reading.MinuteRemaining is not null || reading.DayRemaining is not null || reading.AppMinuteRemaining is not null)
                _lastReading = reading;

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                var retryAfter = response.Headers.RetryAfter?.Delta
                    ?? (response.Headers.RetryAfter?.Date is { } date ? (date > now ? date - now : TimeSpan.Zero) : (TimeSpan?)null);
                var problem = response.Headers.TryGetValues("X-Rate-Limit-Problem", out var values) ? values.FirstOrDefault() : null;

                Pause(now + (retryAfter is { } delay ? delay + RetryAfterMargin : Window), problem ?? "minute");
            }
            else if (reading.MinuteRemaining is { } remaining && remaining <= MinuteRemainingFloor)
            {
                var oldest = _calls.Count > 0 ? _calls.Peek() : now;
                Pause(oldest + Window, "minute");
            }
        }
    }

    /// <summary>Extends the pause to <paramref name="until"/> (never shortens one already longer). Caller holds <see cref="_gate"/>.</summary>
    private void Pause(DateTimeOffset until, string problem)
    {
        if (_pausedUntil is { } current && current >= until)
            return;

        _pausedUntil = until;
        _pauseProblem = problem;
    }

    private static int? ReadInt(HttpResponseMessage response, string header) =>
        response.Headers.TryGetValues(header, out var values)
        && int.TryParse(values.FirstOrDefault(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;

    private static HttpResponseMessage Throttled(HttpRequestMessage request, TimeSpan wait, string problem)
    {
        var seconds = Math.Max(1, (int)Math.Ceiling(wait.TotalSeconds));

        var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests) { RequestMessage = request, Content = new StringContent(string.Empty) };
        response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(seconds));
        response.Headers.Add("X-Rate-Limit-Problem", problem);
        response.Headers.Add(ThrottledHeader, "client");

        return response;
    }
}
