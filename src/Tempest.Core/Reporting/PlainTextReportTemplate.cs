using System.Text;

namespace Tempest.Core.Reporting;

/// <summary>
/// A general-purpose <see cref="IReportTemplate{TDefinition}"/> that lays
/// out a report's own data as simple, human-readable
/// <c>"Key: Value"</c> lines under the definition's own name — the
/// platform's own ready-to-use template for any renderer that does not
/// need a more specific layout of its own.
/// </summary>
/// <remarks>
/// Genuinely reusable across any <typeparamref name="TDefinition"/> —
/// registered by any future report definition without that definition's
/// own renderer needing to write layout logic itself, satisfying this
/// Work Package's own "support reusable report templates" and "support
/// future extension by Engineering Modules" objectives concretely.
/// </remarks>
/// <typeparam name="TDefinition">The report definition type this template renders.</typeparam>
public sealed class PlainTextReportTemplate<TDefinition> : IReportTemplate<TDefinition> where TDefinition : IReportDefinition
{
    /// <inheritdoc />
    public string ContentType => "text/plain";

    /// <inheritdoc />
    public ReportResult Apply(TDefinition definition, ReportRequest request, IReadOnlyDictionary<string, string> data)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(data);

        var builder = new StringBuilder();
        builder.AppendLine(definition.Name);
        builder.AppendLine(new string('-', definition.Name.Length));

        foreach (var (key, value) in data)
            builder.AppendLine($"{key}: {value}");

        return new ReportResult(ContentType, Encoding.UTF8.GetBytes(builder.ToString()));
    }
}
