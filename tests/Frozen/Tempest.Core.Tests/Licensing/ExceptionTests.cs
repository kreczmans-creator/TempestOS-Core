using Tempest.Core.Licensing;

namespace Tempest.Core.Tests.Licensing;

public class ExceptionTests
{
    [Fact]
    public void LicensingException_MessageConstructor_SetsMessage()
    {
        var exception = new LicensingException("something went wrong");

        Assert.Equal("something went wrong", exception.Message);
    }

    [Fact]
    public void LicenseValidationException_IsALicensingException()
    {
        var exception = new LicenseValidationException("license file not found");

        Assert.IsAssignableFrom<LicensingException>(exception);
        Assert.Equal("license file not found", exception.FailureReason);
        Assert.Contains("license file not found", exception.Message);
    }
}
