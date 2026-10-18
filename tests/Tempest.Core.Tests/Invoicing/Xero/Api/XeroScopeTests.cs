using Tempest.Core.Invoicing;
using Tempest.Core.Invoicing.Xero;

namespace Tempest.Core.Tests.Invoicing.Xero.Api;

/// <summary>
/// `v0.24.0` X0 (§2): the exact scopes TempestOS asks Xero for, and the
/// re-authorisation prompt when the stored grant lacks one.
/// </summary>
public sealed class XeroScopeTests
{
    [Fact]
    public void TheRequestedScopes_AreExactlyTheGranularMinimalSet_InOrder()
    {
        Assert.Equal(
            "accounting.invoices accounting.contacts accounting.settings.read accounting.attachments accounting.reports.banksummary.read offline_access",
            string.Join(' ', XeroScopes.Required));
    }

    [Fact]
    public void TheHostsXeroProfile_RequestsXeroScopesRequired_AndNothingElse()
    {
        var profile = XeroServiceRegistration.OAuthProfile;

        Assert.Equal("Xero", profile.Provider);
        Assert.Equal(XeroScopes.Required, profile.Scopes);
        foreach (var dropped in new[] { "openid", "profile", "email", "accounting.contacts.read", "accounting.transactions", "accounting.settings", "accounting.payments" })
            Assert.DoesNotContain(dropped, profile.Scopes);
    }

    [Fact]
    public void FindMissingScopes_AFullGrant_LacksNothing()
    {
        Assert.Empty(XeroConnector.FindMissingScopes(XeroScopes.Required));
        Assert.Empty(XeroConnector.FindMissingScopes([.. XeroScopes.Required, "openid", "email"]));
    }

    [Fact]
    public void FindMissingScopes_TheV023Grant_LacksTheScopesV024Added()
    {
        string[] v023 = ["openid", "profile", "email", "accounting.invoices", "accounting.contacts.read", "accounting.reports.banksummary.read", "offline_access"];

        Assert.Equal([XeroScopes.Contacts, XeroScopes.SettingsRead, XeroScopes.Attachments], XeroConnector.FindMissingScopes(v023));
    }

    [Fact]
    public void FindMissingScopes_NoGrantRecorded_CountsAsLackingTheScopesV024Added()
    {
        Assert.Equal([XeroScopes.Contacts, XeroScopes.SettingsRead, XeroScopes.Attachments], XeroConnector.FindMissingScopes(null));
    }

    [Fact]
    public async Task AuthorisationState_AFullGrant_IsAuthorised()
    {
        var connector = await ConnectorAsync(XeroScopes.Required);

        var state = await connector.AuthorisationStateAsync();

        Assert.Equal(ConnectorAuthorisation.Authorised, state.Status);
        Assert.Null(state.Detail);
    }

    [Fact]
    public async Task AuthorisationState_AGrantLackingAScope_AsksForReauthorisation_NamingIt()
    {
        var connector = await ConnectorAsync([.. XeroScopes.Required.Where(s => s != XeroScopes.Attachments)]);

        var state = await connector.AuthorisationStateAsync();

        Assert.Equal(ConnectorAuthorisation.Expired, state.Status);
        Assert.Equal("Xero needs re-authorising to allow: accounting.attachments", state.Detail);
    }

    [Fact]
    public async Task AuthorisationState_TokensStoredBeforeV024_AskForReauthorisation()
    {
        var connector = await ConnectorAsync(grantedScopes: null);

        var state = await connector.AuthorisationStateAsync();

        Assert.Equal(ConnectorAuthorisation.Expired, state.Status);
        Assert.Equal("Xero needs re-authorising to allow: accounting.contacts, accounting.settings.read, accounting.attachments", state.Detail);
    }

    [Fact]
    public async Task AuthorisationState_NeverAuthorised_IsStillNotAuthorised()
    {
        var (authoriser, _) = await XeroTestAuthoriser.CreateAsync(authorised: false);
        var connector = new XeroConnector(new HttpClient(new TerminalHandler()), authoriser);

        Assert.Equal(ConnectorAuthorisation.NotAuthorised, (await connector.AuthorisationStateAsync()).Status);
    }

    private static async Task<XeroConnector> ConnectorAsync(IReadOnlyList<string>? grantedScopes)
    {
        var (authoriser, _) = await XeroTestAuthoriser.CreateAsync(grantedScopes: grantedScopes);
        return new XeroConnector(new HttpClient(new TerminalHandler()), authoriser);
    }
}
