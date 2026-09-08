using Tempest.Core.Plugins;

namespace Tempest.Core.Tests.Plugins;

// ADR-0112: PluginSignatureEnvelope.TryParse's own malformed-input coverage.
// PluginManifestV2FieldsTests only proves a Signature value round-trips
// verbatim onto PluginManifest.Signature (pre-verification storage) - this
// file proves TryParse's own parsing contract directly: never throws, valid
// envelopes parse, and every combination of missing/blank required field
// returns null.
public class PluginSignatureEnvelopeTests
{
    // ------------------------------------------------------------------
    // Valid parse
    // ------------------------------------------------------------------

    [Fact]
    public void TryParse_WellFormedEnvelope_ParsesAllThreeFields()
    {
        var json = """{"Algorithm":"RSA-SHA256","PublisherCertificateThumbprint":"ABCDEF0123456789","Value":"c29tZS1zaWduYXR1cmU="}""";

        var envelope = PluginSignatureEnvelope.TryParse(json);

        Assert.NotNull(envelope);
        Assert.Equal("RSA-SHA256", envelope!.Algorithm);
        Assert.Equal("ABCDEF0123456789", envelope.PublisherCertificateThumbprint);
        Assert.Equal("c29tZS1zaWduYXR1cmU=", envelope.Value);
    }

    [Fact]
    public void TryParse_PropertyNamesAreCaseInsensitive_StillParses()
    {
        var json = """{"algorithm":"RSA-SHA256","publishercertificatethumbprint":"ABCDEF0123456789","value":"c29tZQ=="}""";

        var envelope = PluginSignatureEnvelope.TryParse(json);

        Assert.NotNull(envelope);
        Assert.Equal("RSA-SHA256", envelope!.Algorithm);
    }

    [Fact]
    public void TryParse_ExtraUnknownProperties_AreIgnored_StillParses()
    {
        var json = """{"Algorithm":"RSA-SHA256","PublisherCertificateThumbprint":"ABCDEF0123456789","Value":"c29tZQ==","Extra":"ignored"}""";

        var envelope = PluginSignatureEnvelope.TryParse(json);

        Assert.NotNull(envelope);
    }

    // ------------------------------------------------------------------
    // Null / blank input
    // ------------------------------------------------------------------

    [Fact]
    public void TryParse_Null_ReturnsNull() =>
        Assert.Null(PluginSignatureEnvelope.TryParse(null!));

    [Fact]
    public void TryParse_EmptyString_ReturnsNull() =>
        Assert.Null(PluginSignatureEnvelope.TryParse(string.Empty));

    [Fact]
    public void TryParse_WhitespaceOnly_ReturnsNull() =>
        Assert.Null(PluginSignatureEnvelope.TryParse("   "));

    // ------------------------------------------------------------------
    // Malformed JSON - never throws
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("not json at all")]
    [InlineData("{ this is not valid json")]
    [InlineData("[1,2,3]")]
    [InlineData("\"just a string\"")]
    [InlineData("42")]
    [InlineData("null")]
    [InlineData("{}")]
    public void TryParse_MalformedOrShapelessJson_ReturnsNull_DoesNotThrow(string rawValue)
    {
        var exception = Record.Exception(() => PluginSignatureEnvelope.TryParse(rawValue));

        Assert.Null(exception);
        Assert.Null(PluginSignatureEnvelope.TryParse(rawValue));
    }

    // ------------------------------------------------------------------
    // Missing / blank required fields
    // ------------------------------------------------------------------

    [Fact]
    public void TryParse_MissingAlgorithm_ReturnsNull()
    {
        var json = """{"PublisherCertificateThumbprint":"ABCDEF0123456789","Value":"c29tZQ=="}""";

        Assert.Null(PluginSignatureEnvelope.TryParse(json));
    }

    [Fact]
    public void TryParse_MissingPublisherCertificateThumbprint_ReturnsNull()
    {
        var json = """{"Algorithm":"RSA-SHA256","Value":"c29tZQ=="}""";

        Assert.Null(PluginSignatureEnvelope.TryParse(json));
    }

    [Fact]
    public void TryParse_MissingValue_ReturnsNull()
    {
        var json = """{"Algorithm":"RSA-SHA256","PublisherCertificateThumbprint":"ABCDEF0123456789"}""";

        Assert.Null(PluginSignatureEnvelope.TryParse(json));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void TryParse_BlankAlgorithm_ReturnsNull(string blankValue)
    {
        var json = $$"""{"Algorithm":"{{blankValue}}","PublisherCertificateThumbprint":"ABCDEF0123456789","Value":"c29tZQ=="}""";

        Assert.Null(PluginSignatureEnvelope.TryParse(json));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void TryParse_BlankPublisherCertificateThumbprint_ReturnsNull(string blankValue)
    {
        var json = $$"""{"Algorithm":"RSA-SHA256","PublisherCertificateThumbprint":"{{blankValue}}","Value":"c29tZQ=="}""";

        Assert.Null(PluginSignatureEnvelope.TryParse(json));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void TryParse_BlankValue_ReturnsNull(string blankValue)
    {
        var json = $$"""{"Algorithm":"RSA-SHA256","PublisherCertificateThumbprint":"ABCDEF0123456789","Value":"{{blankValue}}"}""";

        Assert.Null(PluginSignatureEnvelope.TryParse(json));
    }

    // ------------------------------------------------------------------
    // Constructor guards (direct construction, bypassing TryParse)
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(null, "thumb", "value")]
    [InlineData("", "thumb", "value")]
    [InlineData("   ", "thumb", "value")]
    [InlineData("RSA-SHA256", null, "value")]
    [InlineData("RSA-SHA256", "", "value")]
    [InlineData("RSA-SHA256", "thumb", null)]
    [InlineData("RSA-SHA256", "thumb", "")]
    public void Constructor_NullEmptyOrWhitespaceArgument_ThrowsArgumentException(
        string? algorithm, string? thumbprint, string? value) =>
        // ArgumentException.ThrowIfNullOrWhiteSpace throws the more specific
        // ArgumentNullException for a null argument and plain ArgumentException
        // for empty/whitespace - ThrowsAny accepts either, since both are
        // ArgumentException, which is this guard's real contract.
        Assert.ThrowsAny<ArgumentException>(() => new PluginSignatureEnvelope(algorithm!, thumbprint!, value!));
}
