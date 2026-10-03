using Tempest.Core.Audit;
using Tempest.Core.Invoicing;
using Tempest.Core.Invoicing.Xero.Settings;
using Tempest.Core.Invoicing.Xero.Sync;
using Tempest.Desktop.Views;
using static Tempest.Desktop.Tests.Xero.SettingsXeroSectionTests;

namespace Tempest.Desktop.Tests.Xero;

/// <summary>
/// `v0.24.0` review-board fixes on Settings → Xero, headless: <em>Refresh from
/// Xero</em> is also <em>Sync now</em> — the engine's Refresh runs, so status
/// changes made in Xero show on demand, and its own settings read is the one
/// shown (M3); a separate <em>Sync now</em> button does the same; and the
/// live-organisation switch is audited under the one documented action name,
/// <c>xero.live-organisation.allowed</c> (m2).
/// </summary>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class SettingsXeroSyncNowTests
{
    private static XeroSyncCycleReport Report(bool settingsRefreshed, int read = 4, int changed = 1, int sent = 2) =>
        new(0, new XeroDrainReport(sent, sent, 0, 0, false, null), new XeroReadBackReport(read, [.. Enumerable.Range(0, changed).Select(_ => XeroDocumentRef.For(XeroDocumentKind.Quote, Guid.NewGuid()))], false), settingsRefreshed);

    [Fact]
    public void TheLiveOrganisationAuditAction_IsTheDocumentedName() =>
        Assert.Equal("xero.live-organisation.allowed", XeroSettingsSection.AllowLiveOrganisationChangedAction);

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public async Task RefreshFromXero_AlsoSyncsNow_AndShowsTheEnginesOwnSettingsRead_WithoutReadingTwice()
    {
        var reader = new FakeXeroSettingsReader { Cached = XeroTestReadings.Demo() };
        var syncs = 0;
        await using var fixture = await SectionFixture.StartAsync(reader, outbox: new FakeXeroOutbox(), services: s => new XeroSettingsSectionServices
        {
            Reader = s.Reader, Identity = s.Identity, Outbox = s.Outbox, Organisations = s.Organisations, Audit = s.Audit, TimeZone = s.TimeZone,
            SyncNow = _ =>
            {
                syncs++;
                return Task.FromResult(Report(settingsRefreshed: true));
            },
        });

        await fixture.Section.RefreshFromXeroAsync();

        Assert.Equal(1, syncs);
        Assert.Equal(0, reader.RefreshCalls); // the engine's settings read is shown, not made twice
        Assert.Equal(
            "Read Demo Company (UK) from Xero. Synced with Xero now: 2 write(s) sent, 4 status(es) read back, 1 changed.",
            Text(fixture.Section, "Xero status"));
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public async Task RefreshFromXero_WhenTheSyncCouldNotRead_FallsBackToReadingTheSettings_AndSaysWhy()
    {
        var reader = new FakeXeroSettingsReader { Cached = XeroTestReadings.Demo() };
        reader.NextRefresh = Task.FromResult(ConnectorResult<XeroSettingsReading>.Ok(XeroTestReadings.Demo()));
        await using var fixture = await SectionFixture.StartAsync(reader, outbox: new FakeXeroOutbox(), services: s => new XeroSettingsSectionServices
        {
            Reader = s.Reader, Identity = s.Identity, Outbox = s.Outbox, Organisations = s.Organisations, Audit = s.Audit, TimeZone = s.TimeZone,
            SyncNow = _ => Task.FromResult(new XeroSyncCycleReport(0, new XeroDrainReport(0, 0, 0, 0, true, null), null, false)),
        });

        await fixture.Section.RefreshFromXeroAsync();

        Assert.Equal(1, reader.RefreshCalls);
        Assert.Contains("Xero needs re-authorising", Text(fixture.Section, "Xero status"), StringComparison.Ordinal);
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public async Task SyncNow_IsOfferedBesideRetryAll_AndRunsTheEngine()
    {
        var reader = new FakeXeroSettingsReader { Cached = XeroTestReadings.Demo() };
        var syncs = 0;
        var outcomes = new List<(string, ActionOutcome)>();
        await using var fixture = await SectionFixture.StartAsync(reader, outbox: new FakeXeroOutbox(), services: s => new XeroSettingsSectionServices
        {
            Reader = s.Reader, Identity = s.Identity, Outbox = s.Outbox, Organisations = s.Organisations, Audit = s.Audit, TimeZone = s.TimeZone,
            SyncNow = _ =>
            {
                syncs++;
                return Task.FromResult(Report(settingsRefreshed: false, read: 7, changed: 2, sent: 0));
            },
        });
        fixture.Section.ActionCompleted += (message, outcome) => outcomes.Add((message, outcome));

        Click(fixture.Section, XeroSettingsSection.SyncNowName);
        await UntilAsync(() => syncs == 1 && Button(fixture.Section, XeroSettingsSection.SyncNowName).IsEnabled);

        Assert.Equal("Synced with Xero now: 0 write(s) sent, 7 status(es) read back, 2 changed.", Text(fixture.Section, "Xero status"));
        Assert.Equal(ActionOutcome.Changed, outcomes[^1].Item2);
        Assert.Equal(0, reader.RefreshCalls);
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public async Task SyncNow_WhenTheEngineThrows_SaysWhy_EndingInOneFullStop()
    {
        // Verifier F3 defect 5: an exception message ends with "." and the note added another.
        var reader = new FakeXeroSettingsReader { Cached = XeroTestReadings.Demo() };
        var syncs = 0;
        await using var fixture = await SectionFixture.StartAsync(reader, outbox: new FakeXeroOutbox(), services: s => new XeroSettingsSectionServices
        {
            Reader = s.Reader, Identity = s.Identity, Outbox = s.Outbox, Organisations = s.Organisations, Audit = s.Audit, TimeZone = s.TimeZone,
            SyncNow = _ =>
            {
                syncs++;
                return Task.FromException<XeroSyncCycleReport>(new InvalidOperationException("The disk is full."));
            },
        });

        Click(fixture.Section, XeroSettingsSection.SyncNowName);
        await UntilAsync(() => syncs == 1 && Button(fixture.Section, XeroSettingsSection.SyncNowName).IsEnabled);

        var text = Text(fixture.Section, "Xero status");
        Assert.Contains("Sync with Xero did not run: The disk is full.", text, StringComparison.Ordinal);
        Assert.DoesNotContain("..", text, StringComparison.Ordinal);
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public async Task WithoutTheEngine_NoSyncNowIsOffered_AndRefreshReadsTheSettingsAlone()
    {
        var reader = new FakeXeroSettingsReader { Cached = XeroTestReadings.Demo() };
        reader.NextRefresh = Task.FromResult(ConnectorResult<XeroSettingsReading>.Ok(XeroTestReadings.Demo()));
        await using var fixture = await SectionFixture.StartAsync(reader, outbox: new FakeXeroOutbox());

        Assert.DoesNotContain(
            Avalonia.LogicalTree.LogicalExtensions.GetLogicalDescendants(fixture.Section).OfType<Avalonia.Controls.Button>(),
            b => Avalonia.Automation.AutomationProperties.GetName(b) == XeroSettingsSection.SyncNowName);
        await fixture.Section.RefreshFromXeroAsync();
        Assert.Equal("Read Demo Company (UK) from Xero.", Text(fixture.Section, "Xero status"));
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public async Task TheLiveSwitch_IsAuditedUnderTheDocumentedName()
    {
        var reader = new FakeXeroSettingsReader { Cached = XeroTestReadings.Live() };
        await using var fixture = await SectionFixture.StartAsync(reader);

        CheckBox(fixture.Section, XeroSettingsSection.AllowLiveOrganisationName).IsChecked = true;
        await fixture.Section.SaveAsync();

        var rows = await fixture.Resolve<IAuditQuery>().QueryAsync(new AuditQueryCriteria(action: "xero.live-organisation.allowed"));
        Assert.Equal("On", Assert.Single(rows).Detail["NewValue"]);
    }
}
