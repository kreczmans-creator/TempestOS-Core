using Tempest.App.Workspace;
using Tempest.App.Workspace.Calculations;
using Tempest.App.Workspace.Documents;
using Tempest.App.Workspace.Manufacturing;
using Tempest.App.Workspace.Mechanical;
using Tempest.App.Workspace.Verification;
using Tempest.Core.EngineeringData;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Identity;
using Tempest.Core.Persistence;

namespace Tempest.Core.Tests.EngineeringDomain;

/// <summary>
/// Production rehydration: every persistable Kind comes back after a
/// restart, and none of it depends on <c>Tempest.Samples</c>.
/// </summary>
/// <remarks>
/// <para>
/// Twelve canonical Kinds used to be registered only by
/// <c>SampleEngineeringObjectRehydrators</c> and nine by nothing at all,
/// so a Risk, a Task or a Hazard survived a restart only because the
/// sample harness happened to ship (`TD-75`), or did not survive at all.
/// </para>
/// <para>
/// <b>The load-bearing test here is
/// <see cref="ProductionRegistration_UsesNoTypeFromTempestSamples"/>.</b>
/// Everything else could pass while the product still quietly depended on
/// the sample assembly, because that assembly is referenced and therefore
/// loaded in this test process. That test asserts the dependency itself
/// rather than the symptom.
/// </para>
/// </remarks>
public sealed class ProductionRehydrationTests
{
    private const string SampleAssemblyName = "Tempest.Samples";

    /// <summary>Every Kind the product must be able to rehydrate, and the type it comes back as.</summary>
    /// <remarks>
    /// Written out rather than derived, deliberately: a list generated
    /// from the registry would agree with the registry by construction and
    /// prove nothing. This is the independent statement of what the
    /// product owes.
    /// </remarks>
    public static TheoryData<string, Type> EveryProductionKind()
    {
        var data = new TheoryData<string, Type>();

        // The seventeen that always had production rehydrators.
        data.Add(MechanicalObjectFactoryRegistry.Project, typeof(Project));
        data.Add(MechanicalObjectFactoryRegistry.Assembly, typeof(Tempest.Core.EngineeringDomain.Assembly));
        data.Add(MechanicalObjectFactoryRegistry.SubAssembly, typeof(SubAssembly));
        data.Add(MechanicalObjectFactoryRegistry.Part, typeof(Part));
        data.Add(MechanicalObjectFactoryRegistry.Component, typeof(Component));
        data.Add(MechanicalObjectFactoryRegistry.Configuration, typeof(Tempest.Core.EngineeringDomain.Configuration));
        data.Add(MechanicalObjectFactoryRegistry.Baseline, typeof(Baseline));
        data.Add(MechanicalObjectFactoryRegistry.Release, typeof(Release));
        data.Add(DocumentObjectFactoryRegistry.Document, typeof(Document));
        data.Add(DocumentObjectFactoryRegistry.Drawing, typeof(Drawing));
        data.Add(DocumentObjectFactoryRegistry.CadModel, typeof(CadModel));
        data.Add(CalculationObjectFactoryRegistry.CalculationKind, typeof(Calculation));
        data.Add(CalculationObjectFactoryRegistry.CalculationSetKind, typeof(CalculationSet));
        data.Add(ManufacturingObjectFactoryRegistry.ManufacturingOperationKind, typeof(ManufacturingOperation));
        data.Add(ManufacturingObjectFactoryRegistry.WorkInstructionKind, typeof(WorkInstruction));
        data.Add(ManufacturingObjectFactoryRegistry.InspectionKind, typeof(Inspection));
        data.Add(VerificationActivityFactoryRegistry.SupportedKind, typeof(VerificationActivity));

        // The twelve that were registered only by Tempest.Samples.
        data.Add(CanonicalObjectKinds.Portfolio, typeof(Portfolio));
        data.Add(CanonicalObjectKinds.Programme, typeof(Programme));
        data.Add(CanonicalObjectKinds.Risk, typeof(Risk));
        data.Add(CanonicalObjectKinds.Decision, typeof(Decision));
        data.Add(CanonicalObjectKinds.Task, typeof(EngineeringTask));
        data.Add(CanonicalObjectKinds.Milestone, typeof(Milestone));
        data.Add(CanonicalObjectKinds.Deliverable, typeof(Deliverable));
        data.Add(CanonicalObjectKinds.ChangeRequest, typeof(ChangeRequest));
        data.Add(CanonicalObjectKinds.EngineeringChange, typeof(EngineeringChange));
        data.Add(CanonicalObjectKinds.Supplier, typeof(Supplier));
        data.Add(CanonicalObjectKinds.PurchaseItem, typeof(PurchaseItem));
        data.Add(CanonicalObjectKinds.ExternalSystemLink, typeof(ExternalSystemLink));

        // The nine that were registered nowhere at all.
        data.Add(CanonicalObjectKinds.Hazard, typeof(Hazard));
        data.Add(CanonicalObjectKinds.Issue, typeof(Issue));
        data.Add(CanonicalObjectKinds.Assumption, typeof(Assumption));
        data.Add(CanonicalObjectKinds.Action, typeof(EngineeringAction));
        data.Add(CanonicalObjectKinds.Approval, typeof(Approval));
        data.Add(CanonicalObjectKinds.Review, typeof(Review));
        data.Add(CanonicalObjectKinds.Simulation, typeof(Simulation));
        data.Add(CanonicalObjectKinds.Test, typeof(Test));
        data.Add(CanonicalObjectKinds.Verification, typeof(Tempest.Core.EngineeringDomain.Verification));

        return data;
    }

    // ================================================================
    // The registration set, proven independently of Tempest.Samples
    // ================================================================

    [Theory]
    [MemberData(nameof(EveryProductionKind))]
    public void EveryKind_HasAProductionRehydrator_ThatComesBackAsTheRightType(string kind, Type expected)
    {
        var registry = BuildProductionRegistry();

        var rehydrator = registry.Find(kind);

        Assert.NotNull(rehydrator);
        Assert.Equal(expected, rehydrator!.ObjectType);
    }

    [Fact]
    public void ProductionRegistration_UsesNoTypeFromTempestSamples()
    {
        // The proof that matters. Tempest.Samples is referenced by this
        // test process, so every other test here would pass just as
        // happily if the product still leaned on it. This asserts the
        // production registration path declares nothing from that
        // assembly — which is what "works without the samples" actually
        // means when you cannot unload an assembly to check.
        var registeringTypes = new[]
        {
            typeof(MechanicalObjectFactoryRegistry),
            typeof(DocumentObjectFactoryRegistry),
            typeof(CalculationObjectFactoryRegistry),
            typeof(VerificationActivityFactoryRegistry),
            typeof(ManufacturingObjectFactoryRegistry),
            typeof(CanonicalObjectKinds),
        };

        foreach (var type in registeringTypes)
        {
            Assert.NotEqual(SampleAssemblyName, type.Assembly.GetName().Name);

            var method = type.GetMethod("RegisterRehydrators", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            Assert.NotNull(method);
        }

        // And the types every registered Kind comes back as are domain
        // types, never sample types.
        var registry = BuildProductionRegistry();
        foreach (var (kind, _) in EveryProductionKind().Select(row => ((string)row[0]!, (Type)row[1]!)))
        {
            var objectType = registry.Find(kind)!.ObjectType;
            Assert.NotEqual(SampleAssemblyName, objectType.Assembly.GetName().Name);
        }
    }

    [Fact]
    public void TheProductionRegistry_CoversEveryPersistableDomainType()
    {
        // The check that would have caught the nine orphans. Any
        // EngineeringObjectBase in Tempest.Core that declares itself
        // rehydratable can be written to disk, so the product owes it a
        // rehydrator — otherwise it is durable in theory and discarded in
        // practice.
        var registry = BuildProductionRegistry();
        var registeredTypes = EveryProductionKind()
            .Select(row => (Type)row[1]!)
            .ToHashSet();

        var persistable = typeof(EngineeringObjectBase).Assembly
            .GetTypes()
            .Where(t => t is { IsAbstract: false, IsPublic: true })
            .Where(t => t.IsSubclassOf(typeof(EngineeringObjectBase)))
            .Where(t => t.GetInterfaces().Any(i =>
                i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRehydratable<>)))
            .ToList();

        var uncovered = persistable.Where(t => !registeredTypes.Contains(t)).Select(t => t.Name).Order(StringComparer.Ordinal).ToList();

        Assert.True(
            uncovered.Count == 0,
            "Persistable domain types with no production rehydrator:\n" + string.Join("\n", uncovered));

        // And every one of them really is reachable through the registry.
        var reachable = registry.RegisteredKinds.Select(k => registry.Find(k)!.ObjectType).ToHashSet();
        foreach (var type in persistable)
            Assert.Contains(type, reachable);
    }

    [Fact]
    public void NoKindIsRegisteredTwice_ByTwoDifferentRegistrationPaths()
    {
        // Registering the same type for the same Kind twice is a no-op by
        // design; registering a *different* type throws. Running the whole
        // production registration twice therefore proves both that no two
        // paths claim the same Kind and that the composition root is safe
        // to run again.
        var context = BuildContext();
        var registry = new EngineeringObjectRehydratorRegistry();

        RegisterProduction(registry, context);
        RegisterProduction(registry, context);

        Assert.Equal(EveryProductionKind().Count, registry.RegisteredKinds.Count);
    }

    [Fact]
    public void CanonicalObjectKinds_DeclaresEveryKindItRegisters()
    {
        var context = BuildContext();
        var registry = new EngineeringObjectRehydratorRegistry();
        CanonicalObjectKinds.RegisterRehydrators(registry, context);

        Assert.Equal(
            CanonicalObjectKinds.All.Order(StringComparer.Ordinal),
            registry.RegisteredKinds.Order(StringComparer.Ordinal));

        Assert.Equal(21, CanonicalObjectKinds.All.Count);
        Assert.Equal(CanonicalObjectKinds.All.Count, CanonicalObjectKinds.All.Distinct(StringComparer.Ordinal).Count());
    }

    // ================================================================
    // Fixtures
    // ================================================================

    private static EngineeringDomainContext BuildContext()
    {
        var store = new Materials.InMemoryPersistenceStore();
        var principal = new CurrentPrincipalAccessor();
        var documents = new EngineeringDocumentStore(store, principal);
        var repository = new InMemoryEngineeringObjectRepository();
        var relationships = new InMemoryEngineeringRelationshipRepository();
        var discovery = new RelationshipDiscoveryService(relationships, repository);

        return new EngineeringDomainContext(
            documents, repository, relationships, new LifecycleTransitionTable(), new ValidationRuleSet(),
            new EvidenceComposer(discovery, repository), principal, new EngineeringObjectStateStore(store));
    }

    /// <summary>Exactly the registration the production composition root performs — and nothing else.</summary>
    private static void RegisterProduction(IEngineeringObjectRehydratorRegistry registry, EngineeringDomainContext context)
    {
        MechanicalObjectFactoryRegistry.RegisterRehydrators(registry, context);
        DocumentObjectFactoryRegistry.RegisterRehydrators(registry, context);
        CalculationObjectFactoryRegistry.RegisterRehydrators(registry, context);
        VerificationActivityFactoryRegistry.RegisterRehydrators(registry, context);
        ManufacturingObjectFactoryRegistry.RegisterRehydrators(registry, context);
        CanonicalObjectKinds.RegisterRehydrators(registry, context);
    }

    private static EngineeringObjectRehydratorRegistry BuildProductionRegistry()
    {
        var registry = new EngineeringObjectRehydratorRegistry();
        RegisterProduction(registry, BuildContext());
        return registry;
    }
}
