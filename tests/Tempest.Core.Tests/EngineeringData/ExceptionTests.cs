using Tempest.Core.EngineeringData;

namespace Tempest.Core.Tests.EngineeringData;

public class ExceptionTests
{
    [Fact]
    public void EngineeringDataException_MessageConstructor_SetsMessage()
    {
        var exception = new EngineeringDataException("something went wrong");

        Assert.Equal("something went wrong", exception.Message);
    }

    [Fact]
    public void EngineeringDocumentNotFoundException_IsAnEngineeringDataException()
    {
        var documentId = Guid.NewGuid();

        var exception = new EngineeringDocumentNotFoundException(documentId);

        Assert.IsAssignableFrom<EngineeringDataException>(exception);
        Assert.Equal(documentId, exception.DocumentId);
        Assert.Contains(documentId.ToString(), exception.Message);
    }
}
