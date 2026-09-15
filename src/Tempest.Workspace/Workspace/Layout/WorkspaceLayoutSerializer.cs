using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tempest.Workspace.Layout;

/// <summary>
/// Reads and writes a <see cref="WorkspaceLayoutTree"/> as JSON
/// (`TD-72`, versioned again by `ADR-0153` for the window forest).
/// </summary>
/// <remarks>
/// <para>
/// A hand-written DTO shape rather than polymorphic serialisation of the
/// model types. The model is a discriminated tree of records; letting
/// System.Text.Json infer that would couple the persisted format to CLR
/// type names, so renaming a node type would silently orphan every saved
/// layout. The DTO names the discriminator explicitly and carries a
/// version, which is what makes a format change a migration rather than a
/// data loss — proven twice now: `ADR-0095` §7 chose this discipline
/// for the very first version, and this is its first real use, reading a
/// version-1 document (one root, a separate floating list) into the
/// version-2 window forest without orphaning anyone's saved arrangement.
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
    /// <summary>The format version written into every document — the window forest (`ADR-0153`).</summary>
    public const int CurrentVersion = 2;

    /// <summary>The pre-`ADR-0153` format: one root, a separate floating list. Still read, never written.</summary>
    private const int LegacyVersion = 1;

    private const string SplitDiscriminator = "split";
    private const string TabsDiscriminator = "tabs";

    /// <summary>Serialises <paramref name="tree"/>, in the current (version 2) format.</summary>
    public static string Serialise(WorkspaceLayoutTree tree)
    {
        ArgumentNullException.ThrowIfNull(tree);

        var dto = new LayoutDocumentDto(
            CurrentVersion,
            tree.Windows.Select(w => new WindowDto(
                w.Id, w.Root is null ? null : ToDto(w.Root), w.IsPrimary, w.MonitorKey, w.X, w.Y, w.Width, w.Height)).ToList(),
            tree.Panels.Select(p => new PanelStateDto(p.Key, p.Value.IsPinned, p.Value.IsCollapsed)).ToList());

        return JsonSerializer.Serialize(dto);
    }

    /// <summary>
    /// Deserialises a layout — version 1 (one root, a separate floating
    /// list) into a forest with one primary window, or version 2 (the
    /// window forest) directly — or <see langword="null"/> when
    /// <paramref name="json"/> is absent, malformed, of an unknown version,
    /// or structurally impossible.
    /// </summary>
    public static WorkspaceLayoutTree? Deserialise(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return null;

            if (!document.RootElement.TryGetProperty(nameof(LayoutDocumentDto.Version), out var versionElement)
                || versionElement.ValueKind != JsonValueKind.Number
                || !versionElement.TryGetInt32(out var version))
                return null;

            return version switch
            {
                LegacyVersion => DeserialiseLegacy(json),
                CurrentVersion => DeserialiseCurrent(json),
                _ => null,
            };
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

    private static WorkspaceLayoutTree? DeserialiseLegacy(string json)
    {
        var dto = JsonSerializer.Deserialize<LegacyLayoutDocumentDto>(json);
        if (dto is null)
            return null;

        // A document that declares no root is a legitimately empty
        // arrangement — every panel closed. A document that declares a
        // root which cannot be reconstructed is corrupt, and must degrade
        // to "no saved layout" so the caller falls back to a default
        // rather than opening an empty workspace the user never asked for.
        // The two are indistinguishable once the root is null, so they are
        // separated here.
        var root = dto.Root is null ? null : FromDto(dto.Root);
        if (dto.Root is not null && root is null)
            return null;

        var floating = (dto.Floating ?? [])
            .Select(f => FromDto(f.Content) is { } content
                ? new WorkspaceLayoutWindow(f.Id, content, IsPrimary: false, null, f.X, f.Y, f.Width, f.Height)
                : null)
            .OfType<WorkspaceLayoutWindow>()
            .ToList();

        var windows = new List<WorkspaceLayoutWindow> { new(Guid.NewGuid(), root, IsPrimary: true, null, 0, 0, 0, 0) };
        windows.AddRange(floating);

        return new WorkspaceLayoutTree(windows, ReadPanels(dto.Panels)).Normalised();
    }

    private static WorkspaceLayoutTree? DeserialiseCurrent(string json)
    {
        var dto = JsonSerializer.Deserialize<LayoutDocumentDto>(json);
        if (dto?.Windows is not { Count: > 0 } windowDtos)
            return null;

        var windows = new List<WorkspaceLayoutWindow>(windowDtos.Count);
        var sawPrimary = false;

        foreach (var windowDto in windowDtos)
        {
            var root = windowDto.Root is null ? null : FromDto(windowDto.Root);
            if (windowDto.Root is not null && root is null)
                return null;

            // A non-primary window naming no panels at all is corrupt —
            // the model's own Normalised() would drop it anyway, but
            // catching it here keeps a hand-edited or truncated document
            // from silently losing a window rather than reading as
            // unreadable.
            var isPrimary = windowDto.IsPrimary && !sawPrimary;
            sawPrimary |= windowDto.IsPrimary;

            windows.Add(new WorkspaceLayoutWindow(windowDto.Id, root, isPrimary, windowDto.MonitorKey, windowDto.X, windowDto.Y, windowDto.Width, windowDto.Height));
        }

        // No window named primary at all is not a legitimate arrangement —
        // there would be nowhere for the rail and header to live.
        if (!sawPrimary)
            return null;

        return new WorkspaceLayoutTree(windows, ReadPanels(dto.Panels)).Normalised();
    }

    private static IReadOnlyDictionary<Guid, PanelPresentation> ReadPanels(IReadOnlyList<PanelStateDto>? panels) =>
        (panels ?? [])
            .GroupBy(p => p.PanelId)
            .ToDictionary(g => g.Key, g => new PanelPresentation(g.Last().IsPinned, g.Last().IsCollapsed));

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

    // ------------------------------------------------------------
    // Version 2 (`ADR-0153`): the window forest
    // ------------------------------------------------------------

    private sealed record LayoutDocumentDto(
        int Version,
        IReadOnlyList<WindowDto>? Windows,
        IReadOnlyList<PanelStateDto>? Panels);

    private sealed record WindowDto(
        Guid Id,
        NodeDto? Root,
        bool IsPrimary,
        string? MonitorKey,
        double X,
        double Y,
        double Width,
        double Height);

    // ------------------------------------------------------------
    // Version 1 (`TD-72`): one root, a separate floating list
    // ------------------------------------------------------------

    private sealed record LegacyLayoutDocumentDto(
        int Version,
        NodeDto? Root,
        IReadOnlyList<FloatingDto>? Floating,
        IReadOnlyList<PanelStateDto>? Panels);

    private sealed record FloatingDto(Guid Id, NodeDto Content, double X, double Y, double Width, double Height);

    // ------------------------------------------------------------
    // Shared shapes
    // ------------------------------------------------------------

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
