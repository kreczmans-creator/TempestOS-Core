using Tempest.Calc.Sheets;

namespace Tempest.Calc.Library.Dashboard;

/// <summary>One record kind a template draws on, and whether any record of it has been entered.</summary>
public sealed record RecordRequirement(string Kind, string Title, bool Present);

/// <summary>
/// Which record kinds each template draws on and which of them have no record entered (Addendum C, C-4). The dashboard asks through this
/// interface so that the records tooling (C-3), which adds its own missing-records indicator to each calculator, can supply the same
/// answer here. <see cref="StoreRequiredRecordsSource"/> is the simple implementation over what exists today.
/// </summary>
public interface IRequiredRecordsSource
{
    /// <summary>The requirements of every template, by template id. A template that draws on no records has an empty list or no entry.</summary>
    Task<IReadOnlyDictionary<string, IReadOnlyList<RecordRequirement>>> RequirementsAsync(IReadOnlyList<CalcTemplate> templates, CancellationToken cancellationToken = default);
}

/// <summary>
/// The records lookup over what exists today: a template's <see cref="CalcTemplate.RecordKinds"/> are the kinds it draws on, and a kind is
/// present when the store holds a record of it. Material kinds are left out: the library ships its own generic starting materials, they are
/// not records the owner must type from a licensed copy. It only reads the store; it does not seed the material library.
/// </summary>
public sealed class StoreRequiredRecordsSource : IRequiredRecordsSource
{
    private readonly CalcInstanceStore _store;

    public StoreRequiredRecordsSource(CalcInstanceStore store) => _store = store ?? throw new ArgumentNullException(nameof(store));

    public async Task<IReadOnlyDictionary<string, IReadOnlyList<RecordRequirement>>> RequirementsAsync(IReadOnlyList<CalcTemplate> templates, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(templates);
        var present = (await _store.ListRecordsAsync(null, cancellationToken).ConfigureAwait(false)).Select(r => r.Kind).ToHashSet(StringComparer.Ordinal);
        var result = new Dictionary<string, IReadOnlyList<RecordRequirement>>(StringComparer.Ordinal);

        foreach (var template in templates)
        {
            result[template.Id] =
            [
                .. (template.RecordKinds ?? [])
                    .Where(k => !MaterialLibrary.IsMaterial(k.Kind))
                    .Select(k => new RecordRequirement(k.Kind, k.Title, present.Contains(k.Kind))),
            ];
        }

        return result;
    }
}

/// <summary>
/// The manual-test status of a row, from the guided test run records (Addendum C, C-5). The integrator wires the C-5 implementation in;
/// <see cref="NoManualTestStatusSource"/> says "not tested" for everything.
/// </summary>
public interface IManualTestStatusSource
{
    /// <summary>The status of the template (or pack template) <paramref name="id"/>.</summary>
    Task<ManualTestStatus> StatusAsync(string id, CancellationToken cancellationToken = default);
}

/// <summary>The default manual-test source: nothing has been tested.</summary>
public sealed class NoManualTestStatusSource : IManualTestStatusSource
{
    public Task<ManualTestStatus> StatusAsync(string id, CancellationToken cancellationToken = default) => Task.FromResult(ManualTestStatus.NotTested);
}

/// <summary>What the dashboard shows: the rows, whether the cap cut some off and whether the document data was available.</summary>
/// <param name="Rows">One row per calculator, then one per chain.</param>
/// <param name="TotalRows">The rows before the cap.</param>
/// <param name="Truncated">Whether <see cref="VerificationDashboardService.MaxRows"/> cut rows off.</param>
/// <param name="DataProblem">Why the embedded document data could not be read (the rows then carry only what the catalogue gives), or <see langword="null"/>.</param>
/// <param name="GeneratedAt">When it was assembled.</param>
public sealed record VerificationReport(IReadOnlyList<VerificationRow> Rows, int TotalRows, bool Truncated, string? DataProblem, DateTimeOffset GeneratedAt)
{
    /// <summary>The discipline labels in the report, in row order, "Chains" last.</summary>
    public IReadOnlyList<string> Disciplines { get; } = [.. Rows.Select(r => r.Discipline).Distinct(StringComparer.Ordinal)];

    /// <summary>The rows in <paramref name="discipline"/> (every discipline when <see langword="null"/>) that have the condition <paramref name="status"/>.</summary>
    public IReadOnlyList<VerificationRow> Filter(string? discipline, VerificationStatusFilter status) =>
        [.. Rows.Where(r => (discipline is null || string.Equals(r.Discipline, discipline, StringComparison.Ordinal)) && r.Matches(status))];
}

/// <summary>
/// Addendum C, C-4: assembles the verification dashboard, one row per worksheet calculator and one per chain, from the template catalogue
/// (id, discipline, version, known-answer cases and their provenance flags, record kinds), the embedded document snapshot
/// (<see cref="VerificationSnapshot"/>: KAT results, Track P re-derivation, the known-defects list, worked examples), the records store
/// (through <see cref="IRequiredRecordsSource"/>) and the manual-test status (through <see cref="IManualTestStatusSource"/>).
/// It only reads: no calculation, Evidence status or issued record is touched.
/// </summary>
public sealed class VerificationDashboardService
{
    /// <summary>The most rows a report holds (the program ships 54 calculators and 13 chains).</summary>
    public const int MaxRows = 500;

    /// <summary>The group name a chain's discipline column carries.</summary>
    public const string ChainsLabel = "Chains";

    private readonly CalcCatalogue _catalogue;
    private readonly IReadOnlyList<CalcPackTemplate> _packTemplates;
    private readonly VerificationSnapshot _snapshot;
    private readonly IRequiredRecordsSource? _records;
    private readonly IManualTestStatusSource _manual;
    private readonly TimeProvider _time;
    private readonly int _maxRows;

    /// <param name="catalogue">The templates; a calculator that is switched off is not in it, and so not listed.</param>
    /// <param name="packTemplates">The built-in chains (saved user chains are not verified and not listed).</param>
    /// <param name="snapshot">The embedded document data, or <see cref="VerificationSnapshot.LoadEmbedded"/> when <see langword="null"/>.</param>
    /// <param name="records">The records lookup; <see langword="null"/> shows no missing records.</param>
    /// <param name="manual">The manual-test source; <see cref="NoManualTestStatusSource"/> when <see langword="null"/>.</param>
    /// <param name="time">The clock.</param>
    /// <param name="maxRows">A lower row cap for a test; it can never exceed <see cref="MaxRows"/>.</param>
    public VerificationDashboardService(
        CalcCatalogue catalogue, IReadOnlyList<CalcPackTemplate> packTemplates, VerificationSnapshot? snapshot = null,
        IRequiredRecordsSource? records = null, IManualTestStatusSource? manual = null, TimeProvider? time = null, int maxRows = MaxRows)
    {
        _catalogue = catalogue ?? throw new ArgumentNullException(nameof(catalogue));
        _packTemplates = packTemplates ?? throw new ArgumentNullException(nameof(packTemplates));
        _snapshot = snapshot ?? VerificationSnapshot.LoadEmbedded();
        _records = records;
        _manual = manual ?? new NoManualTestStatusSource();
        _time = time ?? TimeProvider.System;
        _maxRows = Math.Clamp(maxRows, 1, MaxRows);
    }

    /// <summary>The label of a discipline as the dashboard writes it.</summary>
    public static string DisciplineLabel(CalcDiscipline discipline) => discipline switch
    {
        CalcDiscipline.Mechanical => "Mechanical",
        CalcDiscipline.Structural => "Structural",
        CalcDiscipline.Electrical => "Electrical",
        CalcDiscipline.BuildingServices => "Building services",
        CalcDiscipline.FluidPower => "Fluid power",
        _ => discipline.ToString(),
    };

    /// <summary>Assembles the report.</summary>
    public async Task<VerificationReport> BuildAsync(CancellationToken cancellationToken = default)
    {
        var entries = _catalogue.Current();
        var templates = entries.Select(e => e.Template).ToList();
        var requirements = _records is null
            ? new Dictionary<string, IReadOnlyList<RecordRequirement>>(StringComparer.Ordinal)
            : await _records.RequirementsAsync(templates, cancellationToken).ConfigureAwait(false);
        var rows = new List<VerificationRow>();

        foreach (var template in templates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var item = _snapshot.Items.GetValueOrDefault(template.Id);
            var manual = await _manual.StatusAsync(template.Id, cancellationToken).ConfigureAwait(false);
            rows.Add(CalculatorRow(template, item, requirements.GetValueOrDefault(template.Id) ?? [], manual));
        }

        foreach (var pack in _packTemplates.OrderBy(p => p.Title, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var manual = await _manual.StatusAsync(pack.Id, cancellationToken).ConfigureAwait(false);
            rows.Add(ChainRow(pack, _snapshot.Items.GetValueOrDefault(pack.Id), requirements, manual));
        }

        return new VerificationReport([.. rows.Take(_maxRows)], rows.Count, rows.Count > _maxRows, _snapshot.Problem, _time.GetUtcNow());
    }

    private static VerificationRow CalculatorRow(CalcTemplate template, SnapshotItem? item, IReadOnlyList<RecordRequirement> requirements, ManualTestStatus manual)
    {
        var (state, passed, note) = KatState(template, item?.Kat);
        var (result, counts, verdict, findings) = Rederivation(item?.Rederivation);

        return new VerificationRow(
            VerificationRowKind.Calculator, Clip(template.Id), Clip(template.Title), DisciplineLabel(template.Discipline), Clip(template.Version),
            template.TestCases.Count, passed, state, note, item?.WorkedExamples ?? 0,
            template.TestCases.Count(c => c.Status == CalcReferenceStatus.Unconfirmed), template.TestCases.Count(c => c.Source.Secondary),
            result, counts, verdict,
            [.. requirements.Select(r => Clip(r.Title))], [.. requirements.Where(r => !r.Present).Select(r => Clip(r.Title))],
            findings, manual, []);
    }

    private VerificationRow ChainRow(CalcPackTemplate pack, SnapshotItem? item, IReadOnlyDictionary<string, IReadOnlyList<RecordRequirement>> requirements, ManualTestStatus manual)
    {
        var steps = pack.Steps.Select(s => _catalogue.Find(s.TemplateId)).OfType<CalcCatalogueEntry>().DistinctBy(e => e.Template.Id).ToList();
        var (result, counts, verdict, findings) = Rederivation(item?.Rederivation);
        var kinds = steps.SelectMany(s => requirements.GetValueOrDefault(s.Template.Id) ?? []).DistinctBy(r => r.Kind).ToList();

        // A chain has no known-answer cases of its own: its known answer is its issued worked example, which the chain test class
        // generates and checks. The check counts of that example are design outcomes (an example may end on a FAIL on purpose), not test results.
        var examples = item?.WorkedExamples ?? 0;
        var state = examples > 0 ? KatRunState.Passed : KatRunState.NotRun;

        return new VerificationRow(
            VerificationRowKind.Chain, Clip(pack.Id), Clip(pack.Title), ChainsLabel, string.Empty,
            examples, examples, state, examples > 0 ? string.Empty : "no worked example", examples,
            steps.Sum(s => s.Template.TestCases.Count(c => c.Status == CalcReferenceStatus.Unconfirmed)), steps.Sum(s => s.Template.TestCases.Count(c => c.Source.Secondary)),
            result, counts, verdict,
            [.. kinds.Select(r => Clip(r.Title))], [.. kinds.Where(r => !r.Present).Select(r => Clip(r.Title))],
            findings, manual, [.. steps.Select(s => s.Template.Reference)]);
    }

    /// <summary>A result is valid only for the version and case count it was made for; anything else reads as not run.</summary>
    private static (KatRunState State, int Passed, string Note) KatState(CalcTemplate template, SnapshotKat? kat)
    {
        if (template.TestCases.Count == 0)
            return (KatRunState.NotRun, 0, "no known-answer cases");

        if (kat is null)
            return (KatRunState.NotRun, 0, "no results recorded");

        if (!string.Equals(kat.Version, template.Version, StringComparison.Ordinal))
            return (KatRunState.NotRun, 0, $"results are for version {kat.Version}");

        if (kat.Kats != template.TestCases.Count)
            return (KatRunState.NotRun, 0, $"results hold {kat.Kats} cases, the template has {template.TestCases.Count}");

        var passed = Math.Min(kat.KatsPassed, kat.Kats);
        return (passed == kat.Kats && kat.FailedKats.Count == 0 && kat.FailedExamples.Count == 0 ? KatRunState.Passed : KatRunState.Failed, passed, string.Empty);
    }

    private static (string Result, VerificationSeverityCounts Counts, string Verdict, IReadOnlyList<VerificationFinding> Findings) Rederivation(SnapshotRederivation? r)
    {
        if (r is null)
            return ("Not re-derived", VerificationSeverityCounts.None, string.Empty, []);

        var findings = r.Findings.Where(f => f.Severity != "Rejected").Select(f => new VerificationFinding(f.Id, f.Severity, f.Defect)).ToList();
        var counts = new VerificationSeverityCounts(
            findings.Count(f => f.Severity == "Critical"), findings.Count(f => f.Severity == "High"), findings.Count(f => f.Severity == "Medium"));
        var result = r.Result switch
        {
            "match" => "Match",
            "match-with-findings" => "Match, with findings",
            "values-transfer" => "Values transfer correctly",
            "finding" => "Finding",
            _ => "Not re-derived",
        };

        return (result, counts, Clip(r.Verdict), findings);
    }

    private static string Clip(string text) => text.Length > VerificationSnapshot.MaxTextLength ? text[..VerificationSnapshot.MaxTextLength] : text;
}
