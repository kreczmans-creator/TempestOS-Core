namespace Tempest.App.Workspace;

/// <summary>
/// One entry in <see cref="NavigationService"/>'s own back/forward history —
/// either an area switch (<see cref="AreaId"/> set, <see cref="ObjectId"/>
/// <see langword="null"/>) or an object open/jump
/// (<see cref="ObjectId"/>/<see cref="ObjectKind"/> set,
/// <see cref="AreaId"/> <see langword="null"/>). Not one of the twelve
/// `WP8.0B Workspace Contracts.md` interfaces — a genuine, disclosed
/// implementation-phase addition surfaced by `WP8.0C Navigation Maps.md`
/// §4, which no contract-review Work Package had yet anticipated.
/// </summary>
/// <param name="AreaId">The switched-to area's own Id, or <see langword="null"/> for an object entry.</param>
/// <param name="ObjectId">The opened/jumped-to object's own Id, or <see langword="null"/> for an area-switch entry.</param>
/// <param name="ObjectKind">The <c>Kind</c> of <see cref="ObjectId"/>, or <see langword="null"/> for an area-switch entry.</param>
/// <param name="Title">The entry's own display title — the area's title, or the opened view's own title.</param>
internal sealed record NavigationHistoryEntry(string? AreaId, Guid? ObjectId, string? ObjectKind, string Title);
