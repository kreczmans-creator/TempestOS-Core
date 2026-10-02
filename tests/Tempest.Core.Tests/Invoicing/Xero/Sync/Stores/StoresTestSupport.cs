using System.Collections.Concurrent;
using Tempest.Core.Invoicing.Xero.Sync;
using Tempest.Core.Persistence;

namespace Tempest.Core.Tests.Invoicing.Xero.Sync.Stores;

/// <summary>
/// An in-memory <see cref="IPersistenceStore"/> for the B2 store tests —
/// test-local, per this codebase's convention of small fakes. Every call
/// yields first, so concurrent callers genuinely interleave between a
/// store's read and its write (what the stores' gates must survive).
/// </summary>
internal sealed class YieldingInMemoryPersistenceStore : IPersistenceStore
{
    private readonly ConcurrentDictionary<(string Collection, string Key), string> _values = new();

    public int Writes;

    public string? Raw(string collection, string key) => _values.TryGetValue((collection, key), out var value) ? value : null;

    public void Seed(string collection, string key, string value) => _values[(collection, key)] = value;

    public async Task<string?> ReadAsync(string collection, string key, CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        return Raw(collection, key);
    }

    public async Task WriteAsync(string collection, string key, string value, CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        Interlocked.Increment(ref Writes);
        _values[(collection, key)] = value;
    }

    public async Task DeleteAsync(string collection, string key, CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        _values.TryRemove((collection, key), out _);
    }

    public async Task<IReadOnlyList<string>> ListKeysAsync(string collection, CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        return _values.Keys.Where(k => k.Collection == collection).Select(k => k.Key).ToList();
    }
}

/// <summary>A clock the test moves by hand — never the wall clock, never a sleep.</summary>
internal sealed class SteppingClock(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset _now = start;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by) => _now += by;
}

/// <summary>Shared values for the B2 store tests.</summary>
internal static class StoresFixtures
{
    public const string DemoTenant = "6f1a0c2e-0000-4000-8000-000000000d30";
    public const string LiveTenant = "9b7d3e11-0000-4000-8000-0000000011fe";

    public static readonly DateTimeOffset Start = new(2026, 10, 2, 9, 0, 0, TimeSpan.Zero);

    public static XeroDocumentRef Quote(int n) => XeroDocumentRef.For(XeroDocumentKind.Quote, new Guid(n, 0, 0, new byte[8]));

    public static XeroDocumentRef Invoice(int n) => XeroDocumentRef.For(XeroDocumentKind.Invoice, new Guid(n, 0, 0, new byte[8]));

    public static XeroLink Link(string tenant, XeroDocumentRef document, string xeroId = "a1b2c3d4-0000-4000-8000-000000000001", string linkedBy = "created") =>
        new(
            XeroLink.CurrentSchemaVersion, tenant, document, xeroId,
            XeroNumber: "QU-0001", LastPushedContentHash: "hash-1", LastKnownXeroStatus: "DRAFT",
            AttachmentFileName: "P0012-Q-001.pdf", AttachmentContentHash: new string('a', 64),
            LinkedAtUtc: Start, LastReadAtUtc: Start.AddMinutes(5), LinkedBy: linkedBy);
}
