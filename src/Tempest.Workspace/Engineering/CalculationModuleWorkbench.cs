using System.Reflection;
using System.Text.Json;
using Tempest.Core.Calculations;
using Tempest.Core.Calculations.Modules;
using Tempest.Core.Commands;
using Tempest.Core.EngineeringDomain;
using Tempest.Workspace.Calculations;

namespace Tempest.Workspace.Engineering;

/// <summary>One category of the calculator catalogue and the modules under it.</summary>
public sealed record CalculationModuleGroup(string Category, IReadOnlyList<CalculationModuleDescriptor> Modules);

/// <summary>A run as the surface holds it: the presented record and the named Calculation object it is recorded against.</summary>
/// <param name="Run">The record, presented.</param>
/// <param name="CalculationObjectId">The Calculation object the record is linked to, which the Re-run and Compare commands target.</param>
/// <param name="DisplayName">What the calculation is called in the Project Explorer.</param>
/// <param name="RunCount">How many records the object now carries; Compare needs two.</param>
public sealed record CalculationSurfaceRun(CalculationModuleRun Run, Guid CalculationObjectId, string DisplayName, int RunCount);

/// <summary>What pressing Calculate, Re-run or Compare produced: a run, or the refusal in the service's terms, plus the command's own message where one ran.</summary>
public sealed record CalculationSurfaceAttempt(CalculationSurfaceRun? Run, CalculationModuleOutcome Outcome, string? CommandMessage)
{
    /// <summary>Whether a record was written and shown.</summary>
    public bool Succeeded => Run is not null;
}

/// <summary>One row of a comparison table: which section, which field, before and after, units included.</summary>
public sealed record CalculationComparisonRow(string Section, string Field, string Before, string After);

/// <summary>A comparison of a run with its predecessor, as rows, with the command's own message.</summary>
public sealed record CalculationSurfaceComparison(CalculationComparison Comparison, IReadOnlyList<CalculationComparisonRow> Rows, string CommandMessage)
{
    /// <summary>Whether anything differed.</summary>
    public bool HasChanges => Rows.Count > 0;
}

/// <summary>
/// The Engineering Calculators' own read-and-run model (`WP 21.7B`,
/// `WP 21.7C`): the catalogue by category, the released records each
/// reference input may stand on, a run through the governed
/// <see cref="CalculationModuleService"/>, and the canonical Re-run and
/// Compare commands offered on the record that run produced.
/// </summary>
/// <remarks>
/// <para>
/// <b>One code path with the service.</b> Building and running are the
/// service's; this class adds only what a surface needs on top: the
/// grouping, the naming of each run as a Calculation object (so the
/// record is in the Project Explorer and the Ribbon's own
/// <c>calculations.rerun</c> and <c>calculations.compare-with-previous</c>
/// commands reach it), and the rows a comparison table shows.
/// </para>
/// <para>
/// <b>Every module is a Calculation Template.</b> The Re-run and Compare
/// commands execute through <see cref="CalculationTemplateRegistry"/>,
/// which knows a calculation only once it is registered as a template;
/// this class registers every product calculation the registry does not
/// already hold, so the commands work for all sixteen.
/// </para>
/// </remarks>
public sealed class CalculationModuleWorkbench
{
    /// <summary>The Kind the Re-run and Compare commands target.</summary>
    public const string CalculationKind = CalculationObjectFactoryRegistry.CalculationKind;

    private readonly CalculationModuleService _service;
    private readonly CalculationTemplateRegistry _templates;
    private readonly EngineeringCalculationRegister _register;
    private readonly ICommandDispatcher _dispatcher;
    private readonly EngineeringDomainContext _domain;

    /// <summary>Initialises a new instance of the <see cref="CalculationModuleWorkbench"/> class, registering every product calculation the template registry does not already hold.</summary>
    public CalculationModuleWorkbench(
        CalculationModuleService service,
        CalculationTemplateRegistry templates,
        EngineeringCalculationRegister register,
        ICommandDispatcher dispatcher,
        EngineeringDomainContext domain)
    {
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(templates);
        ArgumentNullException.ThrowIfNull(register);
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(domain);

        _service = service;
        _templates = templates;
        _register = register;
        _dispatcher = dispatcher;
        _domain = domain;

        RegisterMissingTemplates(templates);
    }

    /// <summary>Every product calculation, grouped by category in first-seen order, each group in registration order.</summary>
    public static IReadOnlyList<CalculationModuleGroup> Catalogue() =>
        CalculationModuleDescriptors.All
            .GroupBy(d => d.Category, StringComparer.Ordinal)
            .Select(g => new CalculationModuleGroup(g.Key, g.ToList()))
            .ToList();

    /// <summary>Registers every product calculation <paramref name="templates"/> does not already hold, so the Re-run and Compare commands reach all of them.</summary>
    public static void RegisterMissingTemplates(CalculationTemplateRegistry templates)
    {
        ArgumentNullException.ThrowIfNull(templates);

        foreach (var module in CalculationModuleDescriptors.All)
        {
            if (templates.FindByCalculationId(module.Id) is not null)
                continue;

            typeof(CalculationTemplateRegistry)
                .GetMethod(nameof(CalculationTemplateRegistry.Register), BindingFlags.Public | BindingFlags.Instance)!
                .MakeGenericMethod(module.InputType, module.ResultType)
                .Invoke(templates, [module.Id, module.Metadata]);
        }
    }

    /// <summary>The released records of <paramref name="library"/>, as a picker offers them.</summary>
    public Task<IReadOnlyList<ReleasedRecordOption>> ListReleasedAsync(ReferenceLibrary library, CancellationToken cancellationToken = default) =>
        _service.ListReleasedAsync(library, cancellationToken);

    /// <summary>The fields the record picked for <paramref name="referenceInputName"/> fills, and what it cannot supply.</summary>
    public Task<ReferenceFill> FillAsync(CalculationModuleDescriptor module, string referenceInputName, string recordId, CancellationToken cancellationToken = default) =>
        _service.FillFromRecordAsync(module, referenceInputName, recordId, cancellationToken);

    /// <summary>
    /// Runs <paramref name="module"/> on the form. With no <paramref name="onto"/>,
    /// the record is named as a new Calculation object so it is in the
    /// Project Explorer and the Re-run and Compare commands can reach it.
    /// With <paramref name="onto"/> a run of the same module, the input is
    /// executed against that same Calculation through the canonical
    /// <c>calculations.execute</c> command instead, so the object gathers
    /// the runs Compare sets side by side.
    /// </summary>
    public async Task<CalculationSurfaceAttempt> CalculateAsync(
        CalculationModuleDescriptor module, IReadOnlyList<CalculationFormField> fields, string? displayName = null, CalculationSurfaceRun? onto = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(module);
        ArgumentNullException.ThrowIfNull(fields);

        if (onto is not null && string.Equals(onto.Run.Module.Id, module.Id, StringComparison.Ordinal))
            return await CalculateOntoAsync(module, fields, onto, cancellationToken).ConfigureAwait(false);

        var outcome = await _service.RunAsync(new CalculationModuleRequest(module.Id, fields), cancellationToken).ConfigureAwait(false);
        if (!outcome.WasPerformed)
            return new CalculationSurfaceAttempt(null, outcome, null);

        var run = outcome.Run!;
        var named = string.IsNullOrWhiteSpace(displayName)
            ? await NameByDefaultAsync(module, run, cancellationToken).ConfigureAwait(false)
            : await _register.NameAsync(run.RecordId, displayName.Trim(), cancellationToken).ConfigureAwait(false);

        return new CalculationSurfaceAttempt(new CalculationSurfaceRun(run, named.ObjectId, named.DisplayName, 1), outcome, null);
    }

    /// <summary>
    /// Names a run nobody named: the title and the time, and where two runs
    /// share a second, a counter — a Calculation's name is unique in its
    /// project (`TD-38`), and a default must never refuse a run for that.
    /// A name the engineer typed is theirs: a duplicate of one is refused
    /// in the domain's own words, not silently renamed.
    /// </summary>
    private async Task<NamedCalculation> NameByDefaultAsync(CalculationModuleDescriptor module, CalculationModuleRun run, CancellationToken cancellationToken)
    {
        var stem = $"{module.Title} {run.ExecutedAt:yyyy-MM-dd HH:mm:ss}";

        for (var attempt = 1; ; attempt++)
        {
            var candidate = attempt == 1 ? stem : $"{stem} ({attempt})";

            try
            {
                return await _register.NameAsync(run.RecordId, candidate, cancellationToken).ConfigureAwait(false);
            }
            catch (DuplicateBusinessIdentifierException) when (attempt < 1000)
            {
                // Another run of the same calculation in the same second holds this name; try the next.
            }
        }
    }

    private async Task<CalculationSurfaceAttempt> CalculateOntoAsync(
        CalculationModuleDescriptor module, IReadOnlyList<CalculationFormField> fields, CalculationSurfaceRun onto, CancellationToken cancellationToken)
    {
        var prepared = await _service.PrepareAsync(new CalculationModuleRequest(module.Id, fields), cancellationToken).ConfigureAwait(false);
        if (!prepared.Succeeded)
            return new CalculationSurfaceAttempt(null, new CalculationModuleOutcome(prepared.Refusal, prepared.Reason, prepared.Problems, prepared.Fills, null), null);

        var inputJson = JsonSerializer.Serialize(prepared.Input, module.InputType);
        var result = await _dispatcher
            .DispatchAsync(new ExecuteCalculationCommand(onto.CalculationObjectId, CalculationKind, module.Id, inputJson), cancellationToken)
            .ConfigureAwait(false);

        if (!result.Succeeded)
            return new CalculationSurfaceAttempt(null, Refused(CalculationModuleRefusal.InputInvalid, result.Message ?? "The calculation rejected the input."), result.Message);

        return await LatestAsync(onto, result.Message ?? string.Empty, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Re-runs <paramref name="current"/> through the canonical
    /// <c>calculations.rerun</c> command and shows the record it produced.
    /// </summary>
    public async Task<CalculationSurfaceAttempt> RerunAsync(CalculationSurfaceRun current, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(current);

        var result = await _dispatcher
            .DispatchAsync(new RerunCalculationCommand(current.CalculationObjectId, CalculationKind), cancellationToken)
            .ConfigureAwait(false);

        if (!result.Succeeded)
            return new CalculationSurfaceAttempt(null, Refused(CalculationModuleRefusal.InputInvalid, result.Message ?? "The re-run command failed."), result.Message);

        return await LatestAsync(current, result.Message ?? string.Empty, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Compares <paramref name="current"/> with its predecessor through the
    /// canonical <c>calculations.compare-with-previous</c> command, and
    /// hands back the comparison as rows with units.
    /// </summary>
    /// <exception cref="CalculationException">The object has fewer than two records.</exception>
    public async Task<CalculationSurfaceComparison> CompareAsync(CalculationSurfaceRun current, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(current);

        var result = await _dispatcher
            .DispatchAsync(new CompareCalculationWithPreviousCommand(current.CalculationObjectId, CalculationKind), cancellationToken)
            .ConfigureAwait(false);

        if (!result.Succeeded)
            throw new CalculationException(result.Message ?? "The compare command failed.");

        var comparison = await _templates.CompareWithPreviousAsync(current.CalculationObjectId, cancellationToken).ConfigureAwait(false);
        var rows = new List<CalculationComparisonRow>();

        if (comparison.InputComparisonNote is { } note)
            rows.Add(new CalculationComparisonRow("Input", "(note)", note, string.Empty));

        rows.AddRange(comparison.InputChanges.Select(d => new CalculationComparisonRow("Input", CalculationModuleForm.Humanise(d.FieldName), d.OldDisplay ?? "—", d.NewDisplay ?? "—")));
        rows.AddRange(comparison.ResultChanges.Select(d => new CalculationComparisonRow("Result", CalculationModuleForm.Humanise(d.FieldName), d.OldDisplay ?? "—", d.NewDisplay ?? "—")));

        return new CalculationSurfaceComparison(comparison, rows, result.Message ?? string.Empty);
    }

    private async Task<CalculationSurfaceAttempt> LatestAsync(CalculationSurfaceRun current, string commandMessage, CancellationToken cancellationToken)
    {
        var history = await CalculationRecordReader.GetResultHistoryAsync(_domain, current.CalculationObjectId, cancellationToken).ConfigureAwait(false);
        var latest = history.Count > 0 ? history[^1] : null;

        if (latest is null)
            return new CalculationSurfaceAttempt(null, Refused(CalculationModuleRefusal.InputInvalid, "The re-run left no record to show."), commandMessage);

        var presented = await _service.PresentAsync(current.Run.Module, latest.RecordId, cancellationToken).ConfigureAwait(false);
        if (presented is null)
            return new CalculationSurfaceAttempt(null, Refused(CalculationModuleRefusal.InputInvalid, $"Record {latest.RecordId} could not be read back."), commandMessage);

        return new CalculationSurfaceAttempt(
            new CalculationSurfaceRun(presented, current.CalculationObjectId, current.DisplayName, history.Count),
            new CalculationModuleOutcome(CalculationModuleRefusal.None, null, [], [], presented),
            commandMessage);
    }

    private static CalculationModuleOutcome Refused(CalculationModuleRefusal refusal, string reason) => new(refusal, reason, [], [], null);
}
