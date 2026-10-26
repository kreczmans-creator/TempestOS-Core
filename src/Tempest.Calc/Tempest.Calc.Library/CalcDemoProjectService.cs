using System.Text;
using Tempest.Calc.Sheets;
using Tempest.Core.Audit;
using Tempest.Core.EngineeringDomain;

namespace Tempest.Calc.Library;

/// <summary>What an install or a removal of the demonstration project did, in one plain sentence naming the project (and, when it counts, how many).</summary>
/// <param name="Succeeded">Whether the demonstration was installed or removed.</param>
/// <param name="Message">One sentence for the person.</param>
/// <param name="Calculations">How many calculations the act made or removed.</param>
/// <param name="Packs">How many packs the act made or removed.</param>
public sealed record CalcDemoOutcome(bool Succeeded, string Message, int Calculations = 0, int Packs = 0)
{
    internal static CalcDemoOutcome Refused(string message) => new(false, message);
}

/// <summary>Whether the demonstration project is in this database, and why it cannot be installed or removed if it cannot.</summary>
/// <param name="Installed">A live project is the demonstration (it carries the flag and the number).</param>
/// <param name="ProjectId">That project's id, when installed.</param>
/// <param name="InstallRefusal">Why an install would be refused (it is installed already, or another project holds the number), or <see langword="null"/> when it would go ahead.</param>
public sealed record CalcDemoStatus(bool Installed, Guid? ProjectId, string? InstallRefusal);

/// <summary>
/// Addendum C, C-6: installs and removes the demonstration project (<see cref="CalcDemoProject"/>) through the services a person uses — a project made as the
/// Projects area makes one, each pack made from its template, the calculations' inputs seeded from the templates' own example inputs, the links bound by the
/// pack — and nothing else. It never runs by itself: a person asks for it (Settings → Demo project), and nothing at start-up calls it.
/// <para>
/// <b>Install</b> refuses while a live project holds the demonstration's number (the demonstration itself, or a real project that took the number); it makes
/// the project flagged as a demonstration, the synthetic scan profiles, twelve packs and the scan-to-seal pair, and checks that every calculation runs,
/// has no block and is not stale. A failure part-way removes what the install made. Nothing is checked or issued: a check records an independent person's
/// name, and the installer is one person, so the calculations are left as drafts for the user to check.
/// </para>
/// <para>
/// <b>Remove</b> is the only delete this feature makes. In one transaction it deletes the demonstration's calculation, pack and scan-profile rows
/// (<see cref="CalcInstanceStore.DeleteDemonstrationRowsAsync"/>); it then marks the project and everything under it deleted with the platform's own
/// soft delete (an engineering object is never erased: its history and the audit trail stay, ADR-0074). It refuses when any calculation or pack of the
/// demonstration is issued: an issued record is never deleted. Rows of any other project are never read for deletion, let alone deleted.
/// </para>
/// </summary>
public sealed class CalcDemoProjectService
{
    private readonly CalcInstanceService _service;
    private readonly EngineeringDomainContext _domain;
    private readonly IReadOnlyList<CalcInputValue> _cableTables;
    private readonly IAuditRecorder? _audit;
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <param name="service">The calculation service every calculation of the demonstration is made through.</param>
    /// <param name="domain">The engineering domain the demonstration's project is made in and removed from.</param>
    /// <param name="cableTables">
    /// The cable-capacity and device-rating inputs (<c>ampacity</c> and <c>ratings</c>) the motor-supply cable steps need. No build ships real ones; the
    /// composition passes the calculator's own synthetic test tables, which this service labels as invented demonstration data.
    /// </param>
    /// <param name="audit">Records "Demo.Installed" and "Demo.Removed" (what was made or deleted, and how many) in the audit trail, which a removal never touches; <see langword="null"/> records nothing.</param>
    public CalcDemoProjectService(CalcInstanceService service, EngineeringDomainContext domain, IReadOnlyList<CalcInputValue> cableTables, IAuditRecorder? audit = null)
    {
        _audit = audit;
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _domain = domain ?? throw new ArgumentNullException(nameof(domain));
        _cableTables = cableTables ?? throw new ArgumentNullException(nameof(cableTables));
    }

    /// <summary>Whether the demonstration is installed, and what an install would be refused for.</summary>
    public async Task<CalcDemoStatus> StatusAsync(CancellationToken cancellationToken = default)
    {
        var (demo, other) = await FindAsync(cancellationToken).ConfigureAwait(false);

        if (demo is not null)
            return new CalcDemoStatus(true, demo.Id, $"The demo project '{CalcDemoProject.DisplayName}' is installed already; remove it before installing it again.");

        return new CalcDemoStatus(false, null, other is null ? null : NumberTaken(other));
    }

    private static string NumberTaken(IEngineeringObject other) =>
        $"The project '{other.BusinessIdentifier}' already holds the number {CalcDemoProject.Identifier} and is not the demo project, so the demo project was not installed.";

    // ------------------------------------------------------------------------------------------------------------------------------
    // Install
    // ------------------------------------------------------------------------------------------------------------------------------

    /// <summary>Installs the demonstration project, or says in a sentence why it did not.</summary>
    /// <param name="progress">Told what the install is making, one short line at a time.</param>
    /// <param name="cancellationToken">Cancels the install; what was made is removed.</param>
    public async Task<CalcDemoOutcome> InstallAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var status = await StatusAsync(cancellationToken).ConfigureAwait(false);

            if (status.InstallRefusal is { } refusal)
                return CalcDemoOutcome.Refused(refusal);

            if (CompositionProblem() is { } missing)
                return CalcDemoOutcome.Refused(missing);

            Guid? projectId = null;

            try
            {
                progress?.Report("Creating the demo project…");
                projectId = await CreateProjectAsync(cancellationToken).ConfigureAwait(false);
                var packs = 0;

                foreach (var (area, number) in CalcDemoProject.Areas.Select((a, i) => (a, i + 1)))
                {
                    progress?.Report($"Creating {area.Area} ({number} of {CalcDemoProject.Areas.Count + 1})…");
                    await InstantiateAreaAsync(area, projectId.Value, cancellationToken).ConfigureAwait(false);
                    packs++;
                }

                progress?.Report($"Creating {CalcDemoProject.SealArea} ({CalcDemoProject.Areas.Count + 1} of {CalcDemoProject.Areas.Count + 1})…");
                await CreateScanToSealAsync(projectId.Value, cancellationToken).ConfigureAwait(false);
                packs++;

                progress?.Report("Checking every calculation runs and is current…");
                var calculations = await VerifyAsync(projectId.Value, cancellationToken).ConfigureAwait(false);
                await RecordAsync("Demo.Installed", calculations, packs).ConfigureAwait(false);

                return new CalcDemoOutcome(true, $"Installed the demo project '{CalcDemoProject.DisplayName}': {calculations} calculations in {packs} packs, all drafts with invented data.", calculations, packs);
            }
            catch (OperationCanceledException)
            {
                await RollBackAsync(projectId).ConfigureAwait(false);
                throw;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                await RollBackAsync(projectId).ConfigureAwait(false);
                return CalcDemoOutcome.Refused($"The demo project could not be installed ({Sentence(ex.Message)}); nothing of it was kept.");
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Why this build cannot make the demonstration (a pack template or a calculator it needs is not composed in), or <see langword="null"/>.</summary>
    private string? CompositionProblem()
    {
        foreach (var area in CalcDemoProject.Areas)
        {
            if (TemplateOf(area) is not { } template)
                return $"The demo project cannot be installed: this build has no '{area.TemplateId}' calculation pack.";

            if (_service.ValidatePackTemplate(CalcDemoProject.Renamed(template, area)) is { } invalid)
                return $"The demo project cannot be installed: {invalid}";
        }

        foreach (var id in new[] { "mech.tolerance-stack", "fp.oring-gland" })
        {
            if (_service.Catalogue.Find(id) is null)
                return $"The demo project cannot be installed: this build has no '{id}' calculator.";
        }

        return null;
    }

    private CalcPackTemplate? TemplateOf(CalcDemoArea area) => _service.PackTemplates.FirstOrDefault(t => string.Equals(t.Id, area.TemplateId, StringComparison.Ordinal));

    /// <summary>Makes the project the way the Projects area does, flagged as a demonstration.</summary>
    private async Task<Guid> CreateProjectAsync(CancellationToken cancellationToken)
    {
        var metadata = new EngineeringObjectMetadata(Category: "Demonstration", Tags: [CalcDemoProject.Tag], Notes: CalcDemoProject.Story);
        var factory = new EngineeringObjectFactory<Project>(
            CalcDemoProject.ProjectKind, _domain,
            (doc, rev) => new Project(doc, rev, _domain, CalcDemoProject.Identifier, CalcDemoProject.DisplayName, metadata));
        var created = await factory.CreateAsync($"Project {CalcDemoProject.Identifier} — {CalcDemoProject.DisplayName}. {CalcDemoProject.Watermark}.", cancellationToken).ConfigureAwait(false);
        return created.Id;
    }

    /// <summary>Makes one area's pack from its template, with the story's names, the invented specification and (for a cable step) the synthetic tables.</summary>
    private async Task InstantiateAreaAsync(CalcDemoArea area, Guid projectId, CancellationToken cancellationToken)
    {
        var template = CalcDemoProject.Renamed(TemplateOf(area)!, area);
        var inputs = new Dictionary<string, IReadOnlyList<CalcInputValue>>(StringComparer.Ordinal);

        for (var i = 0; i < template.Steps.Count; i++)
        {
            var step = template.Steps[i];
            var values = _service.ExampleInputs(template, step, CalcDemoProject.Chosen(area, i + 1), CalcDemoProject.Default)
                ?? throw new InvalidOperationException($"Step '{step.Name}' of the {area.Area} pack has no example inputs to seed it from.");

            if (area.Adjust is { } adjust)
                values = adjust(step.Key, values);

            // The cable step is refused without its two tables; they are the synthetic ones, said to be invented on the sheet.
            if (string.Equals(step.TemplateId, "elec.conductor-sizing", StringComparison.Ordinal))
            {
                var tables = _cableTables.Select(t => t with { Provenance = CalcDemoProject.SyntheticTables }).ToList();
                values = [.. values.Where(v => tables.All(t => t.Symbol != v.Symbol)), .. tables];
            }

            inputs[step.Key] = values;
        }

        var (made, refusal) = await _service.InstantiatePackTemplateAsync(template, projectId, CalcDemoProject.PackTitle(area), inputs, cancellationToken).ConfigureAwait(false);

        if (made is null)
            throw new InvalidOperationException($"the {area.Area} pack was refused: {refusal}");

        // A base plate's anchorage is designed elsewhere: file a plainly marked placeholder so a second person can check the pack (as the worked examples do).
        foreach (var plate in template.Steps.Where(s => string.Equals(s.TemplateId, "struct.base-plate", StringComparison.Ordinal)))
        {
            var filed = await _service.AttachExternalEvidenceAsync(
                made.Instances[plate.Key].Id, "DEMO-ANCHORAGE-PLACEHOLDER.txt", "text/plain",
                Encoding.UTF8.GetBytes("PLACEHOLDER for the demo project. No anchorage report exists: the demonstration is invented. This file is not a design report."),
                "profis_ref", cancellationToken).ConfigureAwait(false);

            if (!filed.Succeeded)
                throw new InvalidOperationException($"the placeholder for '{plate.Name}' was refused: {filed.Reason}");
        }
    }

    /// <summary>Makes the scan-to-seal pair: the groove-root stack and the gland, the synthetic table read into both, and the gland linked to the stack.</summary>
    private async Task CreateScanToSealAsync(Guid projectId, CancellationToken cancellationToken)
    {
        foreach (var profile in new[] { CalcDemoProject.StackProfile, CalcDemoProject.GlandProfile })
        {
            var (_, refusal) = await _service.SaveScanProfileAsync(profile, cancellationToken).ConfigureAwait(false);

            // A profile is immutable and a removal deletes it; one left by a removal that stopped is the same profile, so it is used as it stands.
            if (refusal is not null && !refusal.Contains("new version", StringComparison.Ordinal))
                throw new InvalidOperationException($"the scan profile '{profile.Id}' was refused: {refusal}");
        }

        var stack = await _service.CreateAsync("mech.tolerance-stack", projectId, $"{CalcDemoProject.NamePrefix}{CalcDemoProject.SealArea}: Groove root stack", CalcDemoProject.SealStackInputs(), cancellationToken).ConfigureAwait(false);
        var glandTemplate = _service.Catalogue.Get("fp.oring-gland").Template;
        var glandInputs = CalcTemplateHasher.DefaultInputs(glandTemplate).Values.Select(v => v with { Provenance = v.Provenance ?? CalcDemoProject.SealDrawing }).ToList();
        var gland = await _service.CreateAsync("fp.oring-gland", projectId, $"{CalcDemoProject.NamePrefix}{CalcDemoProject.SealArea}: Piston seal gland", glandInputs, cancellationToken).ConfigureAwait(false);

        Require(await _service.BindAsync(gland.Id, "D_g", stack.Id, "x_mean", cancellationToken).ConfigureAwait(false), "linking the gland to the stack");
        Require((await _service.ApplyScanImportAsync(stack.Id, new ScanImportRequest([CalcDemoProject.ScanFile()], CalcDemoProject.StackProfile, CalcDemoProject.ScanSettings), cancellationToken).ConfigureAwait(false)).Action, "reading the scan into the stack");
        Require(await _service.UpdateLinksAsync(gland.Id, cancellationToken).ConfigureAwait(false), "bringing the gland's link up to date");
        Require((await _service.ApplyScanImportAsync(gland.Id, new ScanImportRequest([CalcDemoProject.ScanFile()], CalcDemoProject.GlandProfile, CalcDemoProject.ScanSettings), cancellationToken).ConfigureAwait(false)).Action, "reading the scan into the gland");

        await _service.CreatePackAsync(projectId, $"{CalcDemoProject.NamePrefix}{CalcDemoProject.SealArea}", [stack.Id, gland.Id], cancellationToken).ConfigureAwait(false);
    }

    private static void Require(CalcActionResult result, string what)
    {
        if (!result.Succeeded)
            throw new InvalidOperationException($"{what} was refused: {result.Reason}");
    }

    /// <summary>Every calculation of the project runs (no block) and is current (not stale). Returns how many calculations there are.</summary>
    private async Task<int> VerifyAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var instances = await _service.Store.ListInstancesAsync(projectId, cancellationToken).ConfigureAwait(false);

        foreach (var instance in instances)
        {
            if (instance.Blocks.Count > 0)
                throw new InvalidOperationException($"'{instance.Name}' is blocked: {instance.Blocks[0].Message}");

            if ((await _service.StalenessAsync(instance, cancellationToken).ConfigureAwait(false)).IsStale)
                throw new InvalidOperationException($"'{instance.Name}' is not current with the calculations it takes from");
        }

        return instances.Count;
    }

    private async Task RollBackAsync(Guid? projectId)
    {
        if (projectId is { } id)
        {
            try
            {
                await RemoveProjectAsync(id, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // The install's own failure is the one reported; the project is flagged, so Remove finishes the job.
            }
        }
    }

    /// <summary>One audit row for the install or the removal. A removal is the only delete this feature makes, so it is the one that most needs a record that outlives it.</summary>
    private async Task RecordAsync(string action, int calculations, int packs)
    {
        if (_audit is null)
            return;

        await _audit.RecordAsync(action, new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Project"] = CalcDemoProject.Identifier,
            ["Calculations"] = calculations.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["Packs"] = packs.ToString(System.Globalization.CultureInfo.InvariantCulture),
        }).ConfigureAwait(false);
    }

    private static string Sentence(string message) => message.Trim().TrimEnd('.');

    // ------------------------------------------------------------------------------------------------------------------------------
    // Remove
    // ------------------------------------------------------------------------------------------------------------------------------

    /// <summary>Removes the demonstration project and everything in it, or says in a sentence why it did not. Every other project is left exactly as it was.</summary>
    public async Task<CalcDemoOutcome> RemoveAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var (demo, other) = await FindAsync(cancellationToken).ConfigureAwait(false);

            if (demo is null)
            {
                return CalcDemoOutcome.Refused(other is null
                    ? $"There is no demo project to remove ('{CalcDemoProject.DisplayName}' is not installed)."
                    : $"The project '{other.BusinessIdentifier}' is not the demo project, so it was not removed.");
            }

            try
            {
                return await RemoveProjectAsync(demo.Id, cancellationToken).ConfigureAwait(false);
            }
            catch (CalcInstanceImmutableException ex)
            {
                return CalcDemoOutcome.Refused($"The demo project was not removed, because an issued calculation or pack in it is never deleted ({Sentence(ex.Message)}).");
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>The removal itself, for a project already proved to be the demonstration: refuse if anything in it is issued, delete the rows in one transaction, then soft-delete the objects.</summary>
    private async Task<CalcDemoOutcome> RemoveProjectAsync(Guid projectId, CancellationToken cancellationToken)
    {
        // The refusal is made here, before anything changes, so a person is told which calculation is in the way; the store refuses too, below every service.
        foreach (var instance in await _service.Store.ListInstancesAsync(projectId, cancellationToken).ConfigureAwait(false))
        {
            if (instance.IsImmutable)
                return CalcDemoOutcome.Refused($"The demo project was not removed: '{instance.Name}' is {instance.Status.ToString().ToLowerInvariant()}, and an issued calculation is never deleted.");
        }

        foreach (var pack in await _service.Store.ListPacksAsync(projectId, cancellationToken).ConfigureAwait(false))
        {
            if (pack.Status is CalcInstanceStatus.Issued or CalcInstanceStatus.Superseded)
                return CalcDemoOutcome.Refused($"The demo project was not removed: the pack '{pack.Title}' is {pack.Status.ToString().ToLowerInvariant()}, and an issued pack is never deleted.");
        }

        var (instances, packs, _) = await _service.Store.DeleteDemonstrationRowsAsync(projectId, [CalcDemoProject.StackProfileId, CalcDemoProject.GlandProfileId], cancellationToken).ConfigureAwait(false);
        await DeleteTreeAsync(projectId, cancellationToken).ConfigureAwait(false);
        await RecordAsync("Demo.Removed", instances, packs).ConfigureAwait(false);
        return new CalcDemoOutcome(true, $"Removed the demo project '{CalcDemoProject.DisplayName}': {instances} calculations and {packs} packs.", instances, packs);
    }

    /// <summary>Soft-deletes <paramref name="id"/> and everything under it, deepest first (an object with a live child cannot be deleted).</summary>
    private async Task DeleteTreeAsync(Guid id, CancellationToken cancellationToken)
    {
        foreach (var child in await _domain.Repository.ListChildrenAsync(id, cancellationToken).ConfigureAwait(false))
        {
            if (!child.IsDeleted)
                await DeleteTreeAsync(child.Id, cancellationToken).ConfigureAwait(false);
        }

        if (await _domain.Repository.FindAsync(id, cancellationToken).ConfigureAwait(false) is IDeletable { IsDeleted: false } live)
            await live.DeleteAsync(cancellationToken).ConfigureAwait(false);
    }

    // ------------------------------------------------------------------------------------------------------------------------------

    /// <summary>The live project that is the demonstration, and a live project that merely holds its number.</summary>
    private async Task<(IEngineeringObject? Demo, IEngineeringObject? Other)> FindAsync(CancellationToken cancellationToken)
    {
        IEngineeringObject? demo = null;
        IEngineeringObject? other = null;

        foreach (var entry in await _domain.Repository.ListByKindAsync(CalcDemoProject.ProjectKind, cancellationToken).ConfigureAwait(false))
        {
            if (entry.IsDeleted || !string.Equals(entry.Identifier, CalcDemoProject.Identifier, StringComparison.OrdinalIgnoreCase))
                continue;

            if (await _domain.Repository.FindAsync(entry.Id, cancellationToken).ConfigureAwait(false) is not { } project)
                continue;

            if (CalcDemoProject.IsTheDemonstration(project))
                demo ??= project;
            else
                other ??= project;
        }

        return (demo, other);
    }
}
