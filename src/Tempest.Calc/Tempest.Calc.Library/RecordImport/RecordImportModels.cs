using Tempest.Calc.Import;

namespace Tempest.Calc.Library;

/// <summary>
/// The provenance of a record import, as Track P's <c>_provenance.csv</c> has it: every field except the amendment and the notes is required (a
/// record without its title, source, edition, table or clause and licence holder is refused), and the source is licensed unless the person says otherwise.
/// Who entered it is not a field: it is the signed-in person, stamped on import.
/// </summary>
/// <param name="Title">The record's title as it appears on sheets. A template that makes one record per row appends the row's labels in brackets.</param>
/// <param name="SourceTitle">The title of the publication typed from.</param>
/// <param name="Edition">The edition or year printed on the copy.</param>
/// <param name="Amendment">The amendment, corrigendum or addendum; blank when there is none (as Track P's file has it). It is joined to the edition in the record's one edition field.</param>
/// <param name="TableOrClause">The table, figure or clause copied from.</param>
/// <param name="LicenceHolder">The person or company that holds the licence for the copy.</param>
/// <param name="LicensedSource">Whether the values come from a licensed source; they never leave through an export if so. Default <see langword="true"/>.</param>
/// <param name="Origin">How the values came to be: typed from a copy, derived, or one attributed manufacturer example.</param>
/// <param name="Notes">Anything the next reader needs, such as the reference temperature.</param>
public sealed record RecordProvenanceInput(
    string Title,
    string SourceTitle,
    string Edition,
    string Amendment,
    string TableOrClause,
    string LicenceHolder,
    bool LicensedSource = true,
    CalcDataOrigin Origin = CalcDataOrigin.UserEntered,
    string? Notes = null)
{
    /// <summary>The edition and amendment as one field, the way the record keeps them.</summary>
    public string EditionAndAmendment =>
        string.IsNullOrWhiteSpace(Amendment) ? (Edition ?? string.Empty).Trim() : string.IsNullOrWhiteSpace(Edition) ? Amendment.Trim() : $"{Edition.Trim()}; {Amendment.Trim()}";

    /// <summary>Why the provenance cannot be stamped on a record, or <see langword="null"/>. A material import takes each record's title from the file, so <paramref name="titleOptional"/> lets the title be blank there.</summary>
    public string? Problem(bool titleOptional = false)
    {
        foreach (var (field, text) in new[]
                 {
                     ("title", Title), ("source title", SourceTitle), ("edition", Edition), ("table or clause", TableOrClause), ("licence holder", LicenceHolder),
                 })
        {
            if (titleOptional && field == "title" && string.IsNullOrWhiteSpace(text))
                continue;

            if (string.IsNullOrWhiteSpace(text))
                return $"The provenance needs its {field}.";

            if (text.Length > CalcDataRecord.MaxTextLength)
                return $"The provenance's {field} is longer than {CalcDataRecord.MaxTextLength} characters.";
        }

        if (Amendment?.Length > CalcDataRecord.MaxTextLength)
            return $"The provenance's amendment is longer than {CalcDataRecord.MaxTextLength} characters.";

        if (EditionAndAmendment.Length > CalcDataRecord.MaxTextLength)
            return $"The edition and amendment together are longer than {CalcDataRecord.MaxTextLength} characters.";

        if (Origin == CalcDataOrigin.Indicative)
            return "A record you enter cannot be marked indicative: that flag is for the shipped generic seeds.";

        return null;
    }
}

/// <summary>A table typed on the screen written as the CSV the importer reads, with the typed row each data line came from (1-based, line 2 first).</summary>
public sealed record RecordGridCsv(byte[] Bytes, IReadOnlyList<int> GridRowOfLine);

/// <summary>One row of a Track P <c>_provenance.csv</c> as read: the kind it names and the provenance fields.</summary>
public sealed record RecordProvenanceRow(string Kind, RecordProvenanceInput Provenance, string EnteredBy);

/// <summary>What to import: the file, which record template it follows, its provenance, and how to read it.</summary>
/// <param name="Bytes">The file as read (through the bounded reader), or the text typed on the screen, encoded as UTF-8.</param>
/// <param name="FileName">The file's name for the record's notes; <see langword="null"/> for a table typed on the screen (no file hash is stamped).</param>
/// <param name="TemplateId">The record template's number (<c>04</c>).</param>
/// <param name="Provenance">The provenance stamped on every record.</param>
/// <param name="Number">The decimal mark the file's numbers use. Nothing is guessed from the machine's culture.</param>
/// <param name="Delimiter">The field delimiter; <see langword="null"/> takes it from the header row (comma, semicolon or tab).</param>
/// <param name="ColumnMap">File header → template column symbol, for a file whose headers differ from the template's; a <see langword="null"/> value leaves that file column out. A header not listed is mapped by its own symbol.</param>
/// <param name="AsRevision">When a record of this kind with the same title exists, save the import as its next revision. Without this, such a title is refused.</param>
public sealed record RecordImportRequest(
    byte[] Bytes,
    string? FileName,
    string TemplateId,
    RecordProvenanceInput Provenance,
    NumberProfile? Number = null,
    char? Delimiter = null,
    IReadOnlyDictionary<string, string?>? ColumnMap = null,
    bool AsRevision = false);

/// <summary>How a file column's unit stands against the unit the calculator takes.</summary>
public enum RecordUnitCheck
{
    /// <summary>No unit is involved (a text column, or a dimensionless one).</summary>
    NotApplicable,

    /// <summary>The unit is the one the calculator takes.</summary>
    Match,

    /// <summary>A unit of the same kind: the values are kept as typed, and checked in the calculator's unit.</summary>
    Convertible,

    /// <summary>The column needs a unit and none was given.</summary>
    Missing,

    /// <summary>The unit measures something else, or the column's type (text or number) is not what the calculator takes.</summary>
    Mismatch,

    /// <summary>The unit is not one the engine knows.</summary>
    Unknown,
}

/// <summary>What a column of the file becomes.</summary>
public enum RecordColumnRole
{
    /// <summary>Left out: the person mapped it to nothing.</summary>
    Ignored,

    /// <summary>A scalar symbol of the kind (<c>C_a</c>): saved as a value of the record.</summary>
    Scalar,

    /// <summary>A column of one of the kind's tables: saved in the rows of that table value.</summary>
    TableColumn,

    /// <summary>A text that names the row (a grade, a device): it joins the title and is stored.</summary>
    Label,

    /// <summary>A number that describes the row but no calculator reads (a thickness band): stored, never applied.</summary>
    Extra,

    /// <summary>The material name of a material template: it is the record's title.</summary>
    MaterialName,

    /// <summary>A material property, or a column of a material table.</summary>
    MaterialProperty,
}

/// <summary>One column of the file as the importer reads it.</summary>
/// <param name="Index">The position in the header row, from 0.</param>
/// <param name="Header">The header cell as written.</param>
/// <param name="Symbol">The symbol part of the header.</param>
/// <param name="FileUnit">The unit as the header states it (empty when it does not).</param>
/// <param name="TemplateSymbol">The template column it is mapped to, or <see langword="null"/> when it is left out.</param>
/// <param name="Role">What the column becomes.</param>
/// <param name="Target">Where it lands: the scalar symbol, or <c>table.column</c>.</param>
/// <param name="ExpectedUnit">The unit the calculator takes (in the engine's notation); empty for a text or dimensionless one.</param>
/// <param name="Unit">The unit check.</param>
/// <param name="UnitMessage">What the unit check found, in words.</param>
public sealed record RecordImportColumn(
    int Index,
    string Header,
    string Symbol,
    string FileUnit,
    string? TemplateSymbol,
    RecordColumnRole Role,
    string? Target,
    string ExpectedUnit,
    RecordUnitCheck Unit,
    string UnitMessage);

/// <summary>One thing the preview found: an error stops the import, a warning does not.</summary>
/// <param name="Severity">Error or warning.</param>
/// <param name="Line">The file line, or <see langword="null"/> for a finding about the file or a column.</param>
/// <param name="Column">The column symbol it concerns, if any.</param>
/// <param name="Message">One plain sentence.</param>
public sealed record RecordImportIssue(RecordSeverity Severity, int? Line, string? Column, string Message)
{
    public override string ToString() => $"{(Severity == RecordSeverity.Error ? "Error" : "Warning")}{(Line is { } l ? $", line {l}" : string.Empty)}{(Column is { } c ? $", {c}" : string.Empty)}: {Message}";
}

/// <summary>One data row of the file with what was found in it.</summary>
public sealed record RecordImportRowView(int Line, IReadOnlyList<string> Cells, IReadOnlyList<RecordImportIssue> Issues)
{
    public bool HasError => Issues.Any(i => i.Severity == RecordSeverity.Error);
}

/// <summary>A record the import would save.</summary>
/// <param name="Title">The record's title.</param>
/// <param name="Kind">The record kind.</param>
/// <param name="Revision">1 for a new record, or the revision it would be.</param>
/// <param name="ExistingId">The record it would revise, if any.</param>
/// <param name="ValueSymbols">The symbols of its values (never the values themselves).</param>
/// <param name="Rows">The rows of its table value, if it has one.</param>
/// <param name="Licensed">Whether it would be a licensed record.</param>
public sealed record RecordImportPlanned(string Title, string Kind, int Revision, Guid? ExistingId, IReadOnlyList<string> ValueSymbols, int Rows, bool Licensed);

/// <summary>
/// What an import would do, without doing it: the columns as mapped with their unit checks, every data row with its own errors and warnings, and
/// the records that would be saved. <see cref="CanCommit"/> is true only when nothing is an error: an import is all or nothing.
/// </summary>
public sealed record RecordImportPreview(
    RecordTemplate? Template,
    string? FileName,
    string? Sha256,
    IReadOnlyList<RecordImportColumn> Columns,
    IReadOnlyList<RecordImportRowView> Rows,
    IReadOnlyList<RecordImportIssue> Issues,
    IReadOnlyList<RecordImportPlanned> Planned,
    string? Refusal,
    char Delimiter = ',')
{
    /// <summary>Every error and warning: the file-level ones, then each row's.</summary>
    public IReadOnlyList<RecordImportIssue> AllIssues => [.. Issues, .. Rows.SelectMany(r => r.Issues)];

    public IReadOnlyList<RecordImportIssue> Errors => [.. AllIssues.Where(i => i.Severity == RecordSeverity.Error)];

    public IReadOnlyList<RecordImportIssue> Warnings => [.. AllIssues.Where(i => i.Severity == RecordSeverity.Warning)];

    /// <summary>Whether Commit would save the records: the file was read, nothing is an error, and there is something to save.</summary>
    public bool CanCommit => Refusal is null && Planned.Count > 0 && AllIssues.All(i => i.Severity != RecordSeverity.Error);

    /// <summary>Why nothing can be saved, in one sentence: the refusal, or the first error.</summary>
    public string? Blocker => Refusal ?? Errors.FirstOrDefault()?.ToString() ?? (Planned.Count == 0 ? "The file holds no data rows." : null);

    internal static RecordImportPreview Refused(string reason, RecordTemplate? template = null, string? fileName = null, string? sha = null) =>
        new(template, fileName, sha, [], [], [], [], reason);
}

/// <summary>The result of a commit: the records saved (empty when nothing was), or why nothing was, with the preview it was judged by.</summary>
public sealed record RecordImportOutcome(bool Committed, IReadOnlyList<CalcDataRecord> Records, string? Refusal, RecordImportPreview Preview);

/// <summary>How a record kind stands for one calculator.</summary>
public enum RecordNeed
{
    /// <summary>The calculator is blocked without a record of this kind.</summary>
    Required,

    /// <summary>The calculator needs it for some configurations (a fuse, a pulling check): the reason says which.</summary>
    Conditional,

    /// <summary>The calculator runs on its defaults, and warns until a record is applied.</summary>
    Recommended,

    /// <summary>The calculator takes it only as an alternative or an extra.</summary>
    Optional,
}

/// <summary>One record kind of a calculator, whether any record of it exists, and the templates that fill it.</summary>
/// <param name="Kind">The record kind.</param>
/// <param name="Title">What the calculator calls it.</param>
/// <param name="Symbols">The inputs it fills.</param>
/// <param name="Need">How much the calculator needs it.</param>
/// <param name="Reason">Why, in a few words.</param>
/// <param name="Present">Whether at least one record of this kind holds a value the calculator takes.</param>
/// <param name="Count">How many records of this kind exist.</param>
/// <param name="Templates">The Track P templates that fill this kind (empty when there is none).</param>
public sealed record RecordRequirement(string Kind, string Title, IReadOnlyList<string> Symbols, RecordNeed Need, string Reason, bool Present, int Count, IReadOnlyList<RecordTemplate> Templates)
{
    public bool IsMissing => !Present;
}

/// <summary>A record kind as the records screen lists it.</summary>
/// <param name="Kind">The record kind.</param>
/// <param name="Title">Its title.</param>
/// <param name="Calculators">The calculators that draw on it (their titles).</param>
/// <param name="Templates">The Track P templates that fill it.</param>
/// <param name="Count">How many records of this kind exist.</param>
/// <param name="Licensed">How many of them are licensed.</param>
/// <param name="RequiredBy">The calculators that are blocked without it.</param>
public sealed record RecordTypeInfo(string Kind, string Title, IReadOnlyList<string> Calculators, IReadOnlyList<RecordTemplate> Templates, int Count, int Licensed, IReadOnlyList<string> RequiredBy);
