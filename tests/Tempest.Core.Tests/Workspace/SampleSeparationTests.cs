using Tempest.App.Composition;
using Tempest.App.Workspace;
using Tempest.App.Workspace.Calculations;
using Tempest.App.Workspace.Documents;
using Tempest.App.Workspace.Manufacturing;
using Tempest.App.Workspace.Mechanical;
using Tempest.App.Workspace.Requirements;
using Tempest.App.Workspace.Verification;
using Tempest.Core.Calculations;
using Tempest.Core.Configuration;
using Tempest.Core.Events;
using Tempest.Core.Modules;
using Tempest.Core.Navigation;
using Tempest.Core.Persistence;
using Tempest.Core.Tests.Plugins;
using Tempest.Core.Tests.Workspace.Samples;
using System.Xml.Linq;
using Tempest.Core.UnitsAndQuantities;
using Tempest.Samples;
using Tempest.Validation.FaultInjection;

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
    [InlineData("src/Validation/Tempest.Validation/Tempest.Validation.csproj")]
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
        //
        // It reads the declarations rather than the file's raw text, which
        // phase 2 corrected: the substring form also matched the assembly
        // name in a comment, so documenting the boundary in a project file
        // failed the test that guards it.
        var project = Path.Combine(FindRepositoryRoot(), relativeProjectPath.Replace('/', Path.DirectorySeparatorChar));

        Assert.True(File.Exists(project), $"'{project}' was not found.");

        Assert.DoesNotContain(SampleAssembly, DeclaredReferences(project), StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The assembly names <paramref name="projectFile"/> actually declares a
    /// dependency on — every <c>ProjectReference</c> and <c>Reference</c>
    /// <c>Include</c>, reduced to a bare assembly name. Comments and prose
    /// are ignored, which a raw substring search cannot do.
    /// </summary>
    private static IReadOnlyList<string> DeclaredReferences(string projectFile)
    {
        return XDocument.Load(projectFile)
            .Descendants()
            .Where(element => element.Name.LocalName is "ProjectReference" or "Reference")
            .Select(element => (string?)element.Attribute("Include"))
            .Where(include => !string.IsNullOrWhiteSpace(include))
            .Select(NormaliseToAssemblyName)
            .ToList();
    }

    /// <summary>
    /// Reduces one <c>Include</c> to a bare assembly name, whether it is a
    /// project path (<c>..\..\Samples\Tempest.Samples\Tempest.Samples.csproj</c>)
    /// or an assembly reference (<c>Tempest.Samples</c>, possibly with
    /// <c>, Version=...</c> trailing).
    /// </summary>
    private static string NormaliseToAssemblyName(string? include)
    {
        var value = include!.Replace('\\', Path.DirectorySeparatorChar).Trim();

        // An assembly reference's display name, not a path.
        value = value.Split(',')[0].Trim();

        var fileName = Path.GetFileName(value);

        // Strip only a project-file extension. Path.GetFileNameWithoutExtension
        // would strip ".Samples" from the bare assembly name "Tempest.Samples"
        // and leave "Tempest" — a mutation using <Reference Include=
        // "Tempest.Samples"/> survived this test until that was fixed.
        foreach (var extension in new[] { ".csproj", ".vbproj", ".fsproj" })
        {
            if (fileName.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
                return fileName[..^extension.Length];
        }

        return fileName;
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

    /// <summary>
    /// Enumerates every <c>*.csproj</c> under <paramref name="root"/>,
    /// pruning version-control/tooling and build-output directories as it
    /// walks rather than filtering their results out afterward — any
    /// directory whose name starts with <c>.</c> (<c>.git</c>, and this
    /// project's own git worktrees, each a full nested checkout of the
    /// repository under <c>.claude/worktrees/&lt;name&gt;</c> with its own
    /// <c>tests/**/*.csproj</c>), plus <c>bin</c> and <c>obj</c>.
    /// </summary>
    /// <remarks>
    /// Environment-dependent otherwise: <see cref="Directory.EnumerateFiles(string, string, SearchOption)"/>
    /// with <see cref="SearchOption.AllDirectories"/> has no way to prune a
    /// directory from the walk, so a checkout carrying extra worktrees
    /// (this repository's own tooling creates them under
    /// <c>.claude/worktrees</c>) would have this enumeration return project
    /// files that belong to a nested checkout, not to the tree under test —
    /// passing in CI, where no such directory exists, and failing in any
    /// checkout that has one.
    /// </remarks>
    private static IEnumerable<string> EnumerateProjectFiles(string root)
    {
        var pending = new Stack<string>();
        pending.Push(root);

        while (pending.Count > 0)
        {
            var directory = pending.Pop();

            foreach (var file in Directory.EnumerateFiles(directory, "*.csproj"))
                yield return file;

            foreach (var subdirectory in Directory.EnumerateDirectories(directory))
            {
                var name = Path.GetFileName(subdirectory);
                if (name.StartsWith(".", StringComparison.Ordinal) || name is "bin" or "obj")
                    continue;

                pending.Push(subdirectory);
            }
        }
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
    public async Task TheProductionCompositionRoot_RegistersNoSampleExplorerArea()
    {
        // Phase 1 left one disclosed duplication here: the sample explorer
        // area's id, spelled once in `Tempest.App` (where its node provider
        // then lived) and once in `Tempest.Samples` (where its navigation
        // item is registered). Phase 2 deletes the duplication rather than
        // guarding it, by removing the production side entirely — that
        // provider was keyed to an area no production run has contained
        // since phase 1 stopped the product loading the sample assembly.
        //
        // Absence is asserted through the product's own duplicate-registration
        // rule rather than through a new query method added for a test: a
        // second registration of an already-registered Kind throws
        // DuplicateWorkspaceRegistrationException, so if the composition root
        // still made these calls, these two would throw instead of succeeding.
        using var temp = new TempDirectory();
        var (host, manager) = EngineeringWorkspaceComposer.Build(
        [
            new MemoryConfigurationSource(
            [
                new KeyValuePair<string, string>(PersistenceStore.RootPathConfigurationKey, temp.Path),
            ]),
        ]);

        await using (host)
        {
            manager.RegisterExplorerArea(
                WorkspaceExplorerSampleModule.NavigationItemId,
                new SampleProjectExplorerNodeProvider(WorkspaceExplorerSampleModule.NavigationItemId));
            manager.RegisterView(
                SampleExplorerContent.ComponentKind,
                new SampleWorkspaceViewFactory(SampleExplorerContent.ComponentKind));
        }
    }

    // ================================================================
    // `TD-75` phase 2: the boundary, stated as a whole
    // ================================================================

    [Fact]
    public void NoProjectUnderSrc_ReferencesTheSampleAssembly_ExceptTheSampleAssemblyItself()
    {
        // The phase-1 test above names three projects, which only holds while
        // someone remembers to add the fourth. This sweeps every project under
        // src/ instead, so a new production project that references the
        // samples fails without anyone having to update a list.
        //
        // Phase 2's own subject is the one this catches: Tempest.Validation
        // referenced Tempest.Samples for a single navigation-id constant,
        // which meant the sample harness could not be deleted without
        // breaking the validation harness.
        var root = FindRepositoryRoot();
        var offenders = EnumerateProjectFiles(Path.Combine(root, "src"))
            .Where(path => !path.Contains(Path.Combine("src", "Samples"), StringComparison.Ordinal))
            .Where(path => DeclaredReferences(path).Contains(SampleAssembly, StringComparer.OrdinalIgnoreCase))
            .Select(path => Path.GetRelativePath(root, path))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            offenders.Count == 0,
            "Projects under src/ still declaring a dependency on the sample assembly:\n"
            + string.Join("\n", offenders));
    }

    [Fact]
    public void TheSampleAssembly_IsDeletable_NothingOutsideItDeclaresItAsADependency()
    {
        // What the boundary is actually for, stated as the property a reader
        // would want: the demo harness can be deleted from the repository and
        // the product still builds. Only the two test projects may hold it,
        // and they hold it to drive fixtures, not to ship anything.
        var root = FindRepositoryRoot();
        var holders = EnumerateProjectFiles(root)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(path => !path.Contains(Path.Combine("src", "Samples"), StringComparison.Ordinal))
            .Where(path => DeclaredReferences(path).Contains(SampleAssembly, StringComparer.OrdinalIgnoreCase))
            .Select(path => Path.GetRelativePath(root, path).Replace(Path.DirectorySeparatorChar, '/'))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();

        Assert.Equal(
            [
                "tests/Tempest.Core.Tests/Tempest.Core.Tests.csproj",
                "tests/Tempest.Desktop.Tests/Tempest.Desktop.Tests.csproj",
            ],
            holders);
    }

    [Fact]
    public void TempestApp_DeclaresNoSampleContentOfItsOwn()
    {
        // Phase 1 removed the reference; sample-supporting code stayed behind
        // in Tempest.App.Workspace.Samples — a fictional Longeron/Frame/
        // Bracket tree, a never-editable view and its factory, all shipped in
        // the production assembly. Phase 2 moved them to Tempest.Core.Tests,
        // which is the only thing that ever drove them.
        //
        // Namespace and name are both checked because either alone is easy to
        // slip past: a `Sample*` type in a discipline namespace, or an
        // innocuously-named type in a `.Samples` namespace.
        var offenders = typeof(EngineeringWorkspaceComposer).Assembly
            .GetTypes()
            .Where(type =>
                type.Namespace?.EndsWith(".Samples", StringComparison.Ordinal) == true
                || type.Name.StartsWith("Sample", StringComparison.Ordinal))
            .Select(type => type.FullName!)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            offenders.Count == 0,
            "Tempest.App still declares sample content:\n" + string.Join("\n", offenders));
    }

    [Fact]
    public async Task TheFaultInjectionHarness_CollidesWithWhateverIsRegistered_NotWithASampleConstant()
    {
        // The mechanism that let phase 2 cut the Validation edge, asserted
        // directly: DuplicateNavigationModule reads the live navigation
        // registrations instead of naming one module's Id constant, so it
        // fails the module it is paired with whatever that module is. Paired
        // here with a discipline module rather than a sample one — the exact
        // pairing that was impossible while it referenced Tempest.Samples.
        var navigationProvider = new Tempest.Core.Navigation.NavigationService(new EventBus());
        navigationProvider.Register(new NavigationItem("some.unrelated.area", "Unrelated"));

        var duplicate = new DuplicateNavigationModule(navigationProvider);

        var failure = await Assert.ThrowsAsync<DuplicateNavigationItemException>(
            () => duplicate.InitialiseAsync(CancellationToken.None));

        Assert.Contains("some.unrelated.area", failure.Message, StringComparison.Ordinal);
        Assert.Single(navigationProvider.Items);
    }
}
