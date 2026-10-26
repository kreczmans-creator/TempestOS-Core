using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Tempest.Core.Persistence;

namespace Tempest.Calc.Library.ManualTesting;

/// <summary>A test-run action the service refused; the message is one plain sentence for the tester.</summary>
public sealed class ManualTestRefusedException(string message) : InvalidOperationException(message);

/// <summary>
/// Starts, resumes, records and finishes guided test runs, and answers the dashboard's "latest manual result per template" (Addendum C, C-5,
/// ADR-0175). A run is a document in the <c>Manual.TestRuns</c> collection; this service writes only that collection. It holds no reference to
/// calculations, Evidence or issues, so a guided run cannot alter any of them (a test proves it).
/// </summary>
public sealed class ManualTestRunService : IManualTestStatusSource
{
    /// <summary>The collection test runs live in (ADR-0175): one JSON document per run, keyed by the run id.</summary>
    public const string Collection = "Manual.TestRuns";

    /// <summary>The most runs the store holds; starting another is refused, so a runaway loop cannot fill the database.</summary>
    public const int DefaultMaxRuns = 1_000;

    /// <summary>The most runs one listing returns.</summary>
    public const int MaxListed = 100;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.General)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
        WriteIndented = false,
    };

    private readonly IQueryablePersistenceStore _store;
    private readonly GuidedTestScript _script;
    private readonly Func<string> _build;
    private readonly Func<string> _buildCommit;
    private readonly Func<(string Id, string Name)> _tester;
    private readonly Func<DateTimeOffset> _clock;
    private readonly int _maxRuns;

    /// <param name="store">The persistence store (the same one the calculations use; only <see cref="Collection"/> is written).</param>
    /// <param name="script">The script a new run is started against.</param>
    /// <param name="build">The build as the title bar shows it (for example <c>0.26.0 (1a2b3c4)</c>).</param>
    /// <param name="buildCommit">The commit of the build (the title bar's short commit).</param>
    /// <param name="tester">The person the app signs work off as: the sign-in id and the display name.</param>
    /// <param name="clock">The time source (UTC); the system clock by default.</param>
    /// <param name="maxRuns">The most runs kept; <see cref="DefaultMaxRuns"/> by default.</param>
    public ManualTestRunService(IQueryablePersistenceStore store, GuidedTestScript script, Func<string> build, Func<string> buildCommit, Func<(string Id, string Name)> tester, Func<DateTimeOffset>? clock = null, int maxRuns = DefaultMaxRuns)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _script = script ?? throw new ArgumentNullException(nameof(script));
        _build = build ?? throw new ArgumentNullException(nameof(build));
        _buildCommit = buildCommit ?? throw new ArgumentNullException(nameof(buildCommit));
        _tester = tester ?? throw new ArgumentNullException(nameof(tester));
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
        _maxRuns = maxRuns > 0 ? maxRuns : throw new ArgumentOutOfRangeException(nameof(maxRuns));
    }

    /// <summary>The script new runs are started against.</summary>
    public GuidedTestScript Script => _script;

    /// <summary>Starts a new run for the person signed in, on this build and this script.</summary>
    /// <exception cref="ManualTestRefusedException">The store already holds the most runs it keeps.</exception>
    public async Task<ManualTestRun> StartAsync(CancellationToken cancellationToken = default)
    {
        var (testerId, testerName) = _tester();
        var run = new ManualTestRun
        {
            Id = Guid.NewGuid(),
            ScriptVersion = Clip(_script.ScriptVersion, ManualTestRun.MaxIdentityLength),
            StartedUtc = _clock().ToUniversalTime(),
            Build = Clip(_build(), ManualTestRun.MaxIdentityLength),
            BuildCommit = Clip(string.IsNullOrWhiteSpace(_buildCommit()) ? "unknown" : _buildCommit(), ManualTestRun.MaxIdentityLength),
            Tester = Clip(string.IsNullOrWhiteSpace(testerName) ? (string.IsNullOrWhiteSpace(testerId) ? "unknown" : testerId) : testerName, ManualTestRun.MaxIdentityLength),
            TesterId = Clip(testerId ?? string.Empty, ManualTestRun.MaxIdentityLength),
        };

        await _store.ExecuteInTransactionAsync(async (tx, token) =>
        {
            var existing = await tx.ListKeysAsync(Collection, string.Empty, token).ConfigureAwait(false);

            if (existing.Count >= _maxRuns)
                throw new ManualTestRefusedException($"There are already {existing.Count} test runs on file, which is the most this build keeps; export the ones you need and ask for the old ones to be cleared.");

            await tx.WriteAsync(Collection, Key(run.Id), Serialise(run), token).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);

        return run;
    }

    /// <summary>A run by id, or <see langword="null"/> when there is none or its record cannot be read.</summary>
    public async Task<ManualTestRun?> FindAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var key = Key(id);
        var read = await _store.ReadManyAsync(Collection, [key], cancellationToken).ConfigureAwait(false);
        return Deserialise(read.GetValueOrDefault(key));
    }

    /// <summary>The newest run that is not finished (the one to offer to resume), or <see langword="null"/>.</summary>
    public async Task<ManualTestRun?> FindUnfinishedAsync(CancellationToken cancellationToken = default) =>
        (await ListAsync(MaxListed, cancellationToken).ConfigureAwait(false)).FirstOrDefault(r => !r.IsFinished);

    /// <summary>The newest runs first, at most <paramref name="limit"/> (and never more than <see cref="MaxListed"/>); a record that cannot be read is left out.</summary>
    public async Task<IReadOnlyList<ManualTestRun>> ListAsync(int limit = MaxListed, CancellationToken cancellationToken = default)
    {
        var all = await ReadAllAsync(cancellationToken).ConfigureAwait(false);
        return [.. all.OrderByDescending(r => r.StartedUtc).ThenBy(r => r.Id).Take(Math.Clamp(limit, 1, MaxListed))];
    }

    /// <summary>
    /// Records <paramref name="outcome"/> for a step, replacing any earlier result for it, while the run is unfinished and by the person who
    /// started it. <paramref name="note"/> over <see cref="ManualTestRun.MaxNoteLength"/> characters is refused (never cut); a
    /// <paramref name="valuesShown"/> over <see cref="ManualTestRun.MaxValuesShownLength"/> is cut with a mark.
    /// </summary>
    /// <exception cref="ManualTestRefusedException">The run, the step or the person does not allow it; the message says which.</exception>
    public async Task<ManualTestRun> RecordAsync(Guid runId, string stepId, ManualTestOutcome outcome, string? note, string? valuesShown, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stepId);
        var step = _script.Find(stepId) ?? throw new ManualTestRefusedException($"The script has no step '{Clip(stepId, 60)}', so no result was recorded.");

        if (!Enum.IsDefined(outcome))
            throw new ManualTestRefusedException($"'{outcome}' is not a result a step can have.");

        var text = (note ?? string.Empty).Trim();

        if (text.Length > ManualTestRun.MaxNoteLength)
            throw new ManualTestRefusedException($"The note for step '{step.Title}' is {text.Length} characters; the most a note can hold is {ManualTestRun.MaxNoteLength}.");

        var values = (valuesShown ?? string.Empty).Trim();

        if (values.Length > ManualTestRun.MaxValuesShownLength)
            values = values[..(ManualTestRun.MaxValuesShownLength - 1)] + "…";

        ManualTestRun? saved = null;

        await _store.ExecuteInTransactionAsync(async (tx, token) =>
        {
            var run = await ReadForChangeAsync(tx, runId, token).ConfigureAwait(false);

            if (!string.Equals(run.ScriptVersion, _script.ScriptVersion, StringComparison.Ordinal))
                throw new ManualTestRefusedException($"This run was started on script {run.ScriptVersion} and this build carries script {_script.ScriptVersion}; finish the run and start a new one.");

            var results = run.Results.Where(r => !string.Equals(r.StepId, stepId, StringComparison.Ordinal)).ToList();

            if (results.Count >= ManualTestRun.MaxResults)
                throw new ManualTestRefusedException($"A run holds at most {ManualTestRun.MaxResults} step results.");

            results.Add(new ManualTestStepResult(stepId, outcome, text, values, _clock().ToUniversalTime()));
            saved = run with { Results = results };
            await tx.WriteAsync(Collection, Key(runId), Serialise(saved), token).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);

        return saved!;
    }

    /// <summary>Finishes a run: it is dated and is read-only from then on. Finishing a finished run changes nothing.</summary>
    /// <exception cref="ManualTestRefusedException">There is no such run, or it was started by someone else.</exception>
    public async Task<ManualTestRun> FinishAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        ManualTestRun? saved = null;

        await _store.ExecuteInTransactionAsync(async (tx, token) =>
        {
            var existing = Deserialise(await tx.ReadAsync(Collection, Key(runId), token).ConfigureAwait(false))
                ?? throw new ManualTestRefusedException("That test run is not on file.");

            if (existing.IsFinished)
            {
                saved = existing;
                return;
            }

            var run = await ReadForChangeAsync(tx, runId, token).ConfigureAwait(false);
            saved = run with { FinishedUtc = _clock().ToUniversalTime() };
            await tx.WriteAsync(Collection, Key(runId), Serialise(saved), token).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);

        return saved!;
    }

    /// <summary>The run as a markdown results sheet in the runbook's section 11 format.</summary>
    public string ExportMarkdown(ManualTestRun run) => ManualTestMarkdown.Export(run, _script);

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, ManualTestStatus>> LatestByTemplateAsync(CancellationToken cancellationToken = default)
    {
        var runs = await ReadAllAsync(cancellationToken).ConfigureAwait(false);

        // The latest counted result of each worked example, over every run (a skipped step is not a result).
        var latest = new Dictionary<string, (ManualTestStepResult Result, ManualTestRun Run)>(StringComparer.Ordinal);

        foreach (var run in runs)
        {
            foreach (var result in run.Results.Where(r => r.Outcome != ManualTestOutcome.Skipped))
            {
                if (_script.Find(result.StepId) is not { Kind: GuidedStepKind.Example })
                    continue;

                if (!latest.TryGetValue(result.StepId, out var held) || result.RecordedUtc > held.Result.RecordedUtc)
                    latest[result.StepId] = (result, run);
            }
        }

        var status = new Dictionary<string, ManualTestStatus>(StringComparer.Ordinal);

        foreach (var group in latest.GroupBy(kv => _script.Find(kv.Key)!.TemplateId!, StringComparer.Ordinal))
        {
            var counted = group.Select(kv => kv.Value).ToList();
            var worst = counted.Any(c => c.Result.Outcome == ManualTestOutcome.Fail) ? ManualTestOutcome.Fail
                : counted.Any(c => c.Result.Outcome == ManualTestOutcome.Blocked) ? ManualTestOutcome.Blocked
                : ManualTestOutcome.Pass;
            var newest = counted.OrderByDescending(c => c.Result.RecordedUtc).First();
            var inScript = _script.Steps.Count(s => s.Kind == GuidedStepKind.Example && string.Equals(s.TemplateId, group.Key, StringComparison.Ordinal));
            status[group.Key] = new ManualTestStatus(group.Key, worst, newest.Result.RecordedUtc, newest.Run.BuildCommit, newest.Run.Tester, newest.Run.Id, counted.Count, inScript);
        }

        return status;
    }

    private async Task<IReadOnlyList<ManualTestRun>> ReadAllAsync(CancellationToken cancellationToken)
    {
        var all = await _store.ReadAllAsync(Collection, cancellationToken).ConfigureAwait(false);
        return [.. all.Select(kv => Deserialise(kv.Value)).OfType<ManualTestRun>()];
    }

    /// <summary>Reads a run inside a transaction and refuses a change to one that is missing, finished, written by a newer build or started by someone else.</summary>
    private async Task<ManualTestRun> ReadForChangeAsync(IPersistenceTransaction tx, Guid runId, CancellationToken token)
    {
        var run = Deserialise(await tx.ReadAsync(Collection, Key(runId), token).ConfigureAwait(false))
            ?? throw new ManualTestRefusedException("That test run is not on file.");

        if (run.IsFinished)
            throw new ManualTestRefusedException($"That test run was finished on {run.FinishedUtc!.Value.UtcDateTime:yyyy-MM-dd HH:mm} UTC and cannot be changed; start a new run.");

        var (testerId, _) = _tester();

        if (run.TesterId.Length > 0 && !string.Equals(run.TesterId, testerId, StringComparison.Ordinal))
            throw new ManualTestRefusedException($"That test run was started by {run.Tester}; switch back to them to continue it, or start a new run.");

        return run;
    }

    private static string Key(Guid id) => id.ToString("N");

    private static string Clip(string text, int length) => text.Length <= length ? text : text[..(length - 1)] + "…";

    private static string Serialise(ManualTestRun run) => JsonSerializer.Serialize(run, Json);

    /// <summary>A document that cannot be read, or that a newer build wrote, is not a run to this build.</summary>
    private static ManualTestRun? Deserialise(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            var run = JsonSerializer.Deserialize<ManualTestRun>(json, Json);
            return run is { Schema: <= ManualTestRun.CurrentSchema, ScriptVersion: not null, Build: not null, BuildCommit: not null, Tester: not null } ? run with { Results = run.Results ?? [] } : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Formats a UTC time the way the sheets and exports do.</summary>
    internal static string Stamp(DateTimeOffset time) => time.UtcDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + " UTC";
}
