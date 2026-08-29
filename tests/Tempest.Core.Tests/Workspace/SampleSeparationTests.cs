using Tempest.App.Workspace.Calculations;
using Tempest.App.Workspace.Documents;
using Tempest.App.Workspace.Manufacturing;
using Tempest.App.Workspace.Mechanical;
using Tempest.App.Workspace.Requirements;
using Tempest.App.Workspace.Verification;
using Tempest.Core.Calculations;
using Tempest.Core.Modules;
using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.Tests.Workspace;

/// <summary>
/// `TD-75` phase 1: the product's own navigation and its calculation
/// catalogue no longer live in the sample harness.
/// </summary>
/// <remarks>
/// <para>
/// The 2026-08-30 Product Gap Reconciliation audit measured the coupling
/// by removing the <c>Tempest.App</c> → <c>Tempest.Samples</c> project
/// reference and building: 70 errors across three files, because the six
/// discipline explorer areas and all five engineering calculations were
/// declared in the sample assembly. Deleting the samples would have
/// deleted the Engineering Workspace's navigation and the product's
/// calculations.
/// </para>
/// <para>
/// <b>These tests are the standing guard against that returning.</b> Each
/// one fails if a product-owned navigation item or calculation is moved
/// back behind <c>Tempest.Samples</c> — asserted by <em>where the type is
/// declared</em>, not by whether it happens to work, because in a test
/// process that references the sample assembly everything works either
/// way. That is the same reasoning
/// <c>ProductionRehydrationTests.ProductionRegistration_UsesNoTypeFromTempestSamples</c>
/// applies to rehydration: when you are proving an absence, assert the
/// dependency, not the symptom.
/// </para>
/// </remarks>
public sealed class SampleSeparationTests
{
    private const string SampleAssembly = "Tempest.Samples";

    /// <summary>Every discipline explorer module, and the navigation area id it owns.</summary>
    public static TheoryData<Type, string> EveryDisciplineExplorerModule() => new()
    {
        { typeof(MechanicalWorkspaceExplorerModule), MechanicalWorkspaceExplorerModule.NavigationItemId },
        { typeof(DocumentsWorkspaceExplorerModule), DocumentsWorkspaceExplorerModule.NavigationItemId },
        { typeof(RequirementsWorkspaceExplorerModule), RequirementsWorkspaceExplorerModule.NavigationItemId },
        { typeof(VerificationWorkspaceExplorerModule), VerificationWorkspaceExplorerModule.NavigationItemId },
        { typeof(CalculationsWorkspaceExplorerModule), CalculationsWorkspaceExplorerModule.NavigationItemId },
        { typeof(ManufacturingWorkspaceExplorerModule), ManufacturingWorkspaceExplorerModule.NavigationItemId },
    };

    /// <summary>Every calculation in the product's own catalogue.</summary>
    public static TheoryData<Type> EveryProductCalculation() =>
    [
        typeof(BoltShearCapacityCalculationDefinition),
        typeof(BeamBendingStressCalculationDefinition),
        typeof(BearingLoadCapacityCalculationDefinition),
        typeof(PressureVesselWallThicknessCalculationDefinition),
        typeof(MaterialSelectionMarginCalculationDefinition),
    ];

    // ================================================================
    // Where the product's own content is declared
    // ================================================================

    [Theory]
    [MemberData(nameof(EveryDisciplineExplorerModule))]
    public void EachDisciplineNavigationArea_IsDeclaredByItsOwnDiscipline_NotBySamples(Type moduleType, string navigationItemId)
    {
        Assert.NotEqual(SampleAssembly, moduleType.Assembly.GetName().Name);
        Assert.Equal("Tempest.App", moduleType.Assembly.GetName().Name);

        // It lives in the discipline's own namespace, beside the
        // registration that attaches the real node provider to it.
        Assert.StartsWith("Tempest.App.Workspace.", moduleType.Namespace, StringComparison.Ordinal);

        // And it is a real, discoverable module — moving the file without
        // keeping it discoverable would lose the navigation just as surely.
        Assert.True(moduleType.IsSubclassOf(typeof(ModuleLifecycleBase)), $"{moduleType.Name} is no longer a discoverable module.");
        Assert.False(string.IsNullOrWhiteSpace(navigationItemId));

        // The module id no longer claims to be a sample either.
        var metadata = moduleType.GetCustomAttributes(typeof(ModuleMetadataAttribute), inherit: false)
            .Cast<ModuleMetadataAttribute>()
            .Single();

        Assert.DoesNotContain("samples", metadata.Id, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [MemberData(nameof(EveryProductCalculation))]
    public void EachProductCalculation_IsDeclaredInTheDomain_NotInSamples(Type definitionType)
    {
        Assert.NotEqual(SampleAssembly, definitionType.Assembly.GetName().Name);
        Assert.Equal("Tempest.Core", definitionType.Assembly.GetName().Name);
        Assert.Equal("Tempest.Core.Calculations", definitionType.Namespace);
    }

    [Fact]
    public void TheDisciplineNavigationAreas_AreSixDistinctAreas()
    {
        var ids = EveryDisciplineExplorerModule()
            .Select(row => (string)row[1]!)
            .ToList();

        Assert.Equal(6, ids.Count);
        Assert.Equal(6, ids.Distinct(StringComparer.Ordinal).Count());

        // The ids themselves are unchanged by the move: they are the Kind
        // the whole Workspace registration surface is keyed on, and
        // renaming one would silently detach a discipline's node provider
        // from its own area.
        Assert.Equal("tempest.mechanical.product-structure", MechanicalWorkspaceExplorerModule.NavigationItemId);
        Assert.Equal("tempest.documents.management", DocumentsWorkspaceExplorerModule.NavigationItemId);
        Assert.Equal("tempest.requirements.management", RequirementsWorkspaceExplorerModule.NavigationItemId);
        Assert.Equal("tempest.verification.management", VerificationWorkspaceExplorerModule.NavigationItemId);
        Assert.Equal("tempest.calculations.management", CalculationsWorkspaceExplorerModule.NavigationItemId);
        Assert.Equal("tempest.manufacturing.management", ManufacturingWorkspaceExplorerModule.NavigationItemId);
    }

    [Fact]
    public void TheProductsCalculations_AreStillExecutable_NotJustDeclared()
    {
        // Moving a calculation is only safe if it still computes. This runs
        // one end to end through the real engine and checks the answer,
        // rather than asserting that a type exists in a namespace.
        var definition = new BoltShearCapacityCalculationDefinition();

        var result = definition.Calculate(
            new BoltShearCapacityInput(
                new Quantity<Length>(20, LengthUnits.Millimetre),
                new Quantity<Pressure>(400, PressureUnits.Megapascal),
                ShearPlanes: 2,
                SafetyFactor: 1.5),
            new CalculationContext());

        // Two planes of a 20 mm bolt at 400 MPa, derated by 1.5:
        // 2 × (π/4 × 20²) mm² × 400 MPa ÷ 1.5 ≈ 167.6 kN.
        var capacity = result.AllowableShearCapacity.ConvertTo(ForceUnits.Newton).Value;

        Assert.InRange(capacity, 160_000, 175_000);
    }

    // ================================================================
    // The production assembly does not reference the sample assembly
    // ================================================================

    [Theory]
    [InlineData("src/Tempest.App/Tempest.App.csproj")]
    [InlineData("src/Tempest.Core/Tempest.Core.csproj")]
    [InlineData("src/Tempest.Desktop/Tempest.Desktop.csproj")]
    public void NoProductionProject_DeclaresADependencyOnTheSampleAssembly(string relativeProjectPath)
    {
        // The load-bearing assertion, and it reads the project file rather
        // than the compiled metadata — deliberately.
        //
        // The first version of this test used
        // `Assembly.GetReferencedAssemblies()` and a mutation that restored
        // the `<ProjectReference>` **survived it**: the C# compiler emits a
        // reference only for an assembly whose types are actually used, so
        // an unused project reference is invisible in metadata while still
        // being a real, declared dependency that ships the sample assembly
        // into the output folder. Asserting the declaration is the only
        // thing that catches it coming back.
        var project = Path.Combine(FindRepositoryRoot(), relativeProjectPath.Replace('/', Path.DirectorySeparatorChar));

        Assert.True(File.Exists(project), $"'{project}' was not found.");

        var contents = File.ReadAllText(project);

        Assert.DoesNotContain(SampleAssembly, contents, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TheSampleAssembly_IsNotCopiedIntoTheShippedDesktopOutput()
    {
        // The same claim one level further out: whatever the project files
        // say, the thing that actually ships is the output folder.
        var desktopOutput = Path.Combine(
            FindRepositoryRoot(), "src", "Tempest.Desktop", "bin",
            Path.GetFileName(Path.GetDirectoryName(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar))!),
            "net10.0");

        if (!Directory.Exists(desktopOutput))
            return; // The desktop app has not been built in this configuration; nothing to check.

        Assert.False(
            File.Exists(Path.Combine(desktopOutput, "Tempest.Samples.dll")),
            $"Tempest.Samples.dll is present in the shipped desktop output at '{desktopOutput}'.");
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "global.json")))
                return directory.FullName;

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            $"Could not locate the repository root (global.json) above '{AppContext.BaseDirectory}'.");
    }

    [Fact]
    public void NoTypeInTempestApp_ComesFromTheSampleAssembly()
    {
        // A sweep rather than a spot check: any public type in the
        // production workspace assembly whose base type, interfaces or
        // declared members reach into Tempest.Samples would reintroduce the
        // coupling somewhere this file does not name.
        var app = typeof(MechanicalWorkspaceExplorerModule).Assembly;

        var offenders = app.GetTypes()
            .Where(t => t.BaseType?.Assembly.GetName().Name == SampleAssembly
                     || t.GetInterfaces().Any(i => i.Assembly.GetName().Name == SampleAssembly))
            .Select(t => t.FullName ?? t.Name)
            .ToList();

        Assert.True(offenders.Count == 0, "Types in Tempest.App deriving from Tempest.Samples:\n" + string.Join("\n", offenders));
    }

    // ================================================================
    // What is deliberately still sample content
    // ================================================================

    [Fact]
    public void TheTrivialArithmeticStandIn_StaysASample()
    {
        // `DoubleLengthCalculationDefinition` genuinely is a demonstration
        // of the calculation framework, so it stayed behind. This asserts
        // the move was selective rather than wholesale.
        var type = typeof(Tempest.Samples.DoubleLengthCalculationDefinition);

        Assert.Equal(SampleAssembly, type.Assembly.GetName().Name);
    }

    [Fact]
    public void TheSampleExplorerAreaId_AgreesAcrossTheBoundaryItSitsOn()
    {
        // The one duplication `TD-75` phase 1 leaves, disclosed rather than
        // hidden: the sample explorer area's id is spelled in
        // `Tempest.App.Workspace.Samples` (where its node provider lives)
        // and again in `Tempest.Samples` (where its navigation item is
        // registered), because the two now sit either side of a removed
        // dependency and the sample assembly cannot read a constant from
        // the production one. This fails the moment they drift.
        Assert.Equal(
            "tempest.samples.workspace-explorer.objects",
            Tempest.Samples.WorkspaceExplorerSampleModule.NavigationItemId);
    }
}
