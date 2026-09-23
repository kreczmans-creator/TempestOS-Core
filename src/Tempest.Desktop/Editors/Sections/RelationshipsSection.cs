using Avalonia.Controls;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Events;
using Tempest.Core.Requirements;
using Tempest.Desktop.Theming;

namespace Tempest.Desktop.Editors.Sections;

/// <summary>
/// The Relationship summary (`WP 10.3A`) — a real, flat list, both
/// directions for a real object; outgoing only for a Requirement (`TD-41`,
/// `WP 19.10I` — <see cref="ObjectEditorView"/>'s own former
/// <c>PopulateRelationshipsAsync</c>/<c>PopulateRequirementRelationshipsAsync</c>,
/// moved verbatim and unified here since both fed the identical panel and
/// row-building helper (`WP 21.1B`).
/// </summary>
/// <remarks>
/// Always visible — every real object gets at least the incoming-relationship
/// read, and a Requirement always gets its own outgoing read — so
/// <see cref="AppliesTo"/> is unconditionally <see langword="true"/>, and
/// <see cref="LoadAsync"/> branches on <paramref name="subject"/> being a
/// real <see cref="IHasRelationships"/> versus <see langword="null"/> (the
/// Requirement path) internally, exactly as the two pre-split methods this
/// class replaces were each called from their own, separate orchestration
/// path. No incoming-relationship query runs for a Requirement — the
/// pre-split <c>PopulateRequirementRelationshipsAsync</c>'s own remarks
/// explain why: both "allocated to" and "verified by" are recorded as
/// outgoing references from the requirement itself, so
/// <see cref="IRequirementsService.GetRelationshipsAsync"/> alone already
/// covers both.
/// </remarks>
internal sealed class RelationshipsSection : IEditorSection
{
    private readonly StackPanel _panel = new() { Spacing = DesignTokens.SpaceXs };
    private EditorSectionContext _ctx = null!;

    public string Title => "Relationships";

    public bool AppliesTo(IEngineeringObject? subject) => true;

    public Control Build(EditorSectionContext ctx)
    {
        _ctx = ctx;
        return EditorSectionHelpers.BuildSection(Title, _panel);
    }

    public async Task LoadAsync(IEngineeringObject? subject, CancellationToken ct)
    {
        _panel.Children.Clear();

        if (subject is not null)
        {
            if (subject is IHasRelationships hasRelationships)
            {
                var outgoing = await hasRelationships.GetRelationshipsAsync().ConfigureAwait(true);
                foreach (var relationship in outgoing)
                    _panel.Children.Add(await EditorSectionHelpers.BuildRelationshipRowAsync(_ctx, relationship.TargetId, relationship.RelationshipKind, "→").ConfigureAwait(true));
            }

            var incoming = await _ctx.DomainContext.RelationshipRepository.GetIncomingAsync(_ctx.ObjectId).ConfigureAwait(true);
            foreach (var relationship in incoming)
                _panel.Children.Add(await EditorSectionHelpers.BuildRelationshipRowAsync(_ctx, relationship.SourceId, relationship.RelationshipKind, "←").ConfigureAwait(true));
        }
        else if (_ctx.RequirementsService is not null && _ctx.ObjectKind == RequirementsService.RequirementDocumentKind)
        {
            var outgoing = await _ctx.RequirementsService.GetRelationshipsAsync(_ctx.ObjectId).ConfigureAwait(true);
            foreach (var relationship in outgoing)
                _panel.Children.Add(await EditorSectionHelpers.BuildRelationshipRowAsync(_ctx, relationship.TargetDocumentId, relationship.RelationshipKind, "→").ConfigureAwait(true));
        }

        if (_panel.Children.Count == 0)
            _panel.Children.Add(new TextBlock { Text = "No relationships recorded.", Opacity = 0.7 });
    }

    public void React(WorkspaceChange change)
    {
        // Reserved — see IEditorSection's own remarks.
    }
}
