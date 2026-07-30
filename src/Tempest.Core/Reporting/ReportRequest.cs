namespace Tempest.Core.Reporting;

/// <summary>Parameters for a single report generation request. Immutable.</summary>
/// <param name="Parameters">Caller-supplied, renderer-specific parameters for this request.</param>
public sealed record ReportRequest(IReadOnlyDictionary<string, string> Parameters);
