using Tempest.Desktop.Editors.Sections;

namespace Tempest.Desktop.Editors;

/// <summary>
/// The single factory <see cref="ObjectEditorView"/> takes its ordered list
/// of sections from (`WP 21.1B`, brief scope item 3: "Registration, not a
/// switch") — a future Kind's own new section adds one file under
/// <c>Editors/Sections/</c> and one line here; two Work Packages touching
/// two different Kinds' own sections no longer touch this file (or
/// <see cref="ObjectEditorView"/> itself) at the same place, since each
/// section's own code lives entirely in its own file.
/// </summary>
/// <remarks>
/// <para>
/// <b>Order here is the order every section's own <see cref="Avalonia.Controls.Expander"/>
/// renders in, top to bottom</b> — this list is exactly the pre-split
/// shell's own historical <c>body.Children.Add(...)</c> sequence, minus
/// Identity and Content (which stay directly on the shell; see
/// <see cref="ObjectEditorView"/>'s own class remarks on that pair's
/// hidden coupling through the header's Save/Cancel/read-only state).
/// <see cref="ObjectEditorView.BuildLayout"/> splices <c>Content</c> back
/// in after this list's own fourth entry (Description, Commercial,
/// Invoice Lines, Quotation Lines) — the exact point <c>Content</c> sat at
/// in the pre-split shell, proven unchanged by
/// <c>tests/Tempest.Desktop.Tests/Editors/Golden/*.txt</c>. Reordering
/// this list reorders the real, running editor and will fail every one of
/// those nine golden-tree comparisons — that is by design, not a false
/// positive.
/// </para>
/// <para>
/// <b><paramref name="context"/> is accepted, not yet read</b> — every
/// section here takes a parameterless constructor and reads
/// <see cref="EditorSectionContext"/> only once, later, through its own
/// <see cref="IEditorSection.Build"/>. It is threaded into this factory's
/// own signature regardless, matching the brief's own exact contract
/// (<c>EditorSections.All(context)</c>), so a future section whose mere
/// inclusion depends on <paramref name="context"/> (a feature flag, say)
/// never has to widen this method's signature to get it.
/// </para>
/// </remarks>
internal static class EditorSections
{
    public static IReadOnlyList<IEditorSection> All(EditorSectionContext context) =>
    [
        new DescriptionSection(),
        new CommercialSection(),
        new InvoiceLinesSection(),
        new QuotationLinesSection(),

        // `ObjectEditorView.BuildLayout` splices the shell's own Content
        // section in here — see this class's own remarks.

        new EvidenceSubjectSection(),
        new BillOfMaterialsSection(),
        new RequirementOwnerPrioritySection(),
        new CalculationExecuteSection(),
        new CalculationPointerSection(),
        new CalculationDueSection(),
        new VerificationResultSection(),
        new EvidenceCitationsSection(),
        new EvidenceDeclaredFiguresSection(),
        new AttachmentsSection(),
        new WhereUsedSection(),
        new LifecycleSection(),
        new InvoiceConnectorSection(),
        new EvidenceLifecycleSection(),
        new RelationshipsSection(),
        new ValidationSection(),
        new EvidenceAuditSection(),
    ];

    /// <summary>How many of <see cref="All"/>'s own entries precede the shell's own Content section — <see cref="ObjectEditorView.BuildLayout"/>'s own splice point.</summary>
    public const int SectionsBeforeContent = 4;
}
