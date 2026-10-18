using System.Net;
using System.Text;
using Tempest.Core.Invoicing.Xero;
using Tempest.Core.Invoicing.Xero.Api;
using Tempest.Core.Tests.Invoicing.Xero.Api;
using Tempest.Core.Tests.Invoicing.Xero.Settings;

namespace Tempest.Core.Tests.Invoicing.Xero.Safety;

/// <summary>
/// `v0.24.0` F1 (review board m3): the <em>Allow live organisation</em>
/// switch (D7) is bound to the organisation it was granted for. Re-authorising
/// into another live organisation never writes there on the old decision: the
/// write is blocked, the switch is turned off (audited) and the reason says
/// so. The Demo Company never needs the switch.
/// </summary>
public sealed class XeroLiveOrganisationBindingTests
{
    private const string Root = "https://api.xero.com/api.xro/2.0/";
    private const string LiveA = "tenant-live-a";
    private const string LiveB = "tenant-live-b";
    private const string Demo = "tenant-demo";
    private const string ContactBody = """{"Contacts":[{"Name":"Acme Engineering Ltd"}]}""";

    [Fact]
    public async Task TheSwitch_GrantedForThisOrganisation_LetsItsWritesThrough()
    {
        var rig = await Rig.CreateAsync(grantedFor: LiveA);

        var response = await rig.SendAsync(LiveA, live: true, name: "Tempest Engineering Ltd");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Single(rig.Network.Received);
        Assert.Empty(rig.Audit.Rows);
        Assert.True(await XeroWriteSafetyHandler.IsLiveOrganisationAllowedForAsync(rig.Settings, LiveA));
    }

    [Fact]
    public async Task ReconnectingToAnotherLiveOrganisation_BlocksTheWrite_TurnsTheSwitchOff_AndSaysSo()
    {
        var rig = await Rig.CreateAsync(grantedFor: LiveA);

        var response = await rig.SendAsync(LiveB, live: true, name: "Other Holdings Ltd");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(XeroWriteSafetyHandler.RuleLiveOrganisation, response.Headers.GetValues(XeroWriteSafetyHandler.BlockedHeader).Single());
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("granted for another Xero organisation", body, StringComparison.Ordinal);
        Assert.Contains("turned it off", body, StringComparison.Ordinal);
        Assert.Contains("Other Holdings Ltd", body, StringComparison.Ordinal);
        Assert.Empty(rig.Network.Received);

        // Turned off, and the binding cleared: the Product Owner decides again.
        Assert.Equal("False", await rig.Settings.GetValueAsync(XeroWriteSafetyHandler.AllowLiveOrganisationSettingKey));
        Assert.Equal(string.Empty, await rig.Settings.GetValueAsync(XeroWriteSafetyHandler.AllowLiveOrganisationTenantSettingKey));
        Assert.False(await XeroWriteSafetyHandler.IsLiveOrganisationAllowedForAsync(rig.Settings, LiveB));
        Assert.False(await XeroWriteSafetyHandler.IsLiveOrganisationAllowedForAsync(rig.Settings, LiveA));

        // Audited: the switch turned off (who it was granted for, where TempestOS is now), then the block.
        Assert.Equal(
            [XeroWriteSafetyHandler.LiveOrganisationRevokedAuditAction, XeroWriteSafetyHandler.BlockedAuditAction],
            rig.Audit.Rows.Select(r => r.Action));
        var revoked = rig.Audit.Rows[0].Detail!;
        Assert.Equal(LiveA, revoked["GrantedForTenant"]);
        Assert.Equal(LiveB, revoked["ConnectedTenant"]);
        Assert.Equal("off", revoked["NewValue"]);
        Assert.DoesNotContain(revoked.Values, v => v.Contains(XeroTestAuthoriser.AccessToken, StringComparison.Ordinal));

        // Going back to the first organisation does not quietly restore it either.
        var back = await rig.SendAsync(LiveA, live: true, name: "Tempest Engineering Ltd");
        Assert.Equal(HttpStatusCode.BadRequest, back.StatusCode);
        Assert.Empty(rig.Network.Received);
    }

    [Fact]
    public async Task ConnectingTheDemoCompany_NeedsNoSwitch_AndLeavesTheGrantAsItWas()
    {
        var rig = await Rig.CreateAsync(grantedFor: LiveA);

        var response = await rig.SendAsync(Demo, live: false, name: "Demo Company (UK)");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Single(rig.Network.Received);
        Assert.True(await XeroWriteSafetyHandler.IsLiveOrganisationAllowedForAsync(rig.Settings, LiveA));
        Assert.Empty(rig.Audit.Rows);
    }

    [Fact]
    public async Task ASwitchTurnedOnWithNoOrganisationRecorded_AllowsNone_AndIsTurnedOff()
    {
        var rig = await Rig.CreateAsync(grantedFor: null);
        await rig.Settings.SetValueAsync(XeroWriteSafetyHandler.AllowLiveOrganisationSettingKey, "True");

        var response = await rig.SendAsync(LiveA, live: true, name: "Tempest Engineering Ltd");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(rig.Network.Received);
        Assert.Equal("False", await rig.Settings.GetValueAsync(XeroWriteSafetyHandler.AllowLiveOrganisationSettingKey));
        Assert.Equal("none recorded", rig.Audit.Rows[0].Detail!["GrantedForTenant"]);
    }

    [Fact]
    public async Task SettingTheSwitch_BindsItToTheOrganisation_AndTurningItOff_ClearsTheBinding()
    {
        var settings = new InMemorySettingsProvider();

        await XeroWriteSafetyHandler.SetAllowLiveOrganisationAsync(settings, allow: true, LiveA);
        Assert.Equal("True", await settings.GetValueAsync(XeroWriteSafetyHandler.AllowLiveOrganisationSettingKey));
        Assert.Equal(LiveA, await settings.GetValueAsync(XeroWriteSafetyHandler.AllowLiveOrganisationTenantSettingKey));
        Assert.True(await XeroWriteSafetyHandler.IsLiveOrganisationAllowedForAsync(settings, LiveA));
        Assert.False(await XeroWriteSafetyHandler.IsLiveOrganisationAllowedForAsync(settings, LiveB));
        Assert.False(await XeroWriteSafetyHandler.IsLiveOrganisationAllowedForAsync(settings, null));

        await XeroWriteSafetyHandler.SetAllowLiveOrganisationAsync(settings, allow: false, LiveA);
        Assert.Equal("False", await settings.GetValueAsync(XeroWriteSafetyHandler.AllowLiveOrganisationSettingKey));
        Assert.Equal(string.Empty, await settings.GetValueAsync(XeroWriteSafetyHandler.AllowLiveOrganisationTenantSettingKey));

        // Allowing with no organisation known allows none.
        await XeroWriteSafetyHandler.SetAllowLiveOrganisationAsync(settings, allow: true, tenantId: null);
        Assert.False(await XeroWriteSafetyHandler.IsLiveOrganisationAllowedForAsync(settings, LiveA));
    }

    [Fact]
    public async Task WithoutTheSettingsBinding_TheHandlerBehavesAsBeforeF1()
    {
        // A handler built without the Settings provider (the B1 test rigs): the switch counts for any organisation.
        var reader = new FakeSettingsReader { Cached = FakeSettingsReader.Reading(LiveB, isDemoCompany: false, name: "Other Holdings Ltd") };
        var network = new TerminalHandler();
        using var client = new HttpClient(new XeroWriteSafetyHandler(() => reader, _ => Task.FromResult(true), () => null) { InnerHandler = network })
        {
            BaseAddress = new Uri(Root),
        };

        using var request = Rig.Request(LiveB);
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Single(network.Received);
    }

    private sealed class Rig
    {
        private Rig()
        {
            Handler = new XeroWriteSafetyHandler(
                () => Reader,
                cancellationToken => XeroServiceRegistration.ReadAllowLiveOrganisationAsync(Settings, cancellationToken),
                () => Audit,
                liveOrganisationSettings: () => Settings)
            {
                InnerHandler = Network,
            };
            Client = new HttpClient(Handler) { BaseAddress = new Uri(Root) };
        }

        public InMemorySettingsProvider Settings { get; } = new();

        public FakeSettingsReader Reader { get; } = new();

        public RecordingAuditRecorder Audit { get; } = new();

        public TerminalHandler Network { get; } = new();

        public XeroWriteSafetyHandler Handler { get; }

        public HttpClient Client { get; }

        public static async Task<Rig> CreateAsync(string? grantedFor)
        {
            var rig = new Rig();
            XeroWriteSafetyHandler.EnsureAllowLiveOrganisationDefinition(rig.Settings);
            if (grantedFor is not null)
                await XeroWriteSafetyHandler.SetAllowLiveOrganisationAsync(rig.Settings, allow: true, grantedFor);

            return rig;
        }

        /// <summary>One contact create to <paramref name="tenantId"/>, whose organisation the X1 cache has read.</summary>
        public Task<HttpResponseMessage> SendAsync(string tenantId, bool live, string name)
        {
            Reader.Cached = FakeSettingsReader.Reading(tenantId, isDemoCompany: !live, name: name);
            return Client.SendAsync(Request(tenantId));
        }

        public static HttpRequestMessage Request(string tenantId)
        {
            var request = new HttpRequestMessage(HttpMethod.Put, "Contacts") { Content = new StringContent(ContactBody, Encoding.UTF8, "application/json") };
            request.Headers.Add("xero-tenant-id", tenantId);
            request.Headers.Authorization = new("Bearer", XeroTestAuthoriser.AccessToken);
            return request;
        }
    }
}
