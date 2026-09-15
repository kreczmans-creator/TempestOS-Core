using Avalonia.Media.Imaging;

namespace Tempest.Desktop.Viewing;

/// <summary>
/// A bounded, least-recently-used cache of rendered page tiles (`TD-101`)
/// — the mechanism that lets panning a tiled page reuse a tile it has
/// already rasterised instead of asking the underlying page source for the
/// same region again.
/// </summary>
/// <remarks>
/// Owns every <see cref="Bitmap"/> it holds: an entry evicted (by the
/// budget) or replaced (<see cref="Add"/> called again for a key already
/// present) is disposed here, and so is every remaining entry when this
/// cache itself is disposed. A caller must not dispose a bitmap it got
/// from <see cref="TryGet"/> or handed to <see cref="Add"/> — this cache
/// owns it from that point on.
/// </remarks>
public sealed class TileCache : IDisposable
{
    /// <summary>
    /// This cache's own default memory budget (`TD-101`) — 256 MiB, stated
    /// explicitly per this Work Package's own brief. At <see cref="TileGrid.TileSize"/>
    /// (512×512, BGRA8888 — 1 MiB per tile) that holds 256 tiles at once:
    /// comfortably more than one screen's worth at any single zoom level
    /// (a 1920×1080 view needs at most 4×3 = 12 tiles visible plus a ring),
    /// enough headroom for several zoom levels' worth of tiles to stay
    /// cached across a zoom-out-and-back-in without every one of them
    /// having been evicted first.
    /// </summary>
    public const long DefaultBudgetBytes = 256L * 1024 * 1024;

    /// <summary>One tile's own cache key: which page, at what scale, at which grid position — cached per zoom level, exactly as this Work Package's own brief asks.</summary>
    public readonly record struct Key(int PageIndex, double Scale, int Column, int Row);

    private sealed record Entry(Key Key, Bitmap Bitmap, long SizeBytes);

    private readonly long _budgetBytes;
    private readonly Dictionary<Key, LinkedListNode<Entry>> _index = [];

    // Most-recently-used at the front, least-recently-used at the back —
    // the back is what Add evicts from first when the budget is exceeded.
    private readonly LinkedList<Entry> _lru = new();

    /// <summary>Initialises a new instance of the <see cref="TileCache"/> class.</summary>
    /// <param name="budgetBytes">The most total tile memory this cache holds at once before evicting. Defaults to <see cref="DefaultBudgetBytes"/>.</param>
    public TileCache(long budgetBytes = DefaultBudgetBytes)
    {
        if (budgetBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(budgetBytes), budgetBytes, "A cache needs a positive budget.");

        _budgetBytes = budgetBytes;
    }

    /// <summary>How many tiles this cache currently holds.</summary>
    public int Count => _index.Count;

    /// <summary>The total size, in bytes, of every tile this cache currently holds.</summary>
    public long UsedBytes { get; private set; }

    /// <summary>The budget this cache was constructed with.</summary>
    public long BudgetBytes => _budgetBytes;

    /// <summary>The tile cached under <paramref name="key"/>, marking it most-recently-used, or <see langword="null"/> if it is not cached.</summary>
    public Bitmap? TryGet(Key key)
    {
        if (!_index.TryGetValue(key, out var node))
            return null;

        _lru.Remove(node);
        _lru.AddFirst(node);
        return node.Value.Bitmap;
    }

    /// <summary>
    /// Caches <paramref name="bitmap"/> under <paramref name="key"/>,
    /// taking ownership of it, then evicts least-recently-used entries
    /// until <see cref="UsedBytes"/> is within <see cref="BudgetBytes"/>
    /// again — including <paramref name="bitmap"/> itself, if
    /// <paramref name="sizeBytes"/> alone exceeds the whole budget, so this
    /// cache's own invariant (never holds more than its budget) has no
    /// exception for a single oversized entry.
    /// </summary>
    public void Add(Key key, Bitmap bitmap, long sizeBytes)
    {
        ArgumentNullException.ThrowIfNull(bitmap);

        if (_index.TryGetValue(key, out var existing))
        {
            _lru.Remove(existing);
            _index.Remove(key);
            UsedBytes -= existing.Value.SizeBytes;
            existing.Value.Bitmap.Dispose();
        }

        var node = _lru.AddFirst(new Entry(key, bitmap, sizeBytes));
        _index[key] = node;
        UsedBytes += sizeBytes;

        while (UsedBytes > _budgetBytes && _lru.Last is { } last)
        {
            _lru.RemoveLast();
            _index.Remove(last.Value.Key);
            UsedBytes -= last.Value.SizeBytes;
            last.Value.Bitmap.Dispose();
        }
    }

    /// <summary>Removes and disposes every cached tile.</summary>
    public void Clear()
    {
        foreach (var entry in _lru)
            entry.Bitmap.Dispose();

        _lru.Clear();
        _index.Clear();
        UsedBytes = 0;
    }

    /// <inheritdoc />
    public void Dispose() => Clear();
}
