using Tempest.Core.Materials;

namespace Tempest.Core.Tests.Materials;

public class ExceptionTests
{
    [Fact]
    public void MaterialsException_MessageConstructor_SetsMessage()
    {
        var exception = new MaterialsException("something went wrong");

        Assert.Equal("something went wrong", exception.Message);
    }

    [Fact]
    public void DuplicateMaterialException_IsAMaterialsException()
    {
        var exception = new DuplicateMaterialException("test-001");

        Assert.IsAssignableFrom<MaterialsException>(exception);
        Assert.Equal("test-001", exception.MaterialId);
        Assert.Contains("test-001", exception.Message);
    }

    [Fact]
    public void MaterialNotFoundException_IsAMaterialsException()
    {
        var exception = new MaterialNotFoundException("test-002");

        Assert.IsAssignableFrom<MaterialsException>(exception);
        Assert.Equal("test-002", exception.MaterialId);
        Assert.Contains("test-002", exception.Message);
    }
}
