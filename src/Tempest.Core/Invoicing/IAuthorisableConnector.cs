using Tempest.Core.Invoicing.OAuth;

namespace Tempest.Core.Invoicing;

/// <summary>
/// A connector whose provider must be signed in to interactively before
/// it can be used — the operator's browser opens on the provider's own
/// consent page and the provider redirects back to the loopback listener
/// (`ADR-0151`, `WP 19.1A` part 2). The two real connectors
/// (<see cref="Xero.XeroConnector"/>, <see cref="QuickBooksOnline.QuickBooksOnlineConnector"/>)
/// implement this; <see cref="FakeInvoicingConnector"/> deliberately does
/// not — it is always authorised and has no provider to sign in to.
/// </summary>
/// <remarks>
/// Added by `WP 21.6P` (the overnight acceptance campaign, 2026-09-15):
/// until then <see cref="OAuthAuthoriser.AuthoriseAsync"/> had no caller
/// anywhere in the product — the Settings area's <em>Authorise</em>
/// button only re-read the stored state — so the first live
/// authorisation the programme owed (`WP 21.6`) could not be performed
/// from the running application at all. Kept as a separate, optional
/// seam rather than a member of <see cref="IInvoicingConnector"/> so
/// the Fake connector and every test double stay untouched.
/// </remarks>
public interface IAuthorisableConnector
{
    /// <summary>
    /// Runs the interactive authorisation: opens the system browser on the
    /// provider's consent page, waits for the loopback redirect, exchanges
    /// the code and stores the tokens. Never throws for a provider
    /// refusal — the outcome is the result (`ADR-0151`). Cancel the token
    /// to give up waiting for the browser.
    /// </summary>
    Task<OAuthResult> AuthoriseAsync(CancellationToken cancellationToken = default);
}
