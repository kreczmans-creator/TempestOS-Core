using Tempest.Core.Commands;

namespace Tempest.Samples;

/// <summary>
/// A reference command whose handler re-imports the artifact most recently
/// produced by <see cref="ExportSampleDataCommandHandler"/>, through
/// <see cref="Tempest.Core.ExportImport.IImportService"/> — see
/// <see cref="ImportSampleDataCommandHandler"/>.
/// </summary>
/// <remarks>
/// Carries no data — the sample import takes no caller-supplied parameters.
/// </remarks>
public sealed class ImportSampleDataCommand : ICommand
{
}
