namespace Tempest.Core.ReferenceData;

/// <summary>How a reference-data record's own values got from their source into TempestOS.</summary>
public enum ReferenceExtractionMethod
{
    /// <summary>Not recorded. The honest default.</summary>
    Unknown,

    /// <summary>Typed in by a person reading the source.</summary>
    ManualTranscription,

    /// <summary>Imported from a structured dataset the source itself published.</summary>
    StructuredImport,

    /// <summary>Extracted from an unstructured document by tooling, and therefore inherently in need of checking.</summary>
    AutomatedExtraction
}
