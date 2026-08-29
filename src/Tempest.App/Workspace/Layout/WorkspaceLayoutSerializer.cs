using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tempest.App.Workspace.Layout;

/// <summary>
/// Reads and writes a <see cref="WorkspaceLayoutTree"/> as JSON
/// (`TD-72`).
/// </summary>
/// <remarks>
/// <para>
/// A hand-written DTO shape rather than polymorphic serialisation of the
/// model types. The model is a discriminated tree of records; letting
/// System.Text.Json infer that would couple the persisted format to CLR
/// type names, so renaming a node type would silently orphan every saved
/// layout. The DTO names the discriminator explicitly and carries a
/// version, which is what makes a future format change a migration rather
/// than a data loss.
/// </para>
/// <para>
/// Reading is <b>total</b>: any malformed, truncated or foreign value
/// returns <see langword="null"/> rather than throwing, and the caller
/// falls back to a default arrangement. A corrupt layout must cost the
/// user their panel positions, never their session (`TD-60`'s established
/// discipline for passive reads).
/// </para>
/// </remarks>
public static class WorkspaceLayoutSerializer
{
    /// <summary>The format version written into every document.</summary>
    public const int CurrentVersion = 1;

    private const string SplitDiscriminator = "split";
    private const string TabsDiscriminator = "tabs";

    /// <summary>Serialises <paramref name="tree"/>.</summary>
    public static string Serialise(WorkspaceLayoutTree tree)
    {
        ArgumentNullException.ThrowIfNull(tree);

        var dto = new LayoutDocumentDto(
            CurrentVersion,
            tree.Root is null ? null : ToDto(tree.Root),
            tree.Floating.Select(f => new FloatingDto(f.Id, ToDto(f.Content), f.X, f.Y, f.Width, f.Height)).ToList(),
            tree.Panels.Select(p => new PanelStateDto(p.Key, p.Value.IsPinned, p.Value.IsCollapsed)).ToList());

        return JsonSerializer.Serialize(dto);
    }

    /// <summary>
    /// Deserialises a layout, or <see langword="null"/> when
    /// <paramref name="json"/> is absent, malformed, of an unknown version,
    /// or structurally impossible.
    /// </summary>
    public static WorkspaceLayoutTree? Deserialise(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            var dto = JsonSerializer.Deserialize<LayoutDocumentDto>(json);
            if (dto is null || dto.Version != CurrentVersion)
                return null;

            // A document that declares no root is a legitimately empty
            // arrangement — every panel closed. A document that declares a
            // root which cannot be reconstructed is corrupt, and must
            // degrade to "no saved layout" so the caller falls back to a
            // default rather than opening an empty workspace the user never
            // asked for. The two are indistinguishable once the root is
            // null, so they are separated here.
            var root = dto.Root is null ? null : FromDto(dto.Root);
            if (dto.Root is not null && root is null)
                return null;

            var floating = (dto.Floating ?? [])
                .Select(f => FromDto(f.Content) is { } content
                    ? new FloatingLayoutWindow(f.Id, content, f.X, f.Y, f.Width, f.Height)
                    : null)
                .OfType<FloatingLayoutWindow>()
                .ToList();

            var panels = (dto.Panels ?? [])
                .GroupBy(p => p.PanelId)
                .ToDictionary(g => g.Key, g => new PanelPresentation(g.Last().IsPinned, g.Last().IsCollapsed));

            return new WorkspaceLayoutTree(root, floating, panels).Normalised();
        }
        catch (JsonException)
        {
            return null;
        }
        catch (ArgumentException)
        {
            // A structurally impossible document — an empty tab group, a
            // split with no children, mismatched weights. The model's own
            // constructors reject it; that is a corrupt layout, not a crash.
            return null;
        }
    }

    private static NodeDto ToDto(WorkspaceLayoutNode node) => node switch
    {
        LayoutSplitNode split => new NodeDto(
            SplitDiscriminator, split.Id, split.Orientation, split.Children.Select(ToDto).ToList(), [.. split.Weights], null, 0),

        LayoutTabGroupNode tabs => new NodeDto(
            TabsDiscriminator, tabs.Id, LayoutOrientation.Horizontal, null, null, [.. tabs.PanelIds], tabs.SelectedIndex),

        _ => throw new ArgumentOutOfRangeException(nameof(node), node, "Unknown layout node type."),
    };

    private static WorkspaceLayoutNode? FromDto(NodeDto? dto)
    {
        if (dto is null)
            return null;

        switch (dto.Kind)
        {
            case TabsDiscriminator:
            {
                var panelIds = (dto.PanelIds ?? []).Distinct().ToList();
                return panelIds.Count == 0 ? null : new LayoutTabGroupNode(dto.Id, panelIds, dto.SelectedIndex);
            }

            case SplitDiscriminator:
            {
                var children = new List<WorkspaceLayoutNode>();
                var weights = new List<double>();
                var source = dto.Children ?? [];

                for (var i = 0; i < source.Count; i++)
                {
                    if (FromDto(source[i]) is not { } child)
                        continue;

                    children.Add(child);
                    weights.Add(dto.Weights is { } w && i < w.Count ? w[i] : 1.0);
                }

                return children.Count switch
                {
                    0 => null,
                    1 => children[0],
                    _ => new LayoutSplitNode(dto.Id, dto.Orientation, children, weights),
                };
            }

            default:
                return null;
        }
    }

    private sealed record LayoutDocumentDto(
        int Version,
        NodeDto? Root,
        IReadOnlyList<FloatingDto>? Floating,
        IReadOnlyList<PanelStateDto>? Panels);

    private sealed record FloatingDto(Guid Id, NodeDto Content, double X, double Y, double Width, double Height);

    private sealed record PanelStateDto(Guid PanelId, bool IsPinned, bool IsCollapsed);

    private sealed record NodeDto(
        string Kind,
        Guid Id,
        [property: JsonConverter(typeof(JsonStringEnumConverter))] LayoutOrientation Orientation,
        IReadOnlyList<NodeDto>? Children,
        IReadOnlyList<double>? Weights,
        IReadOnlyList<Guid>? PanelIds,
        int SelectedIndex);
}
