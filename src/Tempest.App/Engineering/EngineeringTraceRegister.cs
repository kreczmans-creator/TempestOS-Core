using Tempest.Core.EngineeringAssets.CalculationPacks;
using Tempest.Core.EngineeringAssets.Templates;
using Tempest.Core.Materials;
using Tempest.Core.ReferenceData;

namespace Tempest.App.Engineering;

/// <summary>
/// One reference record an engineering artefact stood on, resolved as it
/// was at the revision the artefact pinned.
/// </summary>
/// <param name="Library">The library the pin names.</param>
/// <param name="RecordId">The record the pin names.</param>
/// <param name="PinnedRevision">The revision the artefact recorded using.</param>
/// <param name="IsResolved">Whether the pinned revision was actually found.</param>
/// <param name="UnresolvedReason">Why it was not found, or <see langword="null"/> where it was.</param>
/// <param name="DisplayName">The record's own name at the pinned revision.</param>
/// <param name="CurrentRevision">The revision the record is at now.</param>
/// <param name="ValidationStateNow">The record's lifecycle state now.</param>
/// <param name="SourceOrganisation">Who published the data.</param>
/// <param name="SourceDocument">The document it came from.</param>
/// <param name="SourceLocation">Where in that document.</param>
/// <param name="VerificationStatus">Whether a person has checked it against that document.</param>
public sealed record TracedReference(
    string Library,
    string RecordId,
    int PinnedRevision,
    bool IsResolved,
    string? UnresolvedReason,
    string? DisplayName = null,
    int? CurrentRevision = null,
    ReferenceValidationState? ValidationStateNow = null,
    string? SourceOrganisation = null,
    string? SourceDocument = null,
    string? SourceLocation = null,
    ReferenceVerificationStatus? VerificationStatus = null)
{
    /// <summary>
    /// Whether the record has moved on since the artefact pinned it.
    /// </summary>
    /// <remarks>
    /// Not a defect and not a warning by itself. Reference data is supposed
    /// to be corrected, and the whole point of pinning is that doing so
    /// leaves earlier work intact. What it tells an engineer is that
    /// repeating the work today would read different numbers.
    /// </remarks>
    public bool HasMovedOnSincePinned => CurrentRevision is { } current && current > PinnedRevision;

    /// <summary>Whether the pinned data had been checked against its source by a person.</summary>
    public bool WasVerified => VerificationStatus == ReferenceVerificationStatus.VerifiedAgainstSource;

    /// <summary>A one-line citation an engineer can read or paste into a report.</summary>
    public string Citation => IsResolved
        ? $"{DisplayName ?? RecordId} ({Library}/{RecordId} r{PinnedRevision}) — {SourceOrganisation ?? "source not recorded"}"
            + (SourceDocument is null ? string.Empty : $", {SourceDocument}")
            + (SourceLocation is null ? string.Empty : $", {SourceLocation}")
        : $"{Library}/{RecordId} r{PinnedRevision} — UNRESOLVED: {UnresolvedReason}";
}

/// <summary>One input to a calculation, with the reference it came from.</summary>
/// <param name="Reference">The input's own reference within the pack.</param>
/// <param name="Description">What the input is.</param>
/// <param name="Value">The value the pack recorded, verbatim.</param>
/// <param name="SourceDescription">What the pack says about where the value came from.</param>
/// <param name="TracedTo">The reference record it pins, or <see langword="null"/> where the input pins none.</param>
public sealed record TracedInput(
    string Reference,
    string Description,
    string Value,
    string? SourceDescription,
    TracedReference? TracedTo)
{
    /// <summary>
    /// Whether this input's value can be traced to a governed reference
    /// record rather than resting on the pack's own word.
    /// </summary>
    public bool IsTraceable => TracedTo is { IsResolved: true };
}

/// <summary>The full answer to "where did this engineering result come from?".</summary>
/// <param name="PackReference">The calculation pack's own reference.</param>
/// <param name="Title">Its title.</param>
/// <param name="Inputs">Every input, with the reference each pins.</param>
/// <param name="TemplateUsed">The template revision the pack was recorded on, where it names one.</param>
/// <param name="Outputs">What the pack reports, verbatim.</param>
public sealed record CalculationTrace(
    string PackReference,
    string Title,
    IReadOnlyList<TracedInput> Inputs,
    TracedReference? TemplateUsed,
    IReadOnlyList<string> Outputs)
{
    /// <summary>Every reference this calculation stands on, resolved or not.</summary>
    public IReadOnlyList<TracedReference> AllReferences =>
    [
        .. Inputs.Select(i => i.TracedTo).Where(r => r is not null).Select(r => r!),
        .. TemplateUsed is null ? Array.Empty<TracedReference>() : [TemplateUsed],
    ];

    /// <summary>Whether every reference this calculation names was found.</summary>
    public bool IsFullyResolved => AllReferences.All(r => r.IsResolved);

    /// <summary>References this calculation names that could not be found — a real integrity problem.</summary>
    public IReadOnlyList<TracedReference> DanglingReferences => [.. AllReferences.Where(r => !r.IsResolved)];

    /// <summary>References whose record has been revised since this calculation pinned it.</summary>
    public IReadOnlyList<TracedReference> StaleReferences => [.. AllReferences.Where(r => r.HasMovedOnSincePinned)];

    /// <summary>
    /// Inputs whose value rests on nothing but the pack's own assertion.
    /// </summary>
    /// <remarks>
    /// Reported rather than hidden, because an untraceable input is the
    /// thing a reviewer most needs to see. In the seed corpus this is not a
    /// defect — the bracket pack deliberately records a design load and an
    /// area that nobody has established — but it is exactly the state that
    /// must not pass silently into a released calculation.
    /// </remarks>
    public IReadOnlyList<TracedInput> UntraceableInputs => [.. Inputs.Where(i => !i.IsTraceable)];

    /// <summary>
    /// Whether every reference was found and every one of them had been
    /// verified against its own source by a person.
    /// </summary>
    public bool RestsEntirelyOnVerifiedData => IsFullyResolved && AllReferences.All(r => r.WasVerified);
}

/// <summary>Answers "where did this engineering result come from?".</summary>
public interface IEngineeringTraceRegister
{
    /// <summary>
    /// The full provenance chain behind one calculation pack, or
    /// <see langword="null"/> where no pack is registered under
    /// <paramref name="packRecordId"/>.
    /// </summary>
    Task<CalculationTrace?> TraceCalculationAsync(string packRecordId, CancellationToken cancellationToken = default);
}

/// <summary>
/// The read model that walks an engineering artefact back to the reference
/// revisions it stood on, and those revisions back to the documents they
/// were transcribed from.
/// </summary>
/// <remarks>
/// <para>
/// <b>It resolves the pinned revision, not the current one.</b> That is the
/// entire point. Asking a material catalogue what 6082-T6's proof stress is
/// today answers a different question from asking what the calculation
/// used, and only the second one lets an engineer reproduce work done two
/// years ago. Where the two differ, both are reported —
/// <see cref="TracedReference.HasMovedOnSincePinned"/> — rather than the
/// difference being smoothed away.
/// </para>
/// <para>
/// <b>It knows two libraries, and says so when it meets a third.</b> The
/// seed corpus pins materials and templates and nothing else. A pin naming
/// any other library resolves to
/// <see cref="TracedReference.IsResolved"/> <see langword="false"/> with a
/// reason, which is a visible gap rather than a silently dropped
/// reference. Adding a library here is a deliberate edit, which is the
/// point: a traceability surface that quietly grew to cover whatever it
/// found would be one nobody had decided the scope of.
/// </para>
/// <para>
/// <b>No bespoke traceability mechanism.</b> Everything here is read
/// through <see cref="IReferenceDataCatalog{TDefinition}.GetRevisionAsync"/>
/// and the pins the assets already carry. This class composes what the
/// platform already records; it stores nothing and it is not a second
/// source of truth.
/// </para>
/// </remarks>
public sealed class EngineeringTraceRegister : IEngineeringTraceRegister
{
    private readonly ICalculationPackCatalog _packs;
    private readonly IMaterialCatalog _materials;
    private readonly ITemplateCatalog _templates;

    /// <summary>Initialises a new instance of the <see cref="EngineeringTraceRegister"/> class.</summary>
    /// <param name="packs">The calculation pack library.</param>
    /// <param name="materials">The Materials Library, which calculation inputs pin.</param>
    /// <param name="templates">The template library, which calculation packs pin.</param>
    public EngineeringTraceRegister(
        ICalculationPackCatalog packs,
        IMaterialCatalog materials,
        ITemplateCatalog templates)
    {
        ArgumentNullException.ThrowIfNull(packs);
        ArgumentNullException.ThrowIfNull(materials);
        ArgumentNullException.ThrowIfNull(templates);

        _packs = packs;
        _materials = materials;
        _templates = templates;
    }

    /// <inheritdoc />
    public async Task<CalculationTrace?> TraceCalculationAsync(string packRecordId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packRecordId);

        var pack = await _packs.FindAsync(packRecordId, cancellationToken).ConfigureAwait(false);
        if (pack is null)
            return null;

        var inputs = new List<TracedInput>(pack.Definition.Inputs.Count);

        foreach (var input in pack.Definition.Inputs)
        {
            cancellationToken.ThrowIfCancellationRequested();

            inputs.Add(new TracedInput(
                input.Reference,
                input.Description,
                input.Value,
                input.SourceDescription,
                input.SourcePin is null
                    ? null
                    : await ResolveAsync(input.SourcePin, cancellationToken).ConfigureAwait(false)));
        }

        var template = pack.Definition.TemplateUsage?.TemplatePin is { } templatePin
            ? await ResolveAsync(templatePin, cancellationToken).ConfigureAwait(false)
            : null;

        return new CalculationTrace(
            pack.Definition.Reference,
            pack.Definition.Title,
            inputs,
            template,
            [.. pack.Definition.Outputs.Select(o => $"{o.Reference}: {o.Description} = {o.Value}")]);
    }

    private async Task<TracedReference> ResolveAsync(ReferencePin pin, CancellationToken cancellationToken)
    {
        if (string.Equals(pin.Library, _materials.LibraryName, StringComparison.Ordinal))
            return await ResolveFromAsync(_materials, pin, d => d.Name, cancellationToken).ConfigureAwait(false);

        if (string.Equals(pin.Library, _templates.LibraryName, StringComparison.Ordinal))
            return await ResolveFromAsync(_templates, pin, d => d.Name, cancellationToken).ConfigureAwait(false);

        return new TracedReference(
            pin.Library,
            pin.RecordId,
            pin.RevisionNumber,
            IsResolved: false,
            UnresolvedReason: $"This register resolves pins into '{_materials.LibraryName}' and "
                + $"'{_templates.LibraryName}' only, and the pin names '{pin.Library}'.");
    }

    private static async Task<TracedReference> ResolveFromAsync<TDefinition>(
        IReferenceDataCatalog<TDefinition> catalog,
        ReferencePin pin,
        Func<TDefinition, string> displayName,
        CancellationToken cancellationToken)
        where TDefinition : class
    {
        var current = await catalog.FindAsync(pin.RecordId, cancellationToken).ConfigureAwait(false);
        if (current is null)
        {
            return new TracedReference(
                pin.Library,
                pin.RecordId,
                pin.RevisionNumber,
                IsResolved: false,
                UnresolvedReason: $"No record '{pin.RecordId}' is registered in {catalog.LibraryName}.");
        }

        IReferenceRecord<TDefinition> pinned;
        try
        {
            pinned = await catalog.GetRevisionAsync(pin.RecordId, pin.RevisionNumber, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is ReferenceDataException or ArgumentOutOfRangeException)
        {
            // The record exists but the pinned revision does not — either
            // the history is missing, or the pin names a revision that was
            // never written. Both are genuine integrity failures, and both
            // are reported rather than quietly falling back to the current
            // revision, which would silently answer a different question
            // from the one asked.
            //
            // Two exception types, because the catalogue distinguishes
            // them: a revision outside the range that exists is an
            // ArgumentOutOfRangeException, while a stored revision that
            // cannot be read is a ReferenceDataException. A traceability
            // surface that let either escape would turn a reportable broken
            // pin into a crash in whatever surface asked the question.
            return new TracedReference(
                pin.Library,
                pin.RecordId,
                pin.RevisionNumber,
                IsResolved: false,
                UnresolvedReason: $"Revision {pin.RevisionNumber} of '{pin.RecordId}' could not be read: {exception.Message}",
                CurrentRevision: current.RevisionNumber,
                ValidationStateNow: current.ValidationState);
        }

        return new TracedReference(
            pin.Library,
            pin.RecordId,
            pin.RevisionNumber,
            IsResolved: true,
            UnresolvedReason: null,
            DisplayName: displayName(pinned.Definition),
            CurrentRevision: current.RevisionNumber,
            ValidationStateNow: current.ValidationState,
            SourceOrganisation: pinned.Provenance.SourceOrganisation,
            SourceDocument: pinned.Provenance.SourceDocument,
            SourceLocation: pinned.Provenance.SourceLocation,
            VerificationStatus: pinned.Provenance.VerificationStatus);
    }
}
