namespace Tempest.Calc.Import.Cad;

/// <summary>
/// The entry point for reading a CAD parameter file. Parsing never evaluates anything and never throws anything but
/// <see cref="ImportRefusedException"/> (or the caller's <see cref="OperationCanceledException"/>): a file the importer
/// will not read is refused with a code and a message, and a defect of the importer itself is reported as
/// <see cref="ImportRefusalCode.Internal"/> so that the fuzz tests can tell it from a refusal.
/// </summary>
public static class CadImporter
{
    /// <summary>The file extensions each format is offered for (with the dot, lower case).</summary>
    public static IReadOnlyList<CadSourceFormat> CandidatesFor(string fileName, ImportedFile? file = null)
    {
        var extension = Path.GetExtension(fileName ?? string.Empty).ToLowerInvariant();

        return extension switch
        {
            ".exp" => [CadSourceFormat.NxExpressions],
            ".xlsx" => [CadSourceFormat.NxXlsx, CadSourceFormat.SolidWorksDesignTable],
            ".csv" => [CadSourceFormat.GenericCsv, CadSourceFormat.OnshapeCsv],
            ".json" => [CadSourceFormat.GenericJson, CadSourceFormat.OnshapeJson],
            ".txt" or ".rel" or ".pro" or ".sldeqn" => SniffText(file),
            _ => [],
        };
    }

    /// <summary>Parses <paramref name="file"/> as <paramref name="format"/>.</summary>
    public static CadParseResult Parse(ImportedFile file, CadSourceFormat format, CadParseSettings? settings = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(file);
        settings ??= new CadParseSettings();

        if (!settings.Number.IsValid)
            throw new ImportRefusedException(ImportRefusalCode.NumberFormat, "The number format is not valid (the grouping character cannot be the decimal mark).");

        if (file.Bytes.Length > settings.Limits.MaxBytes)
            throw new ImportRefusedException(ImportRefusalCode.TooLarge, $"The file is larger than {settings.Limits.MaxBytes / (1024.0 * 1024.0):0.#} MB and was not read.");

        var budget = new ImportBudget(settings.Limits, cancellationToken);
        var context = new CadParseContext(file, settings, budget);

        try
        {
            return format switch
            {
                CadSourceFormat.NxExpressions => NxExpressionParser.Parse(context),
                CadSourceFormat.NxXlsx => NxXlsxParser.Parse(context),
                CadSourceFormat.CreoRelations => CreoParser.Parse(context),
                CadSourceFormat.SolidWorksEquations => SolidWorksEquationsParser.Parse(context),
                CadSourceFormat.SolidWorksDesignTable => SolidWorksDesignTableParser.Parse(context),
                CadSourceFormat.OnshapeCsv => TabularParametersParser.ParseCsv(context, CadSystem.Onshape, format),
                CadSourceFormat.OnshapeJson => TabularParametersParser.ParseJson(context, CadSystem.Onshape, format),
                CadSourceFormat.GenericCsv => TabularParametersParser.ParseCsv(context, CadSystem.Generic, format),
                CadSourceFormat.GenericJson => TabularParametersParser.ParseJson(context, CadSystem.Generic, format),
                _ => throw new ImportRefusedException(ImportRefusalCode.Unsupported, "That file format is not read."),
            };
        }
        catch (Exception e) when (e is not (ImportRefusedException or OperationCanceledException))
        {
            // The parsers are written to refuse, not to fail; anything else is a defect in them, and is reported as one.
            throw new ImportRefusedException(ImportRefusalCode.Internal, $"The file could not be read ({e.GetType().Name}). This is a fault in the importer, not in your file.");
        }
    }

    private static IReadOnlyList<CadSourceFormat> SniffText(ImportedFile? file)
    {
        if (file is null)
            return [CadSourceFormat.SolidWorksEquations, CadSourceFormat.CreoRelations];

        try
        {
            var text = BoundedInputReader.DecodeText(file);

            foreach (var (_, line) in CadLines.Of(text))
            {
                var trimmed = line.Trim();

                if (trimmed.Length == 0 || trimmed[0] == '\'')
                    continue;

                return trimmed[0] == '"' ? [CadSourceFormat.SolidWorksEquations, CadSourceFormat.CreoRelations] : [CadSourceFormat.CreoRelations, CadSourceFormat.SolidWorksEquations];
            }
        }
        catch (ImportRefusedException)
        {
            // Not text: the parse will say why.
        }

        return [CadSourceFormat.CreoRelations, CadSourceFormat.SolidWorksEquations];
    }
}
