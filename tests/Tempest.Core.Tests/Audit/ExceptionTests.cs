using Tempest.Core.Audit;

namespace Tempest.Core.Tests.Audit;

public class ExceptionTests
{
    [Fact]
    public void AuditException_MessageConstructor_SetsMessage()
    {
        var exception = new AuditException("something went wrong");

        Assert.Equal("something went wrong", exception.Message);
    }

    [Fact]
    public void AuditException_IsAnException()
    {
        var exception = new AuditException("message");

        Assert.IsAssignableFrom<Exception>(exception);
    }
}
