using Tempest.Core.Modules;
using Tempest.Samples;

namespace Tempest.Core.Tests.Modules;

// Discovery tests for ADR-0027's ModuleMetadataAttribute. Uses the
// internal DiscoverModules(IEnumerable<Type>) seam directly, exactly like
// ReflectionFrameworkDiscoveryServiceTests, so results are deterministic
// and independent of whatever else happens to be loaded into the process.
public class ModuleMetadataAttributeDiscoveryTests
{
    // ----------------------------------------------------------------
    // Metadata discovered from ModuleMetadataAttribute
    // ----------------------------------------------------------------

    [Fact]
    public void DiscoverModules_AttributeCarryingType_ReadsMetadataFromAttribute_WithoutConstructing()
    {
        var service = new ReflectionFrameworkDiscoveryService();

        // AttributeDeclaredModule has no parameterless constructor at all -
        // if discovery ever tried Activator.CreateInstance(type), this
        // would throw. Success here is direct proof it never does.
        var result = service.DiscoverModules([typeof(AttributeDeclaredModule)]);

        var descriptor = Assert.Single(result);
        Assert.Equal("test.attribute.declared", descriptor.Id);
        Assert.Equal("Attribute Declared Module", descriptor.Name);
        Assert.Equal("1.0.0", descriptor.Version);
        Assert.Equal(typeof(AttributeDeclaredModule), descriptor.ModuleType);
    }

    // ----------------------------------------------------------------
    // Parameterised-constructor module discovered successfully
    // ----------------------------------------------------------------

    [Fact]
    public void DiscoverModules_ParameterisedConstructorModule_DiscoveredSuccessfully()
    {
        // Same fixture, different framing: a module whose sole constructor
        // takes a parameter is discovered without error - the central
        // claim ADR-0027 exists to make true.
        var service = new ReflectionFrameworkDiscoveryService();

        var exception = Record.Exception(() => service.DiscoverModules([typeof(AttributeDeclaredModule)]));

        Assert.Null(exception);
    }

    // ----------------------------------------------------------------
    // Zero-argument module still discovered (with the attribute present)
    // ----------------------------------------------------------------

    [Fact]
    public void DiscoverModules_AttributeCarryingType_WithZeroArgConstructor_StillDiscovered()
    {
        var service = new ReflectionFrameworkDiscoveryService();

        var result = service.DiscoverModules([typeof(AttributeZeroArgModule)]);

        var descriptor = Assert.Single(result);
        Assert.Equal("test.attribute.zeroarg", descriptor.Id);
    }

    // ----------------------------------------------------------------
    // Fallback path unchanged - legacy, non-attribute modules
    // ----------------------------------------------------------------

    [Fact]
    public void DiscoverModules_LegacyModulesWithoutAttribute_DiscoveredExactlyAsBefore()
    {
        var service = new ReflectionFrameworkDiscoveryService();

        var result = service.DiscoverModules(
            [typeof(SampleModuleC), typeof(SampleModuleA), typeof(SampleModuleB)]);

        Assert.Equal(3, result.Count);
        Assert.Equal("tempest.sample.alpha", result[0].Id);
        Assert.Equal("tempest.sample.beta", result[1].Id);
        Assert.Equal("tempest.sample.gamma", result[2].Id);
    }

    [Fact]
    public void DiscoverModules_LegacyInvalidMetadata_StillThrowsModuleDiscoveryException()
    {
        var service = new ReflectionFrameworkDiscoveryService();

        Assert.Throws<ModuleDiscoveryException>(() =>
            service.DiscoverModules([typeof(InvalidIdModule)]));
    }

    // ----------------------------------------------------------------
    // Metadata precedence - attribute overrides construction
    // ----------------------------------------------------------------

    [Fact]
    public void DiscoverModules_AttributeAndWorkingConstructorBothPresent_AttributeTakesPrecedence()
    {
        var service = new ReflectionFrameworkDiscoveryService();

        var result = service.DiscoverModules([typeof(AttributePrecedenceModule)]);

        var descriptor = Assert.Single(result);

        // The attribute's own values - never the instance's differing properties.
        Assert.Equal("test.attribute.precedence", descriptor.Id);
        Assert.Equal("Attribute Wins", descriptor.Name);
        Assert.Equal("1.0.0", descriptor.Version);
    }

    // ----------------------------------------------------------------
    // Malformed attribute handling
    // ----------------------------------------------------------------

    [Fact]
    public void DiscoverModules_AttributeWithEmptyId_ThrowsModuleDiscoveryException()
    {
        var service = new ReflectionFrameworkDiscoveryService();

        Assert.Throws<ModuleDiscoveryException>(() =>
            service.DiscoverModules([typeof(MalformedEmptyIdModule)]));
    }

    [Fact]
    public void DiscoverModules_AttributeWithWhitespaceName_ThrowsModuleDiscoveryException()
    {
        var service = new ReflectionFrameworkDiscoveryService();

        Assert.Throws<ModuleDiscoveryException>(() =>
            service.DiscoverModules([typeof(MalformedWhitespaceNameModule)]));
    }

    [Fact]
    public void DiscoverModules_AttributeWithEmptyVersion_ThrowsModuleDiscoveryException()
    {
        var service = new ReflectionFrameworkDiscoveryService();

        Assert.Throws<ModuleDiscoveryException>(() =>
            service.DiscoverModules([typeof(MalformedEmptyVersionModule)]));
    }

    // ----------------------------------------------------------------
    // Duplicate IDs
    // ----------------------------------------------------------------

    [Fact]
    public void DiscoverModules_TwoAttributeModulesShareId_ThrowsDuplicateModuleIdException()
    {
        var service = new ReflectionFrameworkDiscoveryService();

        var exception = Assert.Throws<DuplicateModuleIdException>(() =>
            service.DiscoverModules([typeof(DuplicateAttributeModuleA), typeof(DuplicateAttributeModuleB)]));

        Assert.Equal("test.attribute.duplicate", exception.ModuleId);
    }

    [Fact]
    public void DiscoverModules_AttributeModuleSharesIdWithLegacyModule_ThrowsDuplicateModuleIdException()
    {
        var service = new ReflectionFrameworkDiscoveryService();

        var exception = Assert.Throws<DuplicateModuleIdException>(() =>
            service.DiscoverModules([typeof(SampleModuleA), typeof(AttributeDuplicateOfSampleModuleA)]));

        Assert.Equal("tempest.sample.alpha", exception.ModuleId);
    }

    // ----------------------------------------------------------------
    // Mixed assemblies - attribute-based and legacy modules together,
    // spanning two genuinely different compiled assemblies.
    // ----------------------------------------------------------------

    [Fact]
    public void DiscoverModules_AttributeModuleAlongsideRealLegacyModuleFromAnotherAssembly_BothDiscovered()
    {
        var service = new ReflectionFrameworkDiscoveryService();

        // ClockModule (Tempest.Samples, WP 4.3) is a real, unmodified,
        // legacy (non-attribute) production module from a different
        // compiled assembly than this test's own fixtures.
        var result = service.DiscoverModules([typeof(AttributeDeclaredModule), typeof(ClockModule)]);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, d => d.Id == "test.attribute.declared" && d.ModuleType == typeof(AttributeDeclaredModule));
        Assert.Contains(result, d => d.Id == "tempest.samples.clock" && d.ModuleType == typeof(ClockModule));
    }

    // ----------------------------------------------------------------
    // Deterministic discovery unchanged
    // ----------------------------------------------------------------

    [Fact]
    public void DiscoverModules_MixedAttributeAndLegacyModules_OrderedOrdinallyById_RegardlessOfMechanism()
    {
        var service = new ReflectionFrameworkDiscoveryService();

        var result = service.DiscoverModules(
            [typeof(SampleModuleC), typeof(AttributeDeclaredModule), typeof(SampleModuleA), typeof(AttributeZeroArgModule)]);

        Assert.Equal(
            new[] { "tempest.sample.alpha", "tempest.sample.gamma", "test.attribute.declared", "test.attribute.zeroarg" },
            result.Select(d => d.Id));
    }

    [Fact]
    public void DiscoverModules_CalledTwice_ReturnsTheSameResultBothTimes()
    {
        var service = new ReflectionFrameworkDiscoveryService();
        var candidates = new[] { typeof(AttributeDeclaredModule), typeof(SampleModuleA) };

        var first = service.DiscoverModules(candidates);
        var second = service.DiscoverModules(candidates);

        Assert.Equal(first.Select(d => d.Id), second.Select(d => d.Id));
    }
}
