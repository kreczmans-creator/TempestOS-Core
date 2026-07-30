using Tempest.Core.Audit;

namespace Tempest.Core.Tests.Audit;

public class AuditQueryCriteriaTests
{
    [Fact]
    public void Constructor_NoArguments_EveryPropertyIsNull()
    {
        var criteria = new AuditQueryCriteria();

        Assert.Null(criteria.ActorId);
        Assert.Null(criteria.Action);
        Assert.Null(criteria.From);
        Assert.Null(criteria.To);
    }

    [Fact]
    public void Constructor_ValidArguments_SetsProperties()
    {
        var from = DateTimeOffset.UtcNow.AddDays(-1);
        var to = DateTimeOffset.UtcNow;

        var criteria = new AuditQueryCriteria(actorId: "actor-1", action: "action.performed", from: from, to: to);

        Assert.Equal("actor-1", criteria.ActorId);
        Assert.Equal("action.performed", criteria.Action);
        Assert.Equal(from, criteria.From);
        Assert.Equal(to, criteria.To);
    }

    [Fact]
    public void Constructor_FromEqualsTo_IsAllowed()
    {
        var instant = DateTimeOffset.UtcNow;

        var criteria = new AuditQueryCriteria(from: instant, to: instant);

        Assert.Equal(instant, criteria.From);
        Assert.Equal(instant, criteria.To);
    }

    [Fact]
    public void Constructor_FromLaterThanTo_ThrowsArgumentException()
    {
        var from = DateTimeOffset.UtcNow;
        var to = from.AddDays(-1);

        Assert.Throws<ArgumentException>(() => new AuditQueryCriteria(from: from, to: to));
    }
}
