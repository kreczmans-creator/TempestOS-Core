using Tempest.Core.Timesheets;

namespace Tempest.Workspace.Kpi;

/// <summary>
/// Assembles the Home cockpit's own complete <see cref="Kpi.KpiSnapshot"/>
/// for one <see cref="KpiPeriod"/> (`WP 19.1B`): the one coherent
/// persistence read via <see cref="IWorkspaceSnapshotReader"/>
/// (<see cref="WorkspaceSnapshotKind.Kpi"/>), plus a small, separate,
/// per-principal call to <see cref="IWorkingPatternProvider"/> for
/// utilisation's own denominator.
/// </summary>
/// <remarks>
/// <b>Why the working-pattern call is not inside the same read
/// transaction.</b> <see cref="Tempest.Core.Persistence.IQueryablePersistenceStore.ExecuteInReadTransactionAsync{T}"/>'s
/// own documented rule is that a call back into the outer store from
/// inside its read body contends with the transaction itself and fails on
/// the busy timeout rather than waiting — and <c>IWorkingPatternProvider</c>
/// reads a setting through the identical store, under a different
/// collection. The "one coherent snapshot" `ADR-0150` asks for is about
/// the object-state scan alone — the entries, completions, invoices and
/// Evidence records that could otherwise straddle a commit relative to one
/// another; a principal's own working pattern is comparatively static
/// configuration, not a fact that needs to agree with that same instant.
/// </remarks>
public interface IKpiSnapshotService
{
    /// <summary>Reads the complete KPI snapshot for <paramref name="period"/>, as of now.</summary>
    Task<KpiSnapshot> GetSnapshotAsync(KpiPeriod period, CancellationToken cancellationToken = default);
}

/// <summary>The concrete <see cref="IKpiSnapshotService"/> implementation.</summary>
public sealed class KpiSnapshotService : IKpiSnapshotService
{
    private readonly IWorkspaceSnapshotReader _snapshots;
    private readonly IWorkingPatternProvider _workingPatterns;
    private readonly Func<DateTimeOffset> _now;

    /// <summary>Initialises a new instance of the <see cref="KpiSnapshotService"/> class.</summary>
    public KpiSnapshotService(IWorkspaceSnapshotReader snapshots, IWorkingPatternProvider workingPatterns, Func<DateTimeOffset>? now = null)
    {
        ArgumentNullException.ThrowIfNull(snapshots);
        ArgumentNullException.ThrowIfNull(workingPatterns);

        _snapshots = snapshots;
        _workingPatterns = workingPatterns;
        _now = now ?? (() => DateTimeOffset.UtcNow);
    }

    /// <inheritdoc />
    public async Task<KpiSnapshot> GetSnapshotAsync(KpiPeriod period, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(period);

        var asOf = DateOnly.FromDateTime(_now().UtcDateTime);

        var snapshot = await _snapshots.ReadAsync(WorkspaceSnapshotRequest.Kpi(period, asOf), cancellationToken).ConfigureAwait(false);
        var financials = snapshot.Kpi
            ?? throw new InvalidOperationException("A WorkspaceSnapshotKind.Kpi request returned no Kpi financials.");

        var weekStart = TimesheetWeek.WeekOf(period.From);
        var utilisation = new List<UtilisationRow>(financials.BillableHoursByPrincipal.Count);

        foreach (var (principalId, billableHours) in financials.BillableHoursByPrincipal)
        {
            var hoursPerWeek = await _workingPatterns.AvailableHoursAsync(principalId, weekStart, cancellationToken).ConfigureAwait(false);
            utilisation.Add(new UtilisationRow(principalId, billableHours, KpiEquations.AvailableHours(hoursPerWeek, period)));
        }

        utilisation.Sort((a, b) => string.CompareOrdinal(a.PrincipalIdentityId, b.PrincipalIdentityId));

        return new KpiSnapshot(
            period,
            asOf,
            utilisation,
            financials.MarginByProject,
            financials.WorkInProgress,
            financials.DaysSalesOutstanding,
            financials.CalcThroughput);
    }
}
