namespace Tempest.Core.EngineeringIntelligence;

/// <summary>
/// The subject kinds `P02` recognises — one per `P01` reference library
/// that can be reasoned about.
/// </summary>
/// <remarks>
/// <para>
/// Declared once, here, because a rule's applicability names a kind as
/// text and a typo would produce a rule that silently matches nothing.
/// Validation warns against an unrecognised kind rather than refusing it:
/// a future library will add one, and a rule written ahead of it is
/// premature rather than wrong.
/// </para>
/// <para>
/// These are `P02`'s own vocabulary, not the reference libraries' document
/// Kinds. A `P01` document Kind names how a record is stored
/// (<c>"MaterialSpecification"</c>); a subject kind names what a rule is
/// about (<c>"Material"</c>), and one library could in principle present
/// more than one.
/// </para>
/// </remarks>
public static class AssessmentSubjectKinds
{
    /// <summary>A material, from `A1`.</summary>
    public const string Material = "Material";

    /// <summary>A fastener, from `A3`.</summary>
    public const string Fastener = "Fastener";

    /// <summary>A rolling bearing, from `A4`.</summary>
    public const string Bearing = "Bearing";

    /// <summary>A spring, gear, drive element or standard machine component, from `A5`.</summary>
    public const string Component = "Component";

    /// <summary>A manufacturing process, from `A7`.</summary>
    public const string Process = "Process";

    /// <summary>Every kind, in the order a report should present them.</summary>
    public static IReadOnlyList<string> All { get; } = [Material, Fastener, Bearing, Component, Process];
}
