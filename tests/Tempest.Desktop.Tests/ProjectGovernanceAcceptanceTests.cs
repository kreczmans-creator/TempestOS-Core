using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Tempest.App.Projects;
using Tempest.App.Shell;
using Tempest.App.Workspace;
using Tempest.Core.EngineeringDomain;
using Tempest.Desktop.Views;

namespace Tempest.Desktop.Tests;

/// <summary>
/// The Risks area, driven end to end through the real
/// <see cref="MainWindow"/>: <b>project → Risks → raise a risk → score it →
/// own it → mitigate → raise an issue → propose and take a decision →
/// restart → all three are still there, with everything they carried.</b>
/// </summary>
/// <remarks>
/// <para>
/// Nothing here calls <see cref="IProjectGovernanceService"/> directly for
/// the main journey. Every step goes through the rendered surface — the
/// button a user clicks, the dialog a user types into — because the defect
/// this Work Package closes was precisely a set of real domain types
/// (Risk, Hazard, Issue, Decision) that no surface reached. A service test
/// would have passed against the broken product.
/// </para>
/// <para>
/// The restart is a second real <see cref="WorkspaceHost"/> over the same
/// persistence root, which is what a relaunch actually is.
/// </para>
/// </remarks>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class ProjectGovernanceAcceptanceTests
{
    // ================================================================
    // The journey
    // ================================================================

    [AvaloniaFact]
    public async Task Journey_RaiseARisk_ScoreIt_OwnIt_MitigateIt_ThenRelaunch()
    {
        var root = WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath();

        Guid projectId;
        Guid riskId;
        string ownerId;

        // ============================================================
        // FIRST LAUNCH
        // ============================================================
        var first = new WorkspaceHost(root);
        try
        {
            await first.StartAsync();
            var window = new MainWindow(first);

            var project = await first.ProjectDirectory!.CreateAsync("P-0056", "Apollo Pump Redesign");
            projectId = project.Id;
            ownerId = first.SessionPrincipal!.Identity.Id;

            await GoToRisksAsync(first, window, projectId);

            var risks = RisksSurfaceOf(window);

            // --- 1. It starts genuinely empty, and says so ----------
            Assert.True(risks.IsShowingEmptyState);
            Assert.Equal(GovernanceRegisterTab.Risks, risks.SelectedTab);
            Assert.Contains(
                ProjectRisksView.EmptyRisksHeadline,
                risks.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text ?? string.Empty));

            // --- 2. Raise one through the real button and dialog ----
            await ClickAsync(risks, "Raise Risk");
            await AnswerDialogAsync(window, "Impeller cavitation at low flow");
            await RenderUntilAsync(window, () => RisksSurfaceOrNull(window) is { } s && s.Risks.Count == 1);

            risks = RisksSurfaceOf(window);
            var risk = Assert.Single(risks.Risks);
            riskId = risk.ObjectId;

            Assert.False(risks.IsShowingEmptyState);
            Assert.Equal("Impeller cavitation at low flow", risk.DisplayName);
            Assert.Equal(RiskStatus.Open, risk.Status);
            Assert.True(risk.IsUnowned);
            Assert.False(risk.IsScored);

            // --- 3. Score it (two prompts, both axes together) ------
            await ClickWhenPresentAsync(() => RisksSurfaceOf(window), "Score");
            await AnswerDialogAsync(window, "Likely");
            await AnswerDialogAsync(window, "Severe");
            await RenderUntilAsync(window, () => RisksSurfaceOrNull(window) is { Risks.Count: 1 } s && s.Risks[0].IsScored);

            risk = Assert.Single(RisksSurfaceOf(window).Risks);
            Assert.True(risk.IsScored);
            Assert.Equal("Likely", risk.Likelihood);
            Assert.Equal("Severe", risk.Severity);

            // --- 4. Take ownership ---------------------------------
            await ClickWhenPresentAsync(() => RisksSurfaceOf(window), "Own this");
            await RenderUntilAsync(window, () => RisksSurfaceOrNull(window) is { Risks.Count: 1 } s && !s.Risks[0].IsUnowned);

            risk = Assert.Single(RisksSurfaceOf(window).Risks);
            Assert.False(risk.IsUnowned);
            Assert.Equal(ownerId, risk.OwnedByPrincipalId);

            // --- 5. Move it to Mitigating --------------------------
            await ClickWhenPresentAsync(() => RisksSurfaceOf(window), "Mitigating");
            await RenderUntilAsync(window, () => RisksSurfaceOrNull(window) is { Risks.Count: 1 } s && s.Risks[0].Status == RiskStatus.Mitigating);

            risk = Assert.Single(RisksSurfaceOf(window).Risks);
            Assert.Equal(RiskStatus.Mitigating, risk.Status);
            Assert.True(risk.IsLive);
        }
        finally
        {
            await first.DisposeAsync();
        }

        // ============================================================
        // RELAUNCH — a new host over the same store
        // ============================================================
        var second = new WorkspaceHost(root);
        try
        {
            await second.StartAsync();
            var window = new MainWindow(second);

            await GoToRisksAsync(second, window, projectId);
            await RenderUntilAsync(window, () => RisksSurfaceOrNull(window) is { } s && s.Risks.Count == 1);

            var risk = Assert.Single(RisksSurfaceOf(window).Risks);

            // Everything the user set came back, through the production
            // rehydration path (`TD-104`) and nothing else.
            Assert.Equal(riskId, risk.ObjectId);
            Assert.Equal("Impeller cavitation at low flow", risk.DisplayName);
            Assert.Equal(RiskStatus.Mitigating, risk.Status);
            Assert.Equal("Likely", risk.Likelihood);
            Assert.Equal("Severe", risk.Severity);
            Assert.Equal(ownerId, risk.OwnedByPrincipalId);

            // Its project membership survived too — the risk is in this
            // project's register, not merely somewhere in the store.
            Assert.Equal(projectId, await ProjectMembership.ResolveOwningProjectAsync(DomainOf(second).Repository, riskId));

            // --- 6. And it can still be closed ---------------------
            await ClickWhenPresentAsync(() => RisksSurfaceOf(window), "Closed");
            await RenderUntilAsync(window, () => RisksSurfaceOrNull(window) is { Risks.Count: 1 } s && s.Risks[0].Status == RiskStatus.Closed);

            risk = Assert.Single(RisksSurfaceOf(window).Risks);
            Assert.Equal(RiskStatus.Closed, risk.Status);
            Assert.False(risk.IsLive);
        }
        finally
        {
            await second.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task Journey_RaiseAnIssue_AssignIt_ResolveIt_ThenRelaunch()
    {
        var root = WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath();

        Guid projectId;
        Guid issueId;

        var first = new WorkspaceHost(root);
        try
        {
            await first.StartAsync();
            var window = new MainWindow(first);

            var project = await first.ProjectDirectory!.CreateAsync("P-0056", "Apollo Pump Redesign");
            projectId = project.Id;

            await GoToRisksAsync(first, window, projectId);

            // Switching register is part of the journey: all three families
            // share one area, so the user gets to Issues by switching.
            await ClickAsync(RisksSurfaceOf(window), "Issues (0)");
            await RenderUntilAsync(window, () => RisksSurfaceOrNull(window) is { } s && s.SelectedTab == GovernanceRegisterTab.Issues);

            var surface = RisksSurfaceOf(window);
            Assert.Equal(GovernanceRegisterTab.Issues, surface.SelectedTab);
            Assert.True(surface.IsShowingEmptyState);

            await ClickWhenPresentAsync(() => surface, "Raise Issue");
            await AnswerDialogAsync(window, "Blade cracked during the 120% overspeed run");
            await RenderUntilAsync(window, () => RisksSurfaceOrNull(window) is { } s && s.Issues.Count == 1);

            surface = RisksSurfaceOf(window);
            surface.SelectTab(GovernanceRegisterTab.Issues);

            var issue = Assert.Single(surface.Issues);
            issueId = issue.ObjectId;

            Assert.Equal(IssueStatus.Open, issue.Status);
            Assert.Equal(WorkPriority.Normal, issue.Priority);
            Assert.True(issue.IsUnassigned);

            await ClickWhenPresentAsync(() => RisksSurfaceOf(window), "Assign to me");
            await RenderUntilAsync(window, () => RisksSurfaceOrNull(window) is { Issues.Count: 1 } s && !s.Issues[0].IsUnassigned);

            RisksSurfaceOf(window).SelectTab(GovernanceRegisterTab.Issues);
            Assert.False(Assert.Single(RisksSurfaceOf(window).Issues).IsUnassigned);

            await ClickWhenPresentAsync(() => RisksSurfaceOf(window), "Resolved");
            await RenderUntilAsync(window, () => RisksSurfaceOrNull(window) is { Issues.Count: 1 } s && s.Issues[0].Status == IssueStatus.Resolved);

            RisksSurfaceOf(window).SelectTab(GovernanceRegisterTab.Issues);
            issue = Assert.Single(RisksSurfaceOf(window).Issues);

            Assert.Equal(IssueStatus.Resolved, issue.Status);

            // Resolved is not closed: a fix nobody has confirmed is still
            // somebody's problem, and the surface must keep saying so.
            Assert.True(issue.IsOpen);
        }
        finally
        {
            await first.DisposeAsync();
        }

        var second = new WorkspaceHost(root);
        try
        {
            await second.StartAsync();
            var window = new MainWindow(second);

            await GoToRisksAsync(second, window, projectId);
            await RenderUntilAsync(window, () => RisksSurfaceOrNull(window) is { } s && s.Issues.Count == 1);
            RisksSurfaceOf(window).SelectTab(GovernanceRegisterTab.Issues);

            var issue = Assert.Single(RisksSurfaceOf(window).Issues);

            Assert.Equal(issueId, issue.ObjectId);
            Assert.Equal(IssueStatus.Resolved, issue.Status);
            Assert.False(issue.IsUnassigned);
            Assert.Equal(projectId, await ProjectMembership.ResolveOwningProjectAsync(DomainOf(second).Repository, issueId));
        }
        finally
        {
            await second.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task Journey_ProposeADecision_AcceptIt_ThenRelaunch_AndSupersedeIt()
    {
        var root = WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath();

        Guid projectId;
        Guid decisionId;
        string deciderId;

        var first = new WorkspaceHost(root);
        try
        {
            await first.StartAsync();
            var window = new MainWindow(first);

            var project = await first.ProjectDirectory!.CreateAsync("P-0056", "Apollo Pump Redesign");
            projectId = project.Id;
            deciderId = first.SessionPrincipal!.Identity.Id;

            await GoToRisksAsync(first, window, projectId);

            await ClickAsync(RisksSurfaceOf(window), "Decisions (0)");
            await window.RenderCurrentModuleAsync();

            var surface = RisksSurfaceOf(window);
            Assert.Equal(GovernanceRegisterTab.Decisions, surface.SelectedTab);

            // Two prompts: the title, then the rationale. A decision log
            // whose reasons are auto-filled records nothing worth keeping.
            await ClickAsync(surface, "Propose Decision");
            await AnswerDialogAsync(window, "Machine the impeller from titanium");
            await AnswerDialogAsync(window, "Lighter for the same stiffness, and it survives the overspeed case.");
            await RenderUntilDecisionsAsync(window, d => d.Count == 1);

            RisksSurfaceOf(window).SelectTab(GovernanceRegisterTab.Decisions);
            var decision = Assert.Single(RisksSurfaceOf(window).Decisions);
            decisionId = decision.ObjectId;

            Assert.Equal(DecisionStatus.Proposed, decision.Status);
            Assert.True(decision.IsAwaitingDecision);
            Assert.Null(decision.DecidedAt);
            Assert.Contains("Lighter for the same stiffness", decision.Rationale, StringComparison.Ordinal);

            await ClickAsync(RisksSurfaceOf(window), "Accepted");
            await RenderUntilDecisionsAsync(window, d => d.Count == 1 && d[0].Status == DecisionStatus.Accepted);

            RisksSurfaceOf(window).SelectTab(GovernanceRegisterTab.Decisions);
            decision = Assert.Single(RisksSurfaceOf(window).Decisions);

            Assert.Equal(DecisionStatus.Accepted, decision.Status);
            Assert.True(decision.IsInForce);
            Assert.Equal(deciderId, decision.DecidedByPrincipalId);
            Assert.NotNull(decision.DecidedAt);
        }
        finally
        {
            await first.DisposeAsync();
        }

        var second = new WorkspaceHost(root);
        try
        {
            await second.StartAsync();
            var window = new MainWindow(second);

            await GoToRisksAsync(second, window, projectId);
            await RenderUntilDecisionsAsync(window, d => d.Count == 1);
            RisksSurfaceOf(window).SelectTab(GovernanceRegisterTab.Decisions);

            var decision = Assert.Single(RisksSurfaceOf(window).Decisions);

            Assert.Equal(decisionId, decision.ObjectId);
            Assert.Equal(DecisionStatus.Accepted, decision.Status);
            Assert.Equal(deciderId, decision.DecidedByPrincipalId);
            Assert.NotNull(decision.DecidedAt);

            var decidedAt = decision.DecidedAt;

            // Superseding it leaves the original record of who decided and
            // when exactly as it was.
            await ClickAsync(RisksSurfaceOf(window), "Superseded");
            await RenderUntilDecisionsAsync(window, d => d.Count == 1 && d[0].Status == DecisionStatus.Superseded);

            RisksSurfaceOf(window).SelectTab(GovernanceRegisterTab.Decisions);
            decision = Assert.Single(RisksSurfaceOf(window).Decisions);

            Assert.Equal(DecisionStatus.Superseded, decision.Status);
            Assert.False(decision.IsInForce);
            Assert.Equal(deciderId, decision.DecidedByPrincipalId);
            Assert.Equal(decidedAt, decision.DecidedAt);

            // And it is terminal: the surface offers no further move.
            var captions = ButtonCaptions(RisksSurfaceOf(window));
            Assert.DoesNotContain("Accepted", captions);
            Assert.DoesNotContain("Proposed", captions);
            Assert.Contains("Edit", captions);
        }
        finally
        {
            await second.DisposeAsync();
        }
    }

    // ================================================================
    // The surface never offers a move the domain would refuse
    // ================================================================

    [AvaloniaFact]
    public async Task TheSurfaceOffersOnlyTheTransitionsTheDomainPermits()
    {
        var root = WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath();

        var host = new WorkspaceHost(root);
        try
        {
            await host.StartAsync();
            var window = new MainWindow(host);

            var project = await host.ProjectDirectory!.CreateAsync("P-0056", "Apollo");
            await GoToRisksAsync(host, window, project.Id);

            await ClickAsync(RisksSurfaceOf(window), "Raise Risk");
            await AnswerDialogAsync(window, "Cavitation");
            await RenderUntilAsync(window, () => RisksSurfaceOrNull(window) is { } s && s.Risks.Count == 1);

            var captions = ButtonCaptions(RisksSurfaceOf(window));

            // An Open risk may go to Mitigating, Accepted or Closed — and
            // must never be offered a move to Open, which the table refuses.
            foreach (var permitted in RiskStatusTransitions.GetPermittedTargets(RiskStatus.Open))
                Assert.Contains(ProjectRisksView.Describe(permitted), captions);

            Assert.DoesNotContain(ProjectRisksView.Describe(RiskStatus.Open), captions);
        }
        finally
        {
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task TheRisksAreaIsMarkedImplemented_AndDrawsNoDeclaredCapabilityCard()
    {
        // The TD-102 defect class: a descriptor claiming a capability the
        // surface does not have. This area was Declared until this Work
        // Package; now it must be Implemented *and* render real content.
        Assert.True(ProjectAreas.IsImplemented(ProjectArea.Risks));
        Assert.Null(ProjectAreas.For(ProjectArea.Risks).TrackedBy);

        var root = WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath();
        var host = new WorkspaceHost(root);
        try
        {
            await host.StartAsync();
            var window = new MainWindow(host);

            var project = await host.ProjectDirectory!.CreateAsync("P-0056", "Apollo");
            await GoToRisksAsync(host, window, project.Id);

            Assert.NotNull(RisksSurfaceOf(window));

            // The Risks tab's own content is the real surface, and nothing
            // else. Asserted against the tab rather than against the whole
            // window, because the project workspace builds every area's
            // content up front — so cards for the areas that genuinely are
            // Declared (Timeline, Reports, Settings) legitimately exist in
            // the logical tree and a window-wide assertion would be wrong.
            var risksTab = window.GetLogicalDescendants().OfType<TabItem>()
                .Distinct()
                .Single(tab => tab.Tag is ProjectArea.Risks);

            Assert.IsType<ProjectRisksView>(risksTab.Content);

            Assert.DoesNotContain(
                ((Control)risksTab.Content!).GetLogicalDescendants().OfType<Control>(),
                child => child is DeclaredCapabilityView);
        }
        finally
        {
            await host.DisposeAsync();
        }
    }

    // ================================================================
    // Helpers
    // ================================================================

    private static async Task GoToRisksAsync(WorkspaceHost host, MainWindow window, Guid projectId)
    {
        await host.ShellNavigator!.OpenProjectAsync(projectId, ProjectArea.Risks);
        await window.RenderCurrentModuleAsync();
    }

    /// <summary>The one Risks surface on screen.</summary>
    /// <remarks>
    /// Deduplicated by identity, not filtered by <c>Single()</c>: once a
    /// window is shown, a <see cref="TabControl"/> materialises the selected
    /// tab's content through its presenter as well, so the same control
    /// instance appears twice in the logical tree.
    /// </remarks>
    private static ProjectRisksView RisksSurfaceOf(MainWindow window) =>
        window.GetLogicalDescendants().OfType<ProjectRisksView>().Distinct().Single();

    private static EngineeringDomainContext DomainOf(WorkspaceHost host) =>
        (EngineeringDomainContext)host.Services!.GetService(typeof(EngineeringDomainContext));

    /// <summary>
    /// Re-renders the current module until <paramref name="condition"/> holds
    /// over the live Decisions list, or a two-second deadline expires.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>TD-119</c>. Proposing and deciding are dispatched fire-and-forget —
    /// <c>MainWindow</c> wires them as <c>_ = CreateProjectDecisionAsync()</c>
    /// — so the create, its persistence write and the coordinator's own
    /// refresh all complete on a continuation this test has no task for. A
    /// single <c>RenderCurrentModuleAsync</c> after a fixed delay therefore
    /// read one sample at an arbitrary instant: if the write had not landed,
    /// it read the pre-create list and <c>Decisions</c> was empty. That is the
    /// failure CI hit at <c>384e47f</c> — <c>Assert.Single() Failure: The
    /// collection was empty</c> — while the push-triggered run on the
    /// identical SHA passed.
    /// </para>
    /// <para>
    /// Same bounded-poll remedy as <c>TD-46</c>/<c>WP 13.12.9</c>. Re-rendering
    /// is a read, never a write: <c>ProjectWorkspaceView.RefreshAsync</c> lists
    /// and shows, and can never itself create or transition a decision, so this
    /// loop cannot manufacture a pass. It decides only *when* to assert — every
    /// assertion at the call sites is unchanged, and still fails, on its own
    /// message, if the expected state never arrives.
    /// </para>
    /// </remarks>
    private static async Task RenderUntilDecisionsAsync(
        MainWindow window,
        Func<IReadOnlyList<ProjectDecisionEntry>, bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (true)
        {
            await window.RenderCurrentModuleAsync();
            if (condition(DecisionsOrEmpty(window)) || DateTime.UtcNow >= deadline)
                return;

            await Task.Delay(10);
        }
    }

    /// <summary>
    /// The Decisions on the surface, or an empty list while the logical tree
    /// is momentarily not resolvable to exactly one <see cref="ProjectRisksView"/>.
    /// </summary>
    /// <remarks>
    /// Used only to decide when to stop re-rendering. Every assertion reads
    /// through <see cref="RisksSurfaceOf"/> directly and unguarded, so a tree
    /// that never settles still fails there, loudly, rather than being
    /// swallowed here.
    /// </remarks>
    private static IReadOnlyList<ProjectDecisionEntry> DecisionsOrEmpty(MainWindow window)
    {
        var surfaces = window.GetLogicalDescendants().OfType<ProjectRisksView>().Distinct().ToList();
        return surfaces.Count == 1 ? surfaces[0].Decisions : [];
    }

    private static List<string> ButtonCaptions(Control surface) =>
        [.. surface.GetLogicalDescendants().OfType<Button>().Select(b => b.Content?.ToString() ?? string.Empty)];

    private static async Task ClickAsync(Control surface, string caption)
    {
        var button = surface.GetLogicalDescendants().OfType<Button>()
            .FirstOrDefault(b => string.Equals(b.Content?.ToString(), caption, StringComparison.Ordinal));

        Assert.True(button is not null, $"No '{caption}' button on this surface. Present: {string.Join(", ", ButtonCaptions(surface))}");

        button!.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

        // `TD-119`: no fixed wait. The click is dispatched fire-and-forget, so a
        // duration here would only be a guess; every caller now joins on the real
        // state its own assertion reads.
    }

    /// <summary>Clicks <paramref name="caption"/> once it is actually present on the freshly re-queried surface.</summary>
    /// <remarks>
    /// `TD-119`. Row-level buttons such as "Score" or "Own this" exist only once
    /// the row they belong to has rendered, which happens on an asynchronous
    /// continuation. <see cref="ClickAsync"/> deliberately still fails at once for
    /// a button that ought to be there already; this is for targets legitimately
    /// produced asynchronously. The surface is re-queried every iteration, the
    /// click is raised exactly once, and a button that never appears fails with
    /// the same message <see cref="ClickAsync"/> would give.
    /// </remarks>
    private static async Task ClickWhenPresentAsync(Func<Control> surface, string caption)
    {
        Button? button;
        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (true)
        {
            button = surface().GetLogicalDescendants().OfType<Button>()
                .FirstOrDefault(b => string.Equals(b.Content?.ToString(), caption, StringComparison.Ordinal));

            if (button is not null || DateTime.UtcNow >= deadline)
                break;

            await Task.Delay(10);
        }

        Assert.True(button is not null, $"No '{caption}' button on this surface. Present: {string.Join(", ", ButtonCaptions(surface()))}");

        button!.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
    }

    /// <summary>Re-renders the current module until <paramref name="condition"/> holds, or a two-second deadline expires.</summary>
    /// <remarks>
    /// `TD-119`. The generalisation of <see cref="RenderUntilDecisionsAsync"/> to
    /// the whole surface. Rendering is a read, so this loop cannot manufacture the
    /// state it waits for; it decides only *when* to assert, and every assertion
    /// at the call sites is unchanged.
    /// </remarks>
    private static async Task RenderUntilAsync(MainWindow window, Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (true)
        {
            await window.RenderCurrentModuleAsync();
            if (condition() || DateTime.UtcNow >= deadline)
                return;

            await Task.Delay(10);
        }
    }

    /// <summary>The risks surface, or null while the tree holds no single one.</summary>
    private static ProjectRisksView? RisksSurfaceOrNull(MainWindow window)
    {
        var found = window.GetLogicalDescendants().OfType<ProjectRisksView>().Distinct().ToList();
        return found.Count == 1 ? found[0] : null;
    }

    private static async Task AnswerDialogAsync(MainWindow window, string answer)
    {
        var dialog = window.GetLogicalDescendants().OfType<InputDialog>().Single();

        // `TD-119`: the prompt is raised on an asynchronous continuation, so the
        // dialog need not be showing yet when this helper is called — a second,
        // distinct race from the fixed wait below, which remains disclosed debt.
        // Bounded wait on its real visibility before typing into it.
        var dialogDeadline = DateTime.UtcNow.AddSeconds(2);
        while (!dialog.IsVisible && DateTime.UtcNow < dialogDeadline)
            await Task.Delay(10);

        Assert.True(dialog.IsVisible, "The input dialog never became visible.");

        var textBox = dialog.GetLogicalDescendants().OfType<TextBox>().Single();
        textBox.Text = answer;

        var ok = dialog.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "OK"));
        ok.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

        // `TD-119`: no fixed wait. The dialog is answered exactly once above; the
        // work that releases is joined at the caller's own assertion.
    }
}
