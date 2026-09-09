namespace Tempest.Core.ReferenceData;

/// <summary>
/// Where a reference record's own values were actually read from, structured
/// down to the line on the page — publisher, work, edition, page, table or
/// figure, row or entry.
/// </summary>
/// <remarks>
/// <para>
/// <b>Optional, and never invented.</b> <see cref="Review.ReferenceReviewService"/>
/// requires <see cref="ReferenceProvenance"/> to name a source organisation and
/// document before a record may leave Draft (`TEMPEST-REF-001`); this type is a
/// finer-grained citation of the same act, structured enough to print as one
/// line on an issue sheet, but it is never required and never fabricated to
/// fill a gap the source itself did not close. A record with no source this
/// precise carries <see langword="null"/> rather than a guess (`ADR-0149`).
/// </para>
/// <para>
/// <b>Distinct from <see cref="ReferenceProvenance"/>.</b> Provenance answers
/// "where did this record's own values come from, and has anyone checked them
/// back against it" — a governance question every record answers. A citation
/// answers "point to the exact line" — a bibliographic question only some
/// records can answer as precisely as this, and evidence that cites the
/// record (`WP 18.0A`) wants the sharper of the two.
/// </para>
/// </remarks>
/// <param name="Publisher">Who published the work — a standards body, a manufacturer, a handbook's own publisher. Required.</param>
/// <param name="Work">The work itself, by its own title or designation. Required.</param>
/// <param name="Edition">The edition or revision cited, where the source names one. <see langword="null"/> if not applicable or not stated.</param>
/// <param name="Page">The page the value was read from, where the source is paginated. <see langword="null"/> if not applicable or not stated.</param>
/// <param name="TableOrFigure">The table or figure the value was read from, where the source names one. <see langword="null"/> if not applicable or not stated.</param>
/// <param name="RowOrEntry">The row or entry within that table, where the source names one. <see langword="null"/> if not applicable or not stated.</param>
public sealed record SourceCitation(
    string Publisher,
    string Work,
    string? Edition = null,
    string? Page = null,
    string? TableOrFigure = null,
    string? RowOrEntry = null)
{
    /// <summary>The work's own publisher. Required — a citation that names no publisher points at nothing.</summary>
    public string Publisher { get; } = string.IsNullOrWhiteSpace(Publisher)
        ? throw new ArgumentException("A source citation must name the work's own publisher.", nameof(Publisher))
        : Publisher.Trim();

    /// <summary>The work itself. Required — a citation that names no work points at nothing.</summary>
    public string Work { get; } = string.IsNullOrWhiteSpace(Work)
        ? throw new ArgumentException("A source citation must name the work itself.", nameof(Work))
        : Work.Trim();

    /// <summary>
    /// A short line suitable for an issue sheet:
    /// <c>Publisher, Work, ed. Edition, p. Page, TableOrFigure, row RowOrEntry</c>
    /// — every absent part omitted, never printed as a blank field.
    /// </summary>
    public override string ToString()
    {
        var parts = new List<string>(6) { Publisher, Work };

        if (!string.IsNullOrWhiteSpace(Edition))
            parts.Add($"ed. {Edition}");

        if (!string.IsNullOrWhiteSpace(Page))
            parts.Add($"p. {Page}");

        if (!string.IsNullOrWhiteSpace(TableOrFigure))
            parts.Add(TableOrFigure);

        if (!string.IsNullOrWhiteSpace(RowOrEntry))
            parts.Add($"row {RowOrEntry}");

        return string.Join(", ", parts);
    }
}
