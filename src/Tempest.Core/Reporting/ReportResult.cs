namespace Tempest.Core.Reporting;

/// <summary>The rendered output of a report generation request. Immutable.</summary>
/// <param name="ContentType">The MIME content type of <paramref name="Content"/>.</param>
/// <param name="Content">The rendered bytes.</param>
public sealed record ReportResult(string ContentType, byte[] Content);
