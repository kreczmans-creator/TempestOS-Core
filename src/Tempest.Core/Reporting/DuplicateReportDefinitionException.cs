namespace Tempest.Core.Reporting;

/// <summary>
/// Thrown when <see cref="IReportingService.RegisterDefinition{TDefinition}"/>
/// is called for an Id that already has a registered definition.
/// </summary>
/// <remarks>
/// First registration wins; a colliding, later registration is rejected —
/// never a silent override, mirroring
/// <see cref="Settings.DuplicateSettingDefinitionException"/>'s own
/// convention.
/// </remarks>
public sealed class DuplicateReportDefinitionException : ReportingException
{
    /// <summary>
    /// Initialises a new instance of the <see cref="DuplicateReportDefinitionException"/> class.
    /// </summary>
    /// <param name="definitionId">The report definition Id that already has a registered definition.</param>
    public DuplicateReportDefinitionException(string definitionId)
        : base($"A report definition is already registered under Id '{definitionId}'.")
    {
        DefinitionId = definitionId;
    }

    /// <summary>
    /// Gets the report definition Id that already has a registered definition.
    /// </summary>
    public string DefinitionId { get; }
}
