using Tempest.Core.Commands;

namespace Tempest.Workspace;

/// <summary>
/// Where a newly created object goes when the user did not say (`WP 17.9.2`
/// for Mechanical, generalised to every discipline in `WP 17.9.3`).
/// </summary>
/// <remarks>
/// <para>
/// The rule is the one a person would assume: a new object goes under the
/// thing they have selected if that thing can hold it, otherwise under the
/// open project, otherwise nowhere, which is what standalone Engineering
/// means. Which Kinds "can hold it" is the discipline's own list, passed
/// in; this class chooses, and never writes.
/// </para>
/// <para>
/// The design-freeze surface audit of 2026-09-08 found that Documents,
/// Calculations and Manufacturing objects created from the Ribbon or the
/// Palette never joined a project, because their bindings discarded the
/// context entirely (`TD-172`).
/// </para>
/// </remarks>
public static class CreationPlacement
{
    /// <summary>
    /// The parent for a new object given <paramref name="context"/>: the
    /// selected object when its Kind is one of
    /// <paramref name="containerKinds"/>, else the open project, else
    /// <see langword="null"/>.
    /// </summary>
    public static Guid? ParentFor(CommandContext context, IReadOnlyList<string> containerKinds)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(containerKinds);

        var primary = context.Primary;
        if (primary is not null && containerKinds.Contains(primary.Kind, StringComparer.Ordinal))
            return primary.ObjectId;

        return context.ProjectId;
    }
}
