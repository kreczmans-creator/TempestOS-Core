using Tempest.Core.Reporting;
using Tempest.Core.Settings;

namespace Tempest.Samples;

/// <summary>
/// Renders <see cref="SampleSummaryReportDefinition"/> by gathering a
/// small amount of report data — a greeting read from Settings, the
/// current UTC time, and every request parameter — then delegating
/// layout and rendering to an injected <see cref="IReportTemplate{TDefinition}"/>.
/// </summary>
/// <remarks>
/// Depends on <see cref="ISettingsProvider"/> directly, as an ordinary
/// peer dependency of this renderer's own business logic — exactly the
/// pattern `Platform Service Contracts.md`'s own Configuration
/// Requirements dimension for Reporting anticipated ("that configuration
/// belongs to the renderer, not to <see cref="IReportingService"/>
/// itself"). Demonstrates the Template Strategy's own required
/// separation: this renderer gathers data only; <see cref="IReportTemplate{TDefinition}"/>
/// owns layout and rendering.
/// </remarks>
public sealed class SampleSummaryReportRenderer : IReportRenderer<SampleSummaryReportDefinition>
{
    /// <summary>The setting key this renderer reads for its own greeting line.</summary>
    public const string GreetingSettingKey = "sample.reporting.greeting";

    /// <summary><see cref="GreetingSettingKey"/>'s own default value.</summary>
    public const string GreetingSettingDefaultValue = "Sample Summary Report";

    private readonly ISettingsProvider _settingsProvider;
    private readonly IReportTemplate<SampleSummaryReportDefinition> _template;

    /// <summary>
    /// Initialises a new instance of the <see cref="SampleSummaryReportRenderer"/> class.
    /// </summary>
    /// <param name="settingsProvider">The Settings service this renderer reads its own greeting from.</param>
    /// <param name="template">The template this renderer delegates layout and rendering to.</param>
    public SampleSummaryReportRenderer(ISettingsProvider settingsProvider, IReportTemplate<SampleSummaryReportDefinition> template)
    {
        ArgumentNullException.ThrowIfNull(settingsProvider);
        ArgumentNullException.ThrowIfNull(template);

        _settingsProvider = settingsProvider;
        _template = template;
    }

    /// <inheritdoc />
    public async Task<ReportResult> RenderAsync(SampleSummaryReportDefinition definition, ReportRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(request);

        var greeting = await _settingsProvider.GetValueAsync(GreetingSettingKey, cancellationToken).ConfigureAwait(false);

        var data = new Dictionary<string, string>
        {
            ["Greeting"] = greeting,
            ["GeneratedAtUtc"] = DateTimeOffset.UtcNow.ToString("O"),
        };

        foreach (var parameter in request.Parameters)
            data[$"Parameter.{parameter.Key}"] = parameter.Value;

        return _template.Apply(definition, request, data);
    }
}
