namespace Tempest.Core.ExportImport;

/// <summary>
/// Thrown when <see cref="ImportService.RegisterImportable"/> is called for
/// a <see cref="IImportable.Kind"/> that already has a registered importable.
/// </summary>
/// <remarks>
/// First registration wins; a colliding, later registration is rejected —
/// never a silent override, mirroring
/// <see cref="Reporting.DuplicateReportDefinitionException"/>'s and
/// <see cref="Api.DuplicateApiRouteException"/>'s own convention. Not part
/// of the original architecture's <c>Public Interface Catalogue.md</c>
/// draft — a direct consequence of this Work Package's own additive
/// <see cref="IImportable"/> registration mechanism (see
/// <see cref="ImportService"/>'s own remarks).
/// </remarks>
public sealed class DuplicateImportableKindException : ExportImportException
{
    /// <summary>
    /// Initialises a new instance of the <see cref="DuplicateImportableKindException"/> class.
    /// </summary>
    /// <param name="kind">The kind that already has a registered importable.</param>
    public DuplicateImportableKindException(string kind)
        : base($"An importable is already registered under kind '{kind}'.")
    {
        Kind = kind;
    }

    /// <summary>Gets the kind that already has a registered importable.</summary>
    public string Kind { get; }
}
