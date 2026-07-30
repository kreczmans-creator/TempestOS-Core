using Tempest.Core.Reporting;

namespace Tempest.Core.Tests.Reporting;

internal sealed class RecordedReportDefinitionA : IReportDefinition
{
    public RecordedReportDefinitionA(string id = "definition.a") => Id = id;

    public string Id { get; }

    public string Name => "Recorded Report A";
}

internal sealed class RecordedReportDefinitionB : IReportDefinition
{
    public string Id => "definition.b";

    public string Name => "Recorded Report B";
}

/// <summary>
/// A configurable <see cref="IReportRenderer{TDefinition}"/> that records
/// every render request it receives, in order, and optionally runs a
/// caller-supplied callback to compute the result (or throw).
/// </summary>
internal sealed class RecordingRenderer<TDefinition> : IReportRenderer<TDefinition>
    where TDefinition : IReportDefinition
{
    private readonly Func<TDefinition, ReportRequest, CancellationToken, Task<ReportResult>>? _onRender;

    public RecordingRenderer(Func<TDefinition, ReportRequest, CancellationToken, Task<ReportResult>>? onRender = null)
    {
        _onRender = onRender;
    }

    public List<ReportRequest> Received { get; } = [];

    public Task<ReportResult> RenderAsync(TDefinition definition, ReportRequest request, CancellationToken cancellationToken = default)
    {
        Received.Add(request);

        return _onRender?.Invoke(definition, request, cancellationToken)
            ?? Task.FromResult(new ReportResult("text/plain", [1, 2, 3]));
    }
}
