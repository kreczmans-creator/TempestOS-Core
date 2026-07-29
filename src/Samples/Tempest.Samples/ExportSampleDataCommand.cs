using Tempest.Core.Commands;

namespace Tempest.Samples;

/// <summary>
/// A reference command whose handler exports both of
/// <see cref="ExportImportSampleModule"/>'s own sample settings, as a
/// single, multi-source artifact, through
/// <see cref="Tempest.Core.ExportImport.IExportService"/> — see
/// <see cref="ExportSampleDataCommandHandler"/>.
/// </summary>
/// <remarks>
/// Carries no data — the sample export takes no caller-supplied parameters.
/// </remarks>
public sealed class ExportSampleDataCommand : ICommand
{
}
