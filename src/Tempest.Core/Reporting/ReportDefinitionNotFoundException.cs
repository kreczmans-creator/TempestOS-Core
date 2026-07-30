namespace Tempest.Core.Reporting;

/// <summary>
/// Thrown when <see cref="IReportingService.GenerateAsync"/> is called
/// for an Id with no registered definition.
/// </summary>
public sealed class ReportDefinitionNotFoundException : ReportingException
{
    /// <summary>
    /// Initialises a new instance of the <see cref="ReportDefinitionNotFoundException"/> class.
    /// </summary>
    /// <param name="definitionId">The report definition Id that has no registered definition.</param>
    public ReportDefinitionNotFoundException(string definitionId)
        : base($"No report definition is registered under Id '{definitionId}'.")
    {
        DefinitionId = definitionId;
    }

    /// <summary>
    /// Gets the report definition Id that has no registered definition.
    /// </summary>
    public string DefinitionId { get; }
}
