using Tempest.App.Composition;
using Tempest.Core.Api;
using Tempest.Core.EngineeringDomain;

namespace Tempest.Companion.Tests;

// Proves the server-side body-to-command binder
// (CompanionApiRegistration.BindSetDocumentStatus, ADR-0114): a valid
// body binds to the existing SetDocumentStatusCommand unchanged, and
// every malformed shape throws ApiRequestBindingException (mapped to 400
// by the pipeline), never an unhandled exception.
public class CompanionBindingTests
{
    [Fact]
    public void ValidBody_BindsToTheExistingCommand()
    {
        var id = Guid.NewGuid();

        var command = CompanionApiRegistration.BindSetDocumentStatus(
            $$"""{"targetObjectId":"{{id}}","targetKind":"Document","status":"Approved"}""");

        Assert.Equal(id, command.TargetObjectId);
        Assert.Equal("Document", command.TargetKind);
        Assert.Equal(LifecycleState.Approved, command.Status);
    }

    [Fact]
    public void StatusName_IsCaseInsensitive()
    {
        var command = CompanionApiRegistration.BindSetDocumentStatus(
            $$"""{"targetObjectId":"{{Guid.NewGuid()}}","targetKind":"Drawing","status":"inreview"}""");

        Assert.Equal(LifecycleState.InReview, command.Status);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MissingBody_Throws(string? body)
    {
        Assert.Throws<ApiRequestBindingException>(() => CompanionApiRegistration.BindSetDocumentStatus(body));
    }

    [Fact]
    public void EmptyGuid_Throws()
    {
        Assert.Throws<ApiRequestBindingException>(() => CompanionApiRegistration.BindSetDocumentStatus(
            $$"""{"targetObjectId":"{{Guid.Empty}}","targetKind":"Document","status":"Approved"}"""));
    }

    [Fact]
    public void NonDocumentKind_Throws()
    {
        // The action stays inside the Documents discipline's own intent -
        // it must not become a generic lifecycle mutator by accident.
        Assert.Throws<ApiRequestBindingException>(() => CompanionApiRegistration.BindSetDocumentStatus(
            $$"""{"targetObjectId":"{{Guid.NewGuid()}}","targetKind":"Requirement","status":"Approved"}"""));
    }

    [Fact]
    public void UnknownStatus_Throws()
    {
        Assert.Throws<ApiRequestBindingException>(() => CompanionApiRegistration.BindSetDocumentStatus(
            $$"""{"targetObjectId":"{{Guid.NewGuid()}}","targetKind":"Document","status":"Perfect"}"""));
    }

    [Fact]
    public void MalformedJson_ThrowsJsonException_WhichThePipelineMapsTo400()
    {
        Assert.ThrowsAny<System.Text.Json.JsonException>(() => CompanionApiRegistration.BindSetDocumentStatus("{ not json"));
    }
}
