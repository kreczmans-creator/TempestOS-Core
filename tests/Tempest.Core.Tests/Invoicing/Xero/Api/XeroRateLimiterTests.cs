using System.Net;
using Tempest.Core.Invoicing;
using Tempest.Core.Invoicing.Xero.Api;

namespace Tempest.Core.Tests.Invoicing.Xero.Api;

/// <summary>
/// `v0.24.0` §6.5: <see cref="XeroRateLimiter"/> caps TempestOS at 50 calls a
/// minute, pauses to the next minute when Xero says 2 or fewer remain, and
/// pauses for exactly a 429's <c>Retry-After</c> + 1 s (60 s when absent) —
/// all on a hand-moved clock, never by sleeping.
/// </summary>
public sealed class XeroRateLimiterTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 2, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task TheFiftyFirstCallInAMinute_IsHeldBack_WithASynthetic429_AndNeverReachesXero()
    {
        var (clock, network, limiter, client) = Build();

        for (var i = 0; i < XeroRateLimiter.ClientCallsPerMinute; i++)
        {
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("Invoices")).StatusCode);
            clock.Advance(TimeSpan.FromMilliseconds(100));
        }

        var held = await client.GetAsync("Invoices");

        Assert.Equal(HttpStatusCode.TooManyRequests, held.StatusCode);
        Assert.Equal("client", held.Headers.GetValues(XeroRateLimiter.ThrottledHeader).Single());
        Assert.Equal("minute", held.Headers.GetValues("X-Rate-Limit-Problem").Single());
        Assert.Equal(TimeSpan.FromSeconds(55), held.Headers.RetryAfter!.Delta);
        Assert.Equal(XeroRateLimiter.ClientCallsPerMinute, network.Received.Count);
        Assert.True(limiter.DelayBeforeNextCall() > TimeSpan.Zero);

        clock.Advance(TimeSpan.FromSeconds(55));

        Assert.Equal(TimeSpan.Zero, limiter.DelayBeforeNextCall());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("Invoices")).StatusCode);
    }

    [Fact]
    public async Task AHeldBackCall_IsNotCountedAgainstTheMinute()
    {
        var (clock, network, _, client) = Build();
        for (var i = 0; i < XeroRateLimiter.ClientCallsPerMinute; i++)
            await client.GetAsync("Invoices");

        for (var i = 0; i < 10; i++)
            Assert.Equal(HttpStatusCode.TooManyRequests, (await client.GetAsync("Invoices")).StatusCode);

        clock.Advance(TimeSpan.FromMinutes(1));

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("Invoices")).StatusCode);
        Assert.Equal(XeroRateLimiter.ClientCallsPerMinute + 1, network.Received.Count);
    }

    [Fact]
    public async Task XeroSayingTwoCallsRemain_PausesUntilTheOldestCallOfTheMinuteAgesOut()
    {
        var (clock, network, limiter, client) = Build();
        await client.GetAsync("Invoices");
        clock.Advance(TimeSpan.FromSeconds(20));
        network.Respond = _ => WithHeaders(HttpStatusCode.OK, ("X-MinLimit-Remaining", "2"), ("X-DayLimit-Remaining", "4321"), ("X-AppMinLimit-Remaining", "9000"));

        await client.GetAsync("Invoices");

        Assert.Equal(new XeroRateLimitReading(2, 4321, 9000, clock.Now), limiter.LastReading);
        Assert.Equal(Start + TimeSpan.FromMinutes(1), limiter.PausedUntilUtc);
        var held = await client.GetAsync("Invoices");
        Assert.Equal(HttpStatusCode.TooManyRequests, held.StatusCode);
        Assert.Equal(TimeSpan.FromSeconds(40), held.Headers.RetryAfter!.Delta);

        clock.Advance(TimeSpan.FromSeconds(40));

        Assert.Null(limiter.PausedUntilUtc);
        network.Respond = _ => WithHeaders(HttpStatusCode.OK, ("X-MinLimit-Remaining", "59"));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("Invoices")).StatusCode);
    }

    [Fact]
    public async Task A429WithRetryAfter_PausesForExactlyThatPlusOneSecond_KeepingXerosProblem()
    {
        var (clock, network, limiter, client) = Build();
        network.Respond = _ => RateLimited(TimeSpan.FromSeconds(30), "day");

        var first = await client.GetAsync("Invoices");

        Assert.Equal(HttpStatusCode.TooManyRequests, first.StatusCode);
        Assert.False(first.Headers.Contains(XeroRateLimiter.ThrottledHeader));
        Assert.Equal(clock.Now + TimeSpan.FromSeconds(31), limiter.PausedUntilUtc);

        network.Respond = _ => new HttpResponseMessage(HttpStatusCode.OK);
        var held = await client.GetAsync("Invoices");
        Assert.Equal("day", held.Headers.GetValues("X-Rate-Limit-Problem").Single());
        Assert.Equal(TimeSpan.FromSeconds(31), held.Headers.RetryAfter!.Delta);
        Assert.Single(network.Received);

        clock.Advance(TimeSpan.FromSeconds(30));
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.GetAsync("Invoices")).StatusCode);

        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("Invoices")).StatusCode);
    }

    [Fact]
    public async Task A429WithoutRetryAfter_PausesSixtySeconds()
    {
        var (clock, network, limiter, client) = Build();
        network.Respond = _ => new HttpResponseMessage(HttpStatusCode.TooManyRequests);

        await client.GetAsync("Invoices");

        Assert.Equal(clock.Now + TimeSpan.FromSeconds(60), limiter.PausedUntilUtc);
    }

    [Fact]
    public async Task AHeldBackCall_ReachesTheTypedClientAsUnavailable_WithRetryAfter()
    {
        var (_, _, _, client) = Build();
        for (var i = 0; i < XeroRateLimiter.ClientCallsPerMinute; i++)
            await client.GetAsync("Invoices");

        var response = await client.GetAsync("Invoices");
        var result = XeroAccountingApi.Interpret<object>(response, string.Empty, Start);

        Assert.Equal(ConnectorOutcome.Unavailable, result.Outcome);
        Assert.Equal(429, result.HttpStatus);
        Assert.Equal(TimeSpan.FromSeconds(60), result.RetryAfter);
        Assert.Equal("minute", result.RateLimitProblem);
    }

    private static (ManualClock Clock, TerminalHandler Network, XeroRateLimiter Limiter, HttpClient Client) Build()
    {
        var clock = new ManualClock(Start);
        var network = new TerminalHandler { Respond = _ => new HttpResponseMessage(HttpStatusCode.OK) };
        var limiter = new XeroRateLimiter(clock) { InnerHandler = network };
        return (clock, network, limiter, new HttpClient(limiter) { BaseAddress = new Uri("https://api.xero.com/api.xro/2.0/") });
    }

    private static HttpResponseMessage WithHeaders(HttpStatusCode status, params (string Name, string Value)[] headers)
    {
        var response = new HttpResponseMessage(status);
        foreach (var (name, value) in headers)
            response.Headers.Add(name, value);
        return response;
    }

    private static HttpResponseMessage RateLimited(TimeSpan retryAfter, string problem)
    {
        var response = WithHeaders(HttpStatusCode.TooManyRequests, ("X-Rate-Limit-Problem", problem));
        response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(retryAfter);
        return response;
    }
}
