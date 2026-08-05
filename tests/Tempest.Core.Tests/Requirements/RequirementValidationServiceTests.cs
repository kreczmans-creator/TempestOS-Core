using Tempest.Core.EngineeringData;
using Tempest.Core.Identity;
using Tempest.Core.Persistence;
using Tempest.Core.Requirements;
using Tempest.Core.Verification;

namespace Tempest.Core.Tests.Requirements;

/// <summary>Covers <see cref="RequirementValidationService"/> — `WP 9.1A`'s own new Requirements-scoped validation contract.</summary>
public class RequirementValidationServiceTests
{
    private static (IRequirementsService Requirements, IVerificationService Verification, RequirementValidationService Validation) BuildServices()
    {
        var store = new InMemoryPersistenceStore();
        var principalAccessor = new CurrentPrincipalAccessor();
        var documentStore = new EngineeringDocumentStore(store, principalAccessor);
        var permissionEvaluator = new PermissionEvaluator();
        var verificationService = new VerificationService(documentStore, principalAccessor, permissionEvaluator);
        var requirementsService = new RequirementsService(documentStore, store, principalAccessor, verificationService);
        principalAccessor.SetCurrent(new PlatformPrincipal(new PlatformIdentity("test-user", "test-user"), [VerificationService.ReadPermission]));

        return (requirementsService, verificationService, new RequirementValidationService(requirementsService));
    }

    [Fact]
    public async Task ValidateAsync_UnknownRequirement_Throws()
    {
        var (_, _, validation) = BuildServices();

        await Assert.ThrowsAsync<RequirementNotFoundException>(() => validation.ValidateAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task ValidateAsync_FreshRequirementWithNoRelationships_IsAnOrphanWarning()
    {
        var (requirements, _, validation) = BuildServices();
        var requirement = await requirements.CreateAsync("REQ-1", "The system shall do X.");

        var result = await validation.ValidateAsync(requirement.Id);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        Assert.Contains(result.Warnings, w => w.Code == "TEMPEST-REQ-VAL-002");
    }

    [Fact]
    public async Task ValidateAsync_RequirementWithAnyOutgoingRelationship_IsNotAnOrphan()
    {
        var (requirements, _, validation) = BuildServices();
        var requirementA = await requirements.CreateAsync("REQ-1", "The system shall do X.");
        var requirementB = await requirements.CreateAsync("REQ-2", "The system shall do Y.");
        await requirements.LinkAsync(requirementA.Id, requirementB.Id, RequirementRelationshipKinds.DependsOn);

        var result = await validation.ValidateAsync(requirementA.Id);

        Assert.DoesNotContain(result.Warnings, w => w.Code == "TEMPEST-REQ-VAL-002");
    }

    [Fact]
    public async Task ValidateAsync_AllocatedStatusWithNoVerificationOrAllocation_WarnsBoth()
    {
        var (requirements, _, validation) = BuildServices();
        var requirement = await requirements.CreateAsync("REQ-1", "The system shall do X.");
        await requirements.SetStatusAsync(requirement.Id, RequirementStatus.Reviewed);
        await requirements.SetStatusAsync(requirement.Id, RequirementStatus.Approved);
        await requirements.SetStatusAsync(requirement.Id, RequirementStatus.Allocated);

        var result = await validation.ValidateAsync(requirement.Id);

        Assert.Contains(result.Warnings, w => w.Code == "TEMPEST-REQ-VAL-003");
        Assert.Contains(result.Warnings, w => w.Code == "TEMPEST-REQ-VAL-004");
    }

    [Fact]
    public async Task ValidateAsync_AllocatedStatusWithVerificationAndAllocation_DoesNotWarn()
    {
        var (requirements, verification, validation) = BuildServices();
        var requirement = await requirements.CreateAsync("REQ-1", "The system shall do X.");
        var target = await requirements.CreateAsync("REQ-TARGET", "Allocation target.");
        await requirements.SetStatusAsync(requirement.Id, RequirementStatus.Reviewed);
        await requirements.SetStatusAsync(requirement.Id, RequirementStatus.Approved);
        await requirements.SetStatusAsync(requirement.Id, RequirementStatus.Allocated);
        await requirements.LinkAsync(requirement.Id, target.Id, RequirementRelationshipKinds.AllocatedTo);
        await verification.RecordAsync(requirement.Id, VerificationOutcome.Pass, "Inspection", new VerificationContext());

        var result = await validation.ValidateAsync(requirement.Id);

        Assert.DoesNotContain(result.Warnings, w => w.Code == "TEMPEST-REQ-VAL-003");
        Assert.DoesNotContain(result.Warnings, w => w.Code == "TEMPEST-REQ-VAL-004");
    }

    [Fact]
    public async Task ValidateAsync_DraftStatus_DoesNotWarnAboutVerificationOrAllocation()
    {
        var (requirements, _, validation) = BuildServices();
        var requirement = await requirements.CreateAsync("REQ-1", "The system shall do X.");

        var result = await validation.ValidateAsync(requirement.Id);

        Assert.DoesNotContain(result.Warnings, w => w.Code == "TEMPEST-REQ-VAL-003");
        Assert.DoesNotContain(result.Warnings, w => w.Code == "TEMPEST-REQ-VAL-004");
    }

    [Fact]
    public async Task ValidateAsync_RelationshipOutsideThePlatformsOwnNamedSet_WarnsAsAdvisoryOnly()
    {
        var (requirements, _, validation) = BuildServices();
        var requirement = await requirements.CreateAsync("REQ-1", "The system shall do X.");
        var other = await requirements.CreateAsync("REQ-2", "The system shall do Y.");
        await requirements.LinkAsync(requirement.Id, other.Id, "customUnnamedKind");

        var result = await validation.ValidateAsync(requirement.Id);

        Assert.True(result.IsValid);
        Assert.Contains(result.Warnings, w => w.Code == "TEMPEST-REQ-VAL-005");
    }

    [Fact]
    public async Task ValidateAsync_KnownRelationshipKind_DoesNotWarnAboutUnknownKind()
    {
        var (requirements, _, validation) = BuildServices();
        var requirement = await requirements.CreateAsync("REQ-1", "The system shall do X.");
        var other = await requirements.CreateAsync("REQ-2", "The system shall do Y.");
        await requirements.LinkAsync(requirement.Id, other.Id, RequirementRelationshipKinds.DependsOn);

        var result = await validation.ValidateAsync(requirement.Id);

        Assert.DoesNotContain(result.Warnings, w => w.Code == "TEMPEST-REQ-VAL-005");
    }
}
