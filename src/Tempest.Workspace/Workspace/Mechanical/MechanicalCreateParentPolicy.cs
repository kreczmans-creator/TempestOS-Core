using Tempest.Core.Commands;

namespace Tempest.Workspace.Mechanical;

/// <summary>
/// Decides where a newly created Mechanical Product Structure object goes
/// when the user did not say (`WP 17.9.2`).
/// </summary>
/// <remarks>
/// <para>
/// The first Windows review of `v0.17.0` pressed "Create Mechanical
/// Object" with a project open, was told a Part had been created, and could
/// not find it: the Ribbon and Palette binding never supplied a parent, so
/// the Part was written correctly and hung from nothing, and the Project
/// Explorer — which roots on Projects and walks parent links — had no path
/// to it. The object was real; the tree could not show it.
/// </para>
/// <para>
/// <b>The rule is the one a person would assume.</b> A new object goes
/// under the thing you have selected if that thing can hold children
/// (a Project, an Assembly or a Sub-Assembly); otherwise under the open
/// project; otherwise nowhere, which is what standalone Engineering means.
/// A new Project is always a root. This class chooses; it never writes.
/// </para>
/// </remarks>
public static class MechanicalCreateParentPolicy
{
    /// <summary>The Kinds a new object may be placed under by default.</summary>
    public static readonly IReadOnlyList<string> ContainerKinds =
    [
        MechanicalObjectFactoryRegistry.Project,
        MechanicalObjectFactoryRegistry.Assembly,
        MechanicalObjectFactoryRegistry.SubAssembly,
    ];

    /// <summary>
    /// The parent for a new object of <paramref name="kind"/> given
    /// <paramref name="context"/>: the selected container, else the open
    /// project, else <see langword="null"/>. A Project always gets
    /// <see langword="null"/>.
    /// </summary>
    public static Guid? Resolve(string kind, CommandContext context)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        ArgumentNullException.ThrowIfNull(context);

        if (string.Equals(kind, MechanicalObjectFactoryRegistry.Project, StringComparison.Ordinal))
            return null;

        return CreationPlacement.ParentFor(context, ContainerKinds);
    }
}
