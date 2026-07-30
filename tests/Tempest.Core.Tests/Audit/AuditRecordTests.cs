using Tempest.Core.Audit;

namespace Tempest.Core.Tests.Audit;

public class AuditRecordTests
{
    private static readonly Dictionary<string, string> EmptyDetail = [];

    [Fact]
    public void Constructor_ValidArguments_SetsProperties()
    {
        var occurredAt = DateTimeOffset.UtcNow;
        var detail = new Dictionary<string, string> { ["key"] = "value" };

        var record = new AuditRecord("actor-1", "action.performed", occurredAt, detail);

        Assert.Equal("actor-1", record.ActorId);
        Assert.Equal("action.performed", record.Action);
        Assert.Equal(occurredAt, record.OccurredAt);
        Assert.Equal(detail, record.Detail);
    }

    [Fact]
    public void Constructor_EmptyDetail_IsAllowed()
    {
        var record = new AuditRecord("actor-1", "action.performed", DateTimeOffset.UtcNow, EmptyDetail);

        Assert.Empty(record.Detail);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_NullEmptyOrWhitespaceActorId_ThrowsArgumentException(string? actorId)
    {
        Assert.Throws<ArgumentException>(() => new AuditRecord(actorId!, "action", DateTimeOffset.UtcNow, EmptyDetail));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_NullEmptyOrWhitespaceAction_ThrowsArgumentException(string? action)
    {
        Assert.Throws<ArgumentException>(() => new AuditRecord("actor-1", action!, DateTimeOffset.UtcNow, EmptyDetail));
    }

    [Fact]
    public void Constructor_NullDetail_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new AuditRecord("actor-1", "action", DateTimeOffset.UtcNow, null!));
    }
}
