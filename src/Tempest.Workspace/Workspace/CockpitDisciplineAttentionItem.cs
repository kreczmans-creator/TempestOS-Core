namespace Tempest.Workspace;

/// <summary>
/// One entry in <see cref="EngineeringCockpit.AttentionItemsByDiscipline"/>:
/// a <see cref="CockpitAttentionItem"/> together with the
/// <see cref="CockpitDisciplines"/> name of the collaborator that
/// contributed it. <see cref="EngineeringCockpit.AttentionItems"/> is the
/// same list with the attribution dropped — one source, two projections,
/// so a consumer that needs to say <i>where</i> an item came from (the
/// Dashboard Export) and one that does not (the desktop Cockpit's own
/// tile) can never disagree about <i>what</i> the items are.
/// </summary>
/// <param name="Discipline">One of the <see cref="CockpitDisciplines"/> constants.</param>
/// <param name="Item">The attention item itself, exactly as the contributing collaborator produced it.</param>
public sealed record CockpitDisciplineAttentionItem(string Discipline, CockpitAttentionItem Item);
