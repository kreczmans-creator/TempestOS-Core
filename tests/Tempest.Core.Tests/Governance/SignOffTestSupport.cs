using Tempest.Core.Governance;
using Tempest.Core.Runtime;

namespace Tempest.Core.Tests.Governance;

/// <summary>
/// `ADR-0161`: second-person sign-off is off by default (a one-person
/// consultancy). A test whose own concern is a separation-of-duty refusal
/// turns it on explicitly, through the same policy Settings uses, rather
/// than weakening what it asserts.
/// </summary>
internal static class SignOffTestSupport
{
    public static ISignOffPolicy Policy(ITempestHost host) =>
        (ISignOffPolicy)host.Services!.GetService(typeof(ISignOffPolicy));

    /// <summary>Turns second-person sign-off on for <paramref name="host"/>.</summary>
    public static async Task RequireSecondPersonAsync(ITempestHost host)
    {
        await Policy(host).SetSecondPersonRequiredAsync(true);
        Assert.True(await Policy(host).IsSecondPersonRequiredAsync());
    }
}
