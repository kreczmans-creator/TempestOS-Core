namespace Tempest.Desktop.History;

/// <summary>One recorded entry in a <see cref="CommandHistoryLog"/>.</summary>
public sealed record CommandHistoryEntry(DateTimeOffset Timestamp, string Description, bool Succeeded);
