using Tempest.Workspace.Mechanical;

namespace Tempest.Workspace.Editors;

/// <summary>
/// The real, declared editor shape for the four Kinds `WP 18.2A` applies
/// the declaration-per-Kind rule to: Evidence (`ADR-0148`), Part, Assembly
/// and Component. Every other Kind (Requirement, Document, SubAssembly,
/// Configuration, a Calculation, …) has no declaration registered here, so
/// the Object Editor renders it exactly as it always has (`WP 10.3A`
/// onward) — the refactor's own explicit, narrow scope
/// (`docs/releases/v0.18.0/Execution Plan.md` §6 risk table).
/// </summary>
public static class KindEditorDeclarations
{
    /// <summary>Registers every declaration this class declares against <paramref name="registry"/>.</summary>
    public static void RegisterAll(IKindEditorDeclarationRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);

        registry.Register(Evidence());
        registry.Register(Part());
        registry.Register(Assembly());
        registry.Register(Component());
        registry.Register(Project());
    }

    /// <summary>
    /// Evidence's own declaration (`ADR-0148`): Identity; Files; Subject;
    /// Citations; Declared figures; Lifecycle (status, and the check and
    /// issue records read-only — the Check and Issue actions themselves
    /// are `WP 18.2B`); Audit.
    /// </summary>
    public static KindEditorDeclaration Evidence() => new(
        Core.Evidence.Evidence.CanonicalKind,
        [
            new(EditorSectionKeys.Identity, "Identity",
                new EditorFieldDeclaration("Name", EditorControlKind.Text, Editable: true)),

            new(EditorSectionKeys.Files, "Files",
                new EditorFieldDeclaration("Attachments", EditorControlKind.Attachments, Editable: true)),

            new(EditorSectionKeys.Subject, "Subject",
                new EditorFieldDeclaration("Subject", EditorControlKind.ObjectReference, Editable: false)),

            new(EditorSectionKeys.Citations, "Citations",
                new EditorFieldDeclaration("Citations", EditorControlKind.ReadOnlyList, Editable: true, WriteCommandId: "evidence.cite")),

            new(EditorSectionKeys.DeclaredFigures, "Declared figures",
                new EditorFieldDeclaration("Declared Figures", EditorControlKind.ReadOnlyList, Editable: true, WriteCommandId: "evidence.declare-figure")),

            new(EditorSectionKeys.CheckAndIssue, "Lifecycle",
                new EditorFieldDeclaration("Status", EditorControlKind.Choice, Editable: false),
                new EditorFieldDeclaration("Check", EditorControlKind.Text, Editable: false),
                new EditorFieldDeclaration("Issue", EditorControlKind.Text, Editable: false)),

            new(EditorSectionKeys.Audit, "Audit",
                new EditorFieldDeclaration("Audit Trail", EditorControlKind.ReadOnlyList, Editable: false)),
        ]);

    /// <summary>
    /// Part's own declaration (`TD-174`, `TD-175`): Identity, Description,
    /// the mechanical fields the model has today, a read-only <em>Where
    /// used</em>, Attachments, Lifecycle. Deliberately no Bill-of-Materials
    /// section — a Part shows no BOM <em>input</em> (`D-028`: material is
    /// cited on evidence, not assigned to the part).
    /// </summary>
    public static KindEditorDeclaration Part() => new(
        MechanicalObjectFactoryRegistry.Part,
        [
            IdentitySection(),
            DescriptionSection(),
            WhereUsedSection(),
            AttachmentsSection(),
            LifecycleSection(),
        ]);

    /// <summary>Assembly's own declaration: as <see cref="Part"/>, plus its own Bill-of-Materials section (how it is used within its own parent's assembly).</summary>
    public static KindEditorDeclaration Assembly() => new(
        MechanicalObjectFactoryRegistry.Assembly,
        [
            IdentitySection(),
            DescriptionSection(),
            WhereUsedSection(),
            new(EditorSectionKeys.BillOfMaterials, "Bill of Materials",
                new EditorFieldDeclaration("Quantity", EditorControlKind.Text, Editable: true),
                new EditorFieldDeclaration("Unit of Measure", EditorControlKind.Text, Editable: true),
                new EditorFieldDeclaration("Find Number", EditorControlKind.Text, Editable: true),
                new EditorFieldDeclaration("Item Number", EditorControlKind.Text, Editable: true),
                new EditorFieldDeclaration("Reference Designator", EditorControlKind.Text, Editable: true)),
            AttachmentsSection(),
            LifecycleSection(),
        ]);

    /// <summary>Component's own declaration: as <see cref="Part"/> — identity, description, <em>Where used</em>, attachments, lifecycle; no Bill-of-Materials section.</summary>
    public static KindEditorDeclaration Component() => new(
        MechanicalObjectFactoryRegistry.Component,
        [
            IdentitySection(),
            DescriptionSection(),
            WhereUsedSection(),
            AttachmentsSection(),
            LifecycleSection(),
        ]);

    /// <summary>
    /// The project's own declaration (`WP 19.0A`, `ADR-0150`): Identity;
    /// Commercial (client and rate card and project manager read-only —
    /// each needs a picker, Desktop work of part 2 of this Work Package;
    /// purchase order reference, dates and budget editable now, as plain
    /// text); Lifecycle. No Description, Where-used or Bill-of-Materials
    /// section — those facets are Mechanical's own structural product,
    /// which a Project does not carry.
    /// </summary>
    public static KindEditorDeclaration Project() => new(
        MechanicalObjectFactoryRegistry.Project,
        [
            IdentitySection(),

            new(EditorSectionKeys.Commercial, "Commercial",
                new EditorFieldDeclaration("Client", EditorControlKind.ObjectReference, Editable: false),
                new EditorFieldDeclaration("Purchase Order Reference", EditorControlKind.Text, Editable: true, WriteCommandId: "project.set-purchase-order"),
                new EditorFieldDeclaration("Budget", EditorControlKind.Text, Editable: true, WriteCommandId: "project.set-budget"),
                new EditorFieldDeclaration("Rate Card", EditorControlKind.ObjectReference, Editable: false),
                new EditorFieldDeclaration("Start Date", EditorControlKind.Text, Editable: true, WriteCommandId: "project.set-dates"),
                new EditorFieldDeclaration("Target Date", EditorControlKind.Text, Editable: true, WriteCommandId: "project.set-dates"),
                new EditorFieldDeclaration("Project Manager", EditorControlKind.ObjectReference, Editable: false)),

            LifecycleSection(),
        ]);

    private static EditorSectionDeclaration IdentitySection() => new(
        EditorSectionKeys.Identity, "Identity",
        new EditorFieldDeclaration("Name", EditorControlKind.Text, Editable: true));

    private static EditorSectionDeclaration DescriptionSection() => new(
        EditorSectionKeys.Description, "Description",
        new EditorFieldDeclaration("Owner", EditorControlKind.Text, Editable: false),
        new EditorFieldDeclaration("Discipline", EditorControlKind.Text, Editable: false),
        new EditorFieldDeclaration("Classification", EditorControlKind.Text, Editable: false),
        new EditorFieldDeclaration("Tags", EditorControlKind.Text, Editable: false),
        new EditorFieldDeclaration("Notes", EditorControlKind.MultilineText, Editable: false));

    private static EditorSectionDeclaration WhereUsedSection() => new(
        EditorSectionKeys.WhereUsed, "Where used",
        new EditorFieldDeclaration("Assembly", EditorControlKind.ObjectReference, Editable: false));

    private static EditorSectionDeclaration AttachmentsSection() => new(
        EditorSectionKeys.Attachments, "Attachments",
        new EditorFieldDeclaration("Attachments", EditorControlKind.Attachments, Editable: true));

    private static EditorSectionDeclaration LifecycleSection() => new(
        EditorSectionKeys.Lifecycle, "Lifecycle",
        new EditorFieldDeclaration("Status", EditorControlKind.Lifecycle, Editable: false));
}
