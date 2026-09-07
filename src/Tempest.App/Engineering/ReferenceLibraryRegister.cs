using Tempest.Core.Bearings;
using Tempest.Core.Constants;
using Tempest.Core.Fasteners;
using Tempest.Core.Manufacturing;
using Tempest.Core.Materials;
using Tempest.Core.ReferenceData;
using Tempest.Core.Standards;

namespace Tempest.App.Engineering;

/// <summary>One reference-data record, as the application sees it.</summary>
/// <param name="Library">The library the record belongs to.</param>
/// <param name="RecordId">Its stable identity.</param>
/// <param name="DisplayName">What to call it on screen.</param>
/// <param name="RevisionNumber">The revision it is currently at.</param>
/// <param name="ValidationState">Where it sits in the reference lifecycle.</param>
/// <param name="SourceOrganisation">Who published the data, or <see langword="null"/> where nothing is recorded.</param>
/// <param name="SourceDocument">The document it came from, or <see langword="null"/>.</param>
/// <param name="VerificationStatus">Whether anybody has checked it against that document.</param>
public sealed record ReferenceLibraryEntry(
    string Library,
    string RecordId,
    string DisplayName,
    int RevisionNumber,
    ReferenceValidationState ValidationState,
    string? SourceOrganisation,
    string? SourceDocument,
    ReferenceVerificationStatus VerificationStatus)
{
    /// <summary>Whether engineering work may rely on this record as authoritative reference data.</summary>
    public bool IsUsableAsAuthoritative => ValidationState == ReferenceValidationState.Released;

    /// <summary>Whether a person has checked this record's values against the source it cites.</summary>
    public bool IsVerified => VerificationStatus == ReferenceVerificationStatus.VerifiedAgainstSource;

    /// <summary>
    /// Why this record is not yet usable as authoritative reference data,
    /// or <see langword="null"/> where it is.
    /// </summary>
    /// <remarks>
    /// Stated as a sentence rather than left for each surface to compose
    /// from a state and a flag, because every surface would compose it
    /// slightly differently and an engineer would learn three different
    /// vocabularies for one fact.
    /// </remarks>
    public string? UnusableReason => ValidationState switch
    {
        ReferenceValidationState.Released => null,
        ReferenceValidationState.Superseded => "This record has been superseded. Work against the record that replaced it, or pin this one's revision explicitly.",
        _ when !IsVerified => $"This record is {ValidationState} and nobody has checked its values against {SourceDocument ?? "its source"}. It may be read, but engineering work must not rely on it as authoritative.",
        _ => $"This record is {ValidationState} and has not been released.",
    };
}

/// <summary>A summary of one reference library's own contents.</summary>
/// <param name="Library">The library's own name.</param>
/// <param name="Entries">Its records, in identity order.</param>
public sealed record ReferenceLibrarySummary(string Library, IReadOnlyList<ReferenceLibraryEntry> Entries)
{
    /// <summary>How many records the library holds.</summary>
    public int RecordCount => Entries.Count;

    /// <summary>How many are released and may be relied on.</summary>
    public int ReleasedCount => Entries.Count(e => e.IsUsableAsAuthoritative);

    /// <summary>Whether the library holds nothing at all.</summary>
    public bool IsEmpty => Entries.Count == 0;

    /// <summary>
    /// Whether the library holds records but none of them may yet be relied
    /// on — the state a freshly populated library is in, and a materially
    /// different thing from being empty.
    /// </summary>
    public bool IsPopulatedButUnusable => Entries.Count > 0 && ReleasedCount == 0;
}

/// <summary>What reference data the application currently holds.</summary>
public interface IReferenceLibraryRegister
{
    /// <summary>Every P01 reference library, with its records.</summary>
    Task<IReadOnlyList<ReferenceLibrarySummary>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>One library's own records, or an empty summary where the library name is not one this register knows.</summary>
    Task<ReferenceLibrarySummary> ListLibraryAsync(string library, CancellationToken cancellationToken = default);
}

/// <summary>
/// The read model behind any surface that needs to show what reference data
/// TempestOS holds and whether it may be used.
/// </summary>
/// <remarks>
/// <para>
/// <b>A read model, not a store.</b> It holds no state, caches nothing and
/// creates no persistence — exactly as <c>ProjectTaskRegister</c> and
/// <c>ProjectMilestoneRegister</c> do for their own domains. Everything it
/// reports is read from the governed catalogues, so a record corrected
/// anywhere in the product shows correctly here without this class knowing
/// the correction happened.
/// </para>
/// <para>
/// <b>It reports lifecycle state prominently, because that is the question.</b>
/// A populated library and a usable library are not the same thing, and the
/// difference is the whole of what the reference lifecycle exists to
/// protect. A surface that listed 79 records without saying that none of
/// them is released would be actively misleading, so
/// <see cref="ReferenceLibraryEntry.UnusableReason"/> is computed here once
/// rather than left to each caller.
/// </para>
/// <para>
/// <b>The libraries are named explicitly, not discovered.</b> Reflecting
/// over every registered catalogue would make this class quietly acquire
/// new libraries as the platform grows, which sounds convenient and means
/// nobody ever decides what an engineer sees. Six named dependencies is a
/// decision that can be reviewed.
/// </para>
/// </remarks>
public sealed class ReferenceLibraryRegister : IReferenceLibraryRegister
{
    private readonly IStandardCatalog _standards;
    private readonly IMaterialCatalog _materials;
    private readonly IConstantCatalog _constants;
    private readonly IFastenerCatalog _fasteners;
    private readonly IBearingCatalog _bearings;
    private readonly IProcessCatalog _processes;

    /// <summary>Initialises a new instance of the <see cref="ReferenceLibraryRegister"/> class.</summary>
    /// <param name="standards">The Standards Library.</param>
    /// <param name="materials">The Materials Library.</param>
    /// <param name="constants">The Constants Library.</param>
    /// <param name="fasteners">The Fastener Library.</param>
    /// <param name="bearings">The Bearing Library.</param>
    /// <param name="processes">The Manufacturing Process Library.</param>
    public ReferenceLibraryRegister(
        IStandardCatalog standards,
        IMaterialCatalog materials,
        IConstantCatalog constants,
        IFastenerCatalog fasteners,
        IBearingCatalog bearings,
        IProcessCatalog processes)
    {
        ArgumentNullException.ThrowIfNull(standards);
        ArgumentNullException.ThrowIfNull(materials);
        ArgumentNullException.ThrowIfNull(constants);
        ArgumentNullException.ThrowIfNull(fasteners);
        ArgumentNullException.ThrowIfNull(bearings);
        ArgumentNullException.ThrowIfNull(processes);

        _standards = standards;
        _materials = materials;
        _constants = constants;
        _fasteners = fasteners;
        _bearings = bearings;
        _processes = processes;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ReferenceLibrarySummary>> ListAsync(CancellationToken cancellationToken = default) =>
    [
        await SummariseAsync(_standards, d => d.FullDesignation, cancellationToken).ConfigureAwait(false),
        await SummariseAsync(_materials, d => d.Name, cancellationToken).ConfigureAwait(false),
        await SummariseAsync(_constants, d => $"{d.Symbol} — {d.Name}", cancellationToken).ConfigureAwait(false),
        await SummariseAsync(_fasteners, d => d.Designation, cancellationToken).ConfigureAwait(false),
        await SummariseAsync(_bearings, d => $"{d.Identity.Manufacturer} {d.Identity.ManufacturerPartNumber}", cancellationToken).ConfigureAwait(false),
        await SummariseAsync(_processes, d => d.Variant is null ? d.Name : $"{d.Name} ({d.Variant})", cancellationToken).ConfigureAwait(false),
    ];

    /// <inheritdoc />
    public async Task<ReferenceLibrarySummary> ListLibraryAsync(string library, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(library);

        var all = await ListAsync(cancellationToken).ConfigureAwait(false);

        // An unknown library name returns an empty summary rather than
        // throwing: a surface asking about a library this build does not
        // have is asking a reasonable question, and "nothing" is the honest
        // answer to it.
        return all.FirstOrDefault(s => string.Equals(s.Library, library, StringComparison.OrdinalIgnoreCase))
            ?? new ReferenceLibrarySummary(library, []);
    }

    private static async Task<ReferenceLibrarySummary> SummariseAsync<TDefinition>(
        IReferenceDataCatalog<TDefinition> catalog,
        Func<TDefinition, string> displayName,
        CancellationToken cancellationToken)
        where TDefinition : class
    {
        var records = await catalog.ListAsync(cancellationToken).ConfigureAwait(false);

        return new ReferenceLibrarySummary(
            catalog.LibraryName,
            [
                .. records
                    .Select(r => new ReferenceLibraryEntry(
                        catalog.LibraryName,
                        r.Id,
                        displayName(r.Definition),
                        r.RevisionNumber,
                        r.ValidationState,
                        r.Provenance.SourceOrganisation,
                        r.Provenance.SourceDocument,
                        r.Provenance.VerificationStatus))
                    .OrderBy(e => e.RecordId, StringComparer.Ordinal),
            ]);
    }
}
