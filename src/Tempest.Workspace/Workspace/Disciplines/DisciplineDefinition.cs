namespace Tempest.Workspace.Disciplines;

/// <summary>
/// One Kind an attribute-carrying engineering discipline offers
/// (`ADR-0163`): what it is called, the Project Explorer folder it lists
/// under, and the attributes an engineer records against it.
/// </summary>
/// <param name="Kind">The <see cref="Tempest.Core.EngineeringDomain.IEngineeringObject.Kind"/> — unique across the platform.</param>
/// <param name="Label">The singular name shown on screen ("Member").</param>
/// <param name="Folder">The Project Explorer folder this Kind lists under ("Members").</param>
/// <param name="FolderNodeId">The folder's fixed Explorer node id.</param>
/// <param name="Attributes">The attributes this Kind carries, in the order the Properties panel lists them.</param>
public sealed record DisciplineKind(string Kind, string Label, string Folder, Guid FolderNodeId, IReadOnlyList<string> Attributes);

/// <summary>
/// An engineering discipline built on the shared
/// <see cref="Tempest.Core.EngineeringDomain.DisciplineObject"/> shape
/// (`ADR-0163`): its command category, its Explorer area and the Kinds it
/// offers. <see cref="DisciplineCatalogue"/> holds every one.
/// </summary>
/// <param name="Category">The command category, and the discipline's own name ("Structural").</param>
/// <param name="CommandPrefix">The prefix of every command id this discipline registers ("structural").</param>
/// <param name="NavigationItemId">The Explorer area id.</param>
/// <param name="Kinds">The Kinds this discipline offers, in folder order.</param>
public sealed record DisciplineDefinition(string Category, string CommandPrefix, string NavigationItemId, IReadOnlyList<DisciplineKind> Kinds)
{
    /// <summary>Every Kind string this discipline offers.</summary>
    public IReadOnlyList<string> KindNames { get; } = [.. Kinds.Select(k => k.Kind)];

    /// <summary>The definition of <paramref name="kind"/>, or <see langword="null"/> when this discipline does not offer it.</summary>
    public DisciplineKind? Find(string kind) => Kinds.FirstOrDefault(k => string.Equals(k.Kind, kind, StringComparison.Ordinal));

    /// <summary>The command id for <paramref name="verb"/> ("structural.create").</summary>
    public string CommandId(string verb) => $"{CommandPrefix}.{verb}";
}
