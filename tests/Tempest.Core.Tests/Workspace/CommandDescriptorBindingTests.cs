using System.Text.RegularExpressions;
using Tempest.App.Composition;
using Tempest.App.Workspace;
using Tempest.App.Workspace.Calculations;
using Tempest.App.Workspace.Documents;
using Tempest.App.Workspace.Manufacturing;
using Tempest.App.Workspace.Mechanical;
using Tempest.App.Workspace.Requirements;
using Tempest.App.Workspace.Verification;
using Tempest.Core.Commands;
using Tempest.Core.Configuration;
using Tempest.Core.Persistence;
using Tempest.Core.Requirements;
using Tempest.Core.Runtime;
using Tempest.Core.Tests.Plugins;
using Tempest.Core.Tests.Templates;
using Tempest.Core.Verification;

namespace Tempest.Core.Tests.Workspace;

/// <summary>
/// TD-77 Stage 3 — descriptor binding, asserted against the real
/// composition root.
/// </summary>
/// <remarks>
/// <para>
/// Every assertion below reads the registry
/// <see cref="EngineeringWorkspaceComposer.RegisterEngineeringDisciplines"/>
/// itself populates — the identical call, in the identical order, that the
/// console entry point and the Desktop shell both make. Nothing is
/// re-registered for the test's own convenience, so a binding that exists
/// only in a test fixture cannot pass here.
/// </para>
/// <para>
/// The expected Kind sets, parameter names, defaults and macro-eligible Ids
/// are written out as literals rather than read back from the production
/// constants they were registered from. Asserting a constant against itself
/// proves nothing; these are the audited values, and they have to be
/// changed here, deliberately, for a production change to pass.
/// </para>
/// </remarks>
[Collection("Console output capture")]
public sealed class CommandDescriptorBindingTests : IAsyncLifetime
{
    // ==================================================================
    // The audited classification, as literals
    // ==================================================================

    private static readonly IReadOnlyList<string> Disciplines =
        ["Calculations", "Documents", "Manufacturing", "Mechanical", "Requirements", "Verification"];

    /// <summary>U1 — an object picker this platform does not have (FCR-0073).</summary>
    private static readonly IReadOnlyList<string> ObjectPickerUnavailable =
    [
        "calculations.move", "documents.move", "manufacturing.move", "mechanical.move",
        "verification.move", "requirements.move", "requirements.move-group",
        "calculations.copy", "documents.copy", "manufacturing.copy", "mechanical.copy",
        "verification.copy", "requirements.link", "requirements.add-to-collection",
        "mechanical.compare-baselines",
    ];

    /// <summary>U2 — structured or binary input the platform's text-only prompt cannot collect.</summary>
    private static readonly IReadOnlyList<string> StructuredInputUnavailable =
        ["calculations.execute", "calculations.recalculate", "documents.attach"];

    /// <summary>
    /// The thirteen status transitions plus <c>mechanical.validate-configuration</c>
    /// — every command that can run unattended in a macro, and no other
    /// (ADR-0098).
    /// </summary>
    private static readonly IReadOnlyList<string> MacroSafe =
    [
        "calculations.lock", "calculations.unlock", "calculations.request-review",
        "calculations.approve", "calculations.archive",
        "documents.request-review", "documents.approve", "documents.release",
        "manufacturing.release", "manufacturing.archive",
        "verification.request-review", "verification.approve", "verification.archive",
        "mechanical.validate-configuration",
    ];

    private static readonly IReadOnlyList<string> CalculationKinds = ["Calculation", "CalculationSet"];
    private static readonly IReadOnlyList<string> DocumentKinds = ["Document", "Drawing", "CadModel"];
    private static readonly IReadOnlyList<string> ManufacturingKinds = ["ManufacturingOperation", "WorkInstruction", "Inspection"];
    private static readonly IReadOnlyList<string> MechanicalKinds =
        ["Project", "Assembly", "SubAssembly", "Part", "Component", "Configuration", "Baseline", "Release"];
    private static readonly IReadOnlyList<string> BomLineKinds = ["Assembly", "SubAssembly", "Part", "Component"];
    private static readonly IReadOnlyList<string> BaselineKinds = ["Baseline", "Release"];
    private static readonly IReadOnlyList<string> RequirementKinds = ["Requirement"];
    private static readonly IReadOnlyList<string> VerificationKinds = ["VerificationActivity"];

    // ==================================================================
    // The real composition root, booted once for the whole class
    // ==================================================================

    private TempDirectory _temp = null!;
    private ITempestHost _host = null!;
    private WorkspaceManager _manager = null!;
    private ICommandRegistry _registry = null!;

    public async Task InitializeAsync()
    {
        _temp = new TempDirectory();

        // An explicit module list, exactly as every other Workspace
        // integration test in this assembly builds one — never
        // EngineeringWorkspaceComposer.Build's own reflective discovery,
        // which would sweep up this assembly's deliberately-malformed
        // module fixtures. The registration under test is still the real
        // one: RegisterEngineeringDisciplines below is the identical call
        // the console entry point and the Desktop shell both make.
        _host = new TempestHostBuilder(
        [
            typeof(MechanicalWorkspaceExplorerModule),
            typeof(RequirementsWorkspaceExplorerModule),
            typeof(CalculationsWorkspaceExplorerModule),
            typeof(DocumentsWorkspaceExplorerModule),
            typeof(VerificationWorkspaceExplorerModule),
            typeof(ManufacturingWorkspaceExplorerModule),
        ])
            .AddConfigurationSource(new MemoryConfigurationSource(
            [
                new KeyValuePair<string, string>(PersistenceStore.RootPathConfigurationKey, _temp.Path),
            ]))
            .Build();
        _manager = new WorkspaceManager(_host);

        var originalOut = Console.Out;
        try
        {
            Console.SetOut(new StringWriter());
            await _manager.StartAsync();
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        EngineeringWorkspaceComposer.RegisterEngineeringDisciplines(_manager, _host);
        _registry = (ICommandRegistry)_host.Services!.GetService(typeof(ICommandRegistry));
    }

    public async Task DisposeAsync()
    {
        await _manager.ShutdownAsync();
        await _host.DisposeAsync();
        _temp.Dispose();
    }

    private IReadOnlyList<CommandDescriptor> ProductionDescriptors =>
        _registry.Items.Where(d => Disciplines.Contains(d.Category, StringComparer.Ordinal)).ToList();

    private CommandDescriptor Descriptor(string id) =>
        _registry.Items.SingleOrDefault(d => d.Id == id)
        ?? throw new InvalidOperationException($"No descriptor '{id}' is registered.");

    private CommandBinding Binding(string id) =>
        Descriptor(id).Binding ?? throw new InvalidOperationException($"'{id}' declares no binding.");

    private static CommandParameter Parameter(CommandBinding binding, string name) =>
        binding.Parameters.SingleOrDefault(p => p.Name == name)
        ?? throw new InvalidOperationException($"No parameter '{name}' is declared.");

    private static CommandContext One(string kind) => CommandContext.For(Guid.NewGuid(), kind);

    private static CommandParameterPrompt Answering(params (string Name, string Value)[] answers) =>
        (_, _, _, _) => Task.FromResult<IReadOnlyDictionary<string, string>?>(
            answers.ToDictionary(a => a.Name, a => a.Value, StringComparer.Ordinal));

    // ==================================================================
    // 1. A representative command from each discipline has a binding
    // ==================================================================

    [Theory]
    [InlineData("calculations.rename", "Calculations")]
    [InlineData("documents.approve", "Documents")]
    [InlineData("manufacturing.release", "Manufacturing")]
    [InlineData("mechanical.set-bom-line", "Mechanical")]
    [InlineData("requirements.set-status", "Requirements")]
    [InlineData("verification.record-result", "Verification")]
    public void RepresentativeCommandFromEachDiscipline_HasAnInvocableBinding(string id, string category)
    {
        var descriptor = Descriptor(id);

        Assert.Equal(category, descriptor.Category);
        Assert.NotNull(descriptor.Binding);
        Assert.True(descriptor.Binding!.IsInvocable);
        Assert.Null(descriptor.Binding.UnavailableReason);
    }

    // ==================================================================
    // 2. Every bindable audited descriptor has a binding
    // ==================================================================

    [Fact]
    public void EveryBindableAuditedDescriptor_HasAnInvocableBinding()
    {
        var unavailable = ObjectPickerUnavailable.Concat(StructuredInputUnavailable).ToHashSet(StringComparer.Ordinal);
        var bindable = ProductionDescriptors.Where(d => !unavailable.Contains(d.Id)).ToList();

        // The canonical reconciliation, and the one place in the suite where
        // these numbers are asserted (`WP-F`, `F-11`): 74 production
        // discipline commands, 18 of them explicitly unavailable, so the
        // remaining 56 must every one be invocable. The arithmetic is the
        // protection — it is what stops a nineteenth unavailable command
        // hiding inside the bindable set — so all three terms are stated,
        // not two of them with the third left in a comment.
        Assert.Equal(74, ProductionDescriptors.Count);
        Assert.Equal(18, unavailable.Count);
        Assert.Equal(56, bindable.Count);
        Assert.Equal(ProductionDescriptors.Count, unavailable.Count + bindable.Count);

        var notBound = bindable.Where(d => d.Binding is not { IsInvocable: true }).Select(d => d.Id).ToList();
        Assert.Empty(notBound);
    }

    // ==================================================================
    // 3. Every U1/U2 descriptor has its own specific unavailable reason
    // ==================================================================

    [Fact]
    public void EveryObjectPickerDescriptor_DeclaresThatAnObjectMustBeChosen()
    {
        Assert.Equal(15, ObjectPickerUnavailable.Count);

        foreach (var id in ObjectPickerUnavailable)
        {
            var binding = Binding(id);

            Assert.False(binding.IsInvocable);
            var reason = Assert.IsType<string>(binding.UnavailableReason);

            // Names the missing capability, not merely the absence of one.
            Assert.Contains("chosen from the object tree", reason, StringComparison.Ordinal);
            Assert.Contains("object picker", reason, StringComparison.Ordinal);
            Assert.Contains("FCR-0073", reason, StringComparison.Ordinal);

            // And says what specifically must be chosen, not just "an object".
            Assert.Matches(new Regex("needs (a|the) [^,]+"), reason);
        }
    }

    [Fact]
    public void EveryStructuredInputDescriptor_DeclaresWhatItCannotCollect()
    {
        Assert.Equal(3, StructuredInputUnavailable.Count);

        foreach (var id in StructuredInputUnavailable)
        {
            var binding = Binding(id);

            Assert.False(binding.IsInvocable);
            Assert.Contains("single-line text only", binding.UnavailableReason!, StringComparison.Ordinal);
        }

        // Each names its own genuinely different missing input, rather than
        // sharing one reason between three unrelated commands.
        Assert.Contains("structured input document", Binding("calculations.execute").UnavailableReason!, StringComparison.Ordinal);
        Assert.Contains("supplied as JSON", Binding("calculations.execute").UnavailableReason!, StringComparison.Ordinal);
        Assert.Contains("fresh values", Binding("calculations.recalculate").UnavailableReason!, StringComparison.Ordinal);
        Assert.Contains("file picker", Binding("documents.attach").UnavailableReason!, StringComparison.Ordinal);
        Assert.Contains("bytes", Binding("documents.attach").UnavailableReason!, StringComparison.Ordinal);

        Assert.Equal(
            3,
            StructuredInputUnavailable.Select(id => Binding(id).UnavailableReason).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public async Task AnUnavailableDescriptor_ReportsItsOwnReason_NotTheGenericOne()
    {
        // The Ribbon's own catch-all wording, which ADR-0070 requires a
        // command to replace with a reason of its own.
        const string Generic = "isn't available yet";

        foreach (var id in ObjectPickerUnavailable.Concat(StructuredInputUnavailable))
        {
            var availability = _registry.Evaluate(id, One("Requirement"));
            var invocation = await _registry.InvokeAsync(id, One("Requirement"));

            Assert.False(availability.IsAvailable);
            Assert.DoesNotContain(Generic, availability.Reason!, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(CommandOutcome.Unavailable, invocation.Outcome);
            Assert.Equal(Binding(id).UnavailableReason, invocation.Reason);
        }
    }

    // ==================================================================
    // 4. No descriptor is left in the old ambiguous state
    // ==================================================================

    [Fact]
    public void NoProductionDescriptor_IsLeftWithNeitherABindingNorAReason()
    {
        var ambiguous = ProductionDescriptors
            .Where(d => d.Binding is null)
            .Select(d => d.Id)
            .ToList();

        Assert.Empty(ambiguous);

        // Every one is exactly one of the two, never both and never neither.
        foreach (var descriptor in ProductionDescriptors)
        {
            var binding = descriptor.Binding!;
            Assert.True(binding.IsInvocable ^ binding.UnavailableReason is not null);
        }
    }

    /// <summary>
    /// The structural guard: a future production descriptor cannot be added
    /// to one of the six discipline registrations without either a binding
    /// or a stated reason, because this reads the registration sources
    /// themselves and counts what they declare.
    /// </summary>
    [Fact]
    public void EveryDescriptorRegistrationInSource_DeclaresABinding()
    {
        var offenders = new List<string>();
        var declaredIds = new List<string>();

        foreach (var (file, source) in RegistrationSources())
        {
            foreach (var registration in DescriptorRegistrations(source))
            {
                var id = Regex.Match(registration, @"id: ""(?<id>[^""]+)""").Groups["id"].Value;
                declaredIds.Add(id);

                if (!registration.Contains("Binding =", StringComparison.Ordinal))
                    offenders.Add($"{file}: '{id}' registers no Binding.");
            }
        }

        Assert.Empty(offenders);

        // What the source declares and what the registry ends up holding must
        // be the same set (`WP-F`, `F-11`). This asserted `74 == declared`
        // until then, which could not tell a missed registration from an
        // extra one, and restated the canonical count a third time. The
        // difference below names the Id.
        var registeredIds = ProductionDescriptors.Select(d => d.Id).ToHashSet(StringComparer.Ordinal);
        var declaredOnly = declaredIds.Except(registeredIds, StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal).ToList();
        var registeredOnly = registeredIds.Except(declaredIds, StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal).ToList();

        Assert.True(
            declaredOnly.Count == 0 && registeredOnly.Count == 0,
            $"The six registration sources and the live registry disagree.\n"
            + $"  Declared in source but not registered: {string.Join(", ", declaredOnly)}\n"
            + $"  Registered but not declared in source: {string.Join(", ", registeredOnly)}");

        // No source file registers the same Id twice.
        Assert.Equal(declaredIds.Count, declaredIds.Distinct(StringComparer.Ordinal).Count());
    }

    private static IEnumerable<(string File, string Source)> RegistrationSources()
    {
        foreach (var (folder, file) in new[]
                 {
                     ("Calculations", "CalculationsWorkspaceRegistration.cs"),
                     ("Documents", "DocumentsWorkspaceRegistration.cs"),
                     ("Manufacturing", "ManufacturingWorkspaceRegistration.cs"),
                     ("Mechanical", "MechanicalWorkspaceRegistration.cs"),
                     ("Requirements", "RequirementsWorkspaceRegistration.cs"),
                     ("Verification", "VerificationWorkspaceRegistration.cs"),
                 })
        {
            yield return (
                file,
                File.ReadAllText(Path.Combine(
                    RepositoryPaths.RepositoryRoot, "src", "Tempest.App", "Workspace", folder, file)));
        }
    }

    /// <summary>
    /// Each <c>RegisterDescriptor</c> statement in <paramref name="source"/>,
    /// from its own opening line to the line that closes it — <c>"});"</c>
    /// for a descriptor carrying an object initialiser, <c>"));"</c> for one
    /// that carries nothing. A descriptor added without a binding closes the
    /// second way, and is what this reports.
    /// </summary>
    private static IEnumerable<string> DescriptorRegistrations(string source)
    {
        const string Opening = "commandRegistry.RegisterDescriptor(new CommandDescriptor(";
        var lines = source.Split('\n');

        for (var i = 0; i < lines.Length; i++)
        {
            if (!lines[i].Contains(Opening, StringComparison.Ordinal))
                continue;

            var statement = new List<string> { lines[i] };

            while (++i < lines.Length)
            {
                statement.Add(lines[i]);
                var trimmed = lines[i].TrimEnd();

                if (trimmed.EndsWith("});", StringComparison.Ordinal)
                    || trimmed.EndsWith("));", StringComparison.Ordinal))
                {
                    break;
                }
            }

            yield return string.Join("\n", statement);
        }
    }

    // ==================================================================
    // 5. Creation bindings preserve their defaults and their validation
    // ==================================================================

    [Theory]
    [InlineData("calculations.create", "Calculation")]
    [InlineData("documents.create", "Document")]
    [InlineData("manufacturing.create", "ManufacturingOperation")]
    [InlineData("mechanical.create", "Part")]
    public void CreateBindings_DefaultToTheKindTheRibbonAlreadyDefaultsTo(string id, string expectedDefaultKind)
    {
        var kind = Parameter(Binding(id), "kind");

        Assert.Equal(expectedDefaultKind, kind.DefaultValue);
        Assert.Contains(expectedDefaultKind, kind.AllowedValues!);
    }

    [Theory]
    [InlineData("calculations.create", "displayName")]
    [InlineData("documents.create", "displayName")]
    [InlineData("manufacturing.create", "displayName")]
    [InlineData("mechanical.create", "displayName")]
    [InlineData("verification.create", "displayName")]
    [InlineData("calculations.rename", "newDisplayName")]
    [InlineData("documents.rename", "newDisplayName")]
    [InlineData("manufacturing.rename", "newDisplayName")]
    [InlineData("mechanical.rename", "newDisplayName")]
    [InlineData("verification.rename", "newDisplayName")]
    public void NameParameters_KeepTheTwoHundredCharacterLimitAndTheNonBlankRule(string id, string parameterName)
    {
        var name = Parameter(Binding(id), parameterName);

        Assert.Null(name.Check("A reasonable name"));
        Assert.Null(name.Check(new string('x', 200)));
        Assert.NotNull(name.Check(new string('x', 201)));
        Assert.Contains("200 characters max", name.Check(new string('x', 201))!, StringComparison.Ordinal);
        Assert.NotNull(name.Check(""));
        Assert.NotNull(name.Check("   "));
    }

    [Theory]
    [InlineData("requirements.create", "identifier")]
    [InlineData("requirements.create", "statement")]
    [InlineData("requirements.create-group", "name")]
    [InlineData("requirements.create-collection", "name")]
    [InlineData("requirements.duplicate", "newIdentifier")]
    [InlineData("requirements.revise", "newStatement")]
    public void RequirementsTextParameters_KeepTheirNonBlankRule(string id, string parameterName)
    {
        var parameter = Parameter(Binding(id), parameterName);

        Assert.Null(parameter.Check("REQ-001"));
        Assert.NotNull(parameter.Check(""));
        Assert.NotNull(parameter.Check("   "));
    }

    [Fact]
    public void VerificationCreate_KeepsTheExistingDefaultMethod_AndTakesItsSubjectFromTheSelection()
    {
        var binding = Binding("verification.create");
        var method = Parameter(binding, "method");

        Assert.Equal("Inspection", method.DefaultValue);
        Assert.Null(method.Check("Inspection"));
        Assert.NotNull(method.Check(""));

        // The subject is the selected object, never a fabricated Id — which
        // is why this Create, alone among the six, needs a selection.
        Assert.True(binding.Requires.HasFlag(CommandContextRequirement.SelectedObject));

        var subjectId = Guid.NewGuid();
        var command = (CreateVerificationActivityCommand)binding.Build(
            CommandContext.For(subjectId, "Part"),
            new Dictionary<string, string>(StringComparer.Ordinal) { ["displayName"] = "Vibration check", ["method"] = "Test" });

        Assert.Equal(subjectId, command.SubjectId);
        Assert.Equal("Vibration check", command.DisplayName);
        Assert.Equal("Test", command.Method);
    }

    [Fact]
    public async Task CreateBinding_ConstructsAndDispatchesARealCommand_ThroughTheRegistry()
    {
        var invocation = await _registry.InvokeAsync(
            "requirements.create",
            CommandContext.Empty,
            Answering(("identifier", "REQ-STAGE3"), ("statement", "The binding shall construct a real command.")));

        Assert.Equal(CommandOutcome.Executed, invocation.Outcome);
        Assert.True(invocation.Result!.Succeeded, invocation.Result.Message);

        var service = (IRequirementsService)_host.Services!.GetService(typeof(IRequirementsService));
        var created = await service.ListAsync();
        Assert.Contains(created, r => r.Identifier == "REQ-STAGE3");
    }

    [Fact]
    public async Task ACreateBindingsOwnValidation_IsEnforcedBeforeAnythingIsBuilt()
    {
        var invocation = await _registry.InvokeAsync(
            "requirements.create",
            CommandContext.Empty,
            Answering(("identifier", "   "), ("statement", "Blank identifiers must not reach the constructor.")));

        Assert.Equal(CommandOutcome.Unavailable, invocation.Outcome);
        Assert.Contains("required", invocation.Reason!, StringComparison.OrdinalIgnoreCase);
    }

    // ==================================================================
    // 6. Enum bindings expose exactly the audited AllowedValues
    // ==================================================================

    [Fact]
    public void EnumParameters_ExposeExactlyTheirOwnEnumsNames()
    {
        Assert.Equal(
            ["Draft", "Reviewed", "Approved", "Allocated", "Verified", "Satisfied", "Obsolete"],
            Parameter(Binding("requirements.set-status"), "status").AllowedValues);

        Assert.Equal(
            ["Low", "Medium", "High", "Critical"],
            Parameter(Binding("requirements.set-priority"), "priority").AllowedValues);

        Assert.Equal(
            ["Pass", "Fail", "Conditional"],
            Parameter(Binding("verification.record-result"), "outcome").AllowedValues);

        Assert.Equal(
            ["Pass", "Fail", "Conditional"],
            Parameter(Binding("manufacturing.record-inspection-result"), "outcome").AllowedValues);

        // The bulk commands share their singular counterparts' own sets.
        Assert.Equal(
            Parameter(Binding("requirements.set-status"), "status").AllowedValues,
            Parameter(Binding("requirements.bulk-set-status"), "status").AllowedValues);
        Assert.Equal(
            Parameter(Binding("requirements.set-priority"), "priority").AllowedValues,
            Parameter(Binding("requirements.bulk-set-priority"), "priority").AllowedValues);
    }

    [Fact]
    public void AnEnumParameter_RefusesAValueOutsideItsOwnSet()
    {
        var status = Parameter(Binding("requirements.set-status"), "status");

        Assert.Null(status.Check("Approved"));
        Assert.Null(status.Check("approved"));   // matched case-insensitively, as the Ribbon's own prompt already does
        Assert.NotNull(status.Check("Rejected"));
    }

    [Fact]
    public void KindChoices_ExposeExactlyTheirDisciplinesOwnSupportedKinds()
    {
        Assert.Equal(CalculationKinds, Parameter(Binding("calculations.create"), "kind").AllowedValues);
        Assert.Equal(DocumentKinds, Parameter(Binding("documents.create"), "kind").AllowedValues);
        Assert.Equal(ManufacturingKinds, Parameter(Binding("manufacturing.create"), "kind").AllowedValues);
        Assert.Equal(MechanicalKinds, Parameter(Binding("mechanical.create"), "kind").AllowedValues);
    }

    // ==================================================================
    // 7. Kind restrictions match the audited SupportedKinds
    // ==================================================================

    [Theory]
    // Calculations: the two real Domain-backed Kinds — never the synthetic
    // "CalculationTemplate" Kind no Calculation command can act on.
    [InlineData("calculations.rename")]
    [InlineData("calculations.edit")]
    [InlineData("calculations.delete")]
    [InlineData("calculations.duplicate")]
    [InlineData("calculations.lock")]
    [InlineData("calculations.unlock")]
    [InlineData("calculations.request-review")]
    [InlineData("calculations.approve")]
    [InlineData("calculations.archive")]
    public void CalculationBindings_ApplyToTheTwoRealCalculationKinds(string id) =>
        Assert.Equal(CalculationKinds, Binding(id).AppliesToKinds);

    [Theory]
    [InlineData("documents.rename")]
    [InlineData("documents.edit")]
    [InlineData("documents.delete")]
    [InlineData("documents.duplicate")]
    [InlineData("documents.request-review")]
    [InlineData("documents.approve")]
    [InlineData("documents.release")]
    public void DocumentBindings_ApplyToTheThreeDocumentKinds(string id) =>
        Assert.Equal(DocumentKinds, Binding(id).AppliesToKinds);

    [Theory]
    [InlineData("manufacturing.rename")]
    [InlineData("manufacturing.edit")]
    [InlineData("manufacturing.delete")]
    [InlineData("manufacturing.duplicate")]
    [InlineData("manufacturing.release")]
    [InlineData("manufacturing.archive")]
    public void ManufacturingBindings_ApplyToTheThreeManufacturingKinds(string id) =>
        Assert.Equal(ManufacturingKinds, Binding(id).AppliesToKinds);

    [Theory]
    [InlineData("mechanical.rename")]
    [InlineData("mechanical.edit")]
    [InlineData("mechanical.delete")]
    [InlineData("mechanical.duplicate")]
    public void MechanicalBindings_ApplyToTheEightMechanicalKinds(string id) =>
        Assert.Equal(MechanicalKinds, Binding(id).AppliesToKinds);

    [Fact]
    public void NarrowerMechanicalBindings_KeepTheirOwnNarrowerScope()
    {
        // A BOM line belongs to the four Kinds whose contracts declare
        // IHasBomLine, and member consistency to the two that are Baselines.
        Assert.Equal(BomLineKinds, Binding("mechanical.set-bom-line").AppliesToKinds);
        Assert.Equal(BaselineKinds, Binding("mechanical.validate-configuration").AppliesToKinds);
    }

    [Theory]
    [InlineData("requirements.revise")]
    [InlineData("requirements.set-status")]
    [InlineData("requirements.set-owner")]
    [InlineData("requirements.set-priority")]
    [InlineData("requirements.delete")]
    [InlineData("requirements.duplicate")]
    [InlineData("requirements.bulk-set-status")]
    [InlineData("requirements.bulk-set-owner")]
    [InlineData("requirements.bulk-set-priority")]
    public void RequirementBindings_ApplyToTheRequirementKindAlone(string id) =>
        Assert.Equal(RequirementKinds, Binding(id).AppliesToKinds);

    [Fact]
    public void RequirementContainerBindings_ApplyToTheirOwnContainerKind()
    {
        Assert.Equal(["RequirementGroup"], Binding("requirements.delete-group").AppliesToKinds);
        Assert.Equal(["RequirementCollection"], Binding("requirements.delete-collection").AppliesToKinds);
    }

    [Theory]
    [InlineData("verification.rename")]
    [InlineData("verification.edit")]
    [InlineData("verification.delete")]
    [InlineData("verification.duplicate")]
    [InlineData("verification.record-result")]
    [InlineData("verification.request-review")]
    [InlineData("verification.approve")]
    [InlineData("verification.archive")]
    public void VerificationBindings_ApplyToTheVerificationActivityKind(string id) =>
        Assert.Equal(VerificationKinds, Binding(id).AppliesToKinds);

    [Fact]
    public void CreationBindings_DeclareNoKindRestriction_BecauseTheyActOnNothingSelected()
    {
        foreach (var id in new[]
                 {
                     "calculations.create", "documents.create", "manufacturing.create", "mechanical.create",
                     "requirements.create", "requirements.create-group", "requirements.create-collection",
                     "verification.create",
                 })
        {
            Assert.Null(Binding(id).AppliesToKinds);
        }
    }

    [Fact]
    public void AKindRestriction_IsEnforcedByEvaluate_WithAReasonNamingTheKind()
    {
        var availability = _registry.Evaluate("calculations.rename", One("CalculationTemplate"));

        Assert.False(availability.IsAvailable);
        Assert.Contains("CalculationTemplate", availability.Reason!, StringComparison.Ordinal);
        Assert.True(_registry.Evaluate("calculations.rename", One("Calculation")).IsAvailable);
    }

    // ==================================================================
    // 8. Multi-selection bindings preserve selection order
    // ==================================================================

    [Fact]
    public void BulkBindings_AcceptAMultiSelection_WhileSingleTargetBindingsDoNot()
    {
        var three = new CommandContext(
        [
            new CommandContextObject(Guid.NewGuid(), "Requirement"),
            new CommandContextObject(Guid.NewGuid(), "Requirement"),
            new CommandContextObject(Guid.NewGuid(), "Requirement"),
        ]);

        Assert.True(_registry.Evaluate("requirements.bulk-set-status", three).IsAvailable);
        Assert.True(_registry.Evaluate("requirements.bulk-set-owner", three).IsAvailable);
        Assert.True(_registry.Evaluate("requirements.bulk-set-priority", three).IsAvailable);

        // Refused rather than silently applied to the first of the three.
        Assert.False(_registry.Evaluate("requirements.set-status", three).IsAvailable);
        Assert.Contains("one object at a time", _registry.Evaluate("requirements.set-status", three).Reason!, StringComparison.Ordinal);
    }

    [Fact]
    public void BulkBindings_PassTheWholeSelection_InSelectionOrder()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var third = Guid.NewGuid();
        var context = new CommandContext(
        [
            new CommandContextObject(first, "Requirement"),
            new CommandContextObject(second, "Requirement"),
            new CommandContextObject(third, "Requirement"),
        ]);

        var status = (BulkSetRequirementStatusCommand)Binding("requirements.bulk-set-status").Build(
            context, new Dictionary<string, string>(StringComparer.Ordinal) { ["status"] = "Approved" });
        var owner = (BulkSetRequirementOwnerCommand)Binding("requirements.bulk-set-owner").Build(
            context, new Dictionary<string, string>(StringComparer.Ordinal) { ["owner"] = "A. Engineer" });
        var priority = (BulkSetRequirementPriorityCommand)Binding("requirements.bulk-set-priority").Build(
            context, new Dictionary<string, string>(StringComparer.Ordinal) { ["priority"] = "High" });

        Assert.Equal([first, second, third], status.RequirementIds);
        Assert.Equal([first, second, third], owner.RequirementIds);
        Assert.Equal([first, second, third], priority.RequirementIds);

        Assert.Equal(RequirementStatus.Approved, status.Status);
        Assert.Equal("A. Engineer", owner.Owner);
        Assert.Equal(RequirementPriority.High, priority.Priority);
    }

    [Fact]
    public void ASingleTargetBinding_ActsOnThePrimarySelection()
    {
        var primary = Guid.NewGuid();
        var context = new CommandContext(
            [new CommandContextObject(primary, "Requirement")]);

        var command = (SetRequirementOwnerCommand)Binding("requirements.set-owner").Build(
            context, new Dictionary<string, string>(StringComparer.Ordinal) { ["owner"] = "A. Engineer" });

        Assert.Equal(primary, command.TargetObjectId);
    }

    // ==================================================================
    // 9. mechanical.set-bom-line rejects invalid decimal input
    // ==================================================================

    [Fact]
    public void SetBomLine_ValidatesQuantityAsADecimal_BeforeAnythingIsBuilt()
    {
        var quantity = Parameter(Binding("mechanical.set-bom-line"), "quantity");

        Assert.Null(quantity.Check("1"));
        Assert.Null(quantity.Check("2.5"));
        Assert.Null(quantity.Check("-3"));
        Assert.NotNull(quantity.Check("two"));
        Assert.NotNull(quantity.Check(""));
        Assert.NotNull(quantity.Check("   "));
        Assert.NotNull(quantity.Check("4 off"));
        Assert.NotNull(quantity.Check("1.2.3"));
        Assert.Contains("must be a number", quantity.Check("two")!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SetBomLine_RefusesAnInvalidQuantity_WithoutReachingItsHandler()
    {
        var invocation = await _registry.InvokeAsync(
            "mechanical.set-bom-line",
            One("Part"),
            Answering(
                ("quantity", "not a number"), ("unitOfMeasure", ""), ("findNumber", ""),
                ("itemNumber", ""), ("referenceDesignator", "")));

        Assert.Equal(CommandOutcome.Unavailable, invocation.Outcome);
        Assert.Contains("must be a number", invocation.Reason!, StringComparison.Ordinal);
        Assert.Null(invocation.Result);
    }

    [Fact]
    public void SetBomLine_KeepsItsOptionalStringsOptional()
    {
        var targetId = Guid.NewGuid();
        var command = (SetBomLineCommand)Binding("mechanical.set-bom-line").Build(
            CommandContext.For(targetId, "Part"),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["quantity"] = "4.5",
                ["unitOfMeasure"] = "",
                ["findNumber"] = "   ",
                ["itemNumber"] = "010",
                ["referenceDesignator"] = "R1",
            });

        Assert.Equal(targetId, command.TargetObjectId);
        Assert.Equal(4.5m, command.Quantity);
        Assert.Null(command.UnitOfMeasure);
        Assert.Null(command.FindNumber);
        Assert.Equal("010", command.ItemNumber);
        Assert.Equal("R1", command.ReferenceDesignator);
    }

    // ==================================================================
    // 10. Duplicate bindings carry confirmation metadata
    // ==================================================================

    [Theory]
    [InlineData("calculations.duplicate")]
    [InlineData("documents.duplicate")]
    [InlineData("manufacturing.duplicate")]
    [InlineData("mechanical.duplicate")]
    [InlineData("verification.duplicate")]
    [InlineData("requirements.duplicate")]
    public void EveryDuplicateBinding_RequiresConfirmation(string id)
    {
        var binding = Binding(id);

        Assert.NotNull(binding.ConfirmationMessage);
        Assert.Contains("duplicate", binding.ConfirmationMessage!, StringComparison.OrdinalIgnoreCase);
        Assert.True(binding.RequiresPrompt);
    }

    [Theory]
    [InlineData("calculations.delete")]
    [InlineData("documents.delete")]
    [InlineData("manufacturing.delete")]
    [InlineData("mechanical.delete")]
    [InlineData("verification.delete")]
    [InlineData("requirements.delete")]
    [InlineData("requirements.delete-group")]
    [InlineData("requirements.delete-collection")]
    public void EveryDeleteBinding_RequiresConfirmation(string id)
    {
        var binding = Binding(id);

        Assert.NotNull(binding.ConfirmationMessage);
        Assert.Contains("cannot be undone", binding.ConfirmationMessage!, StringComparison.Ordinal);
        Assert.True(binding.RequiresPrompt);
    }

    [Fact]
    public async Task AConfirmationGatedCommand_IsNeverInvokedWithoutOneBeingAsked()
    {
        // No prompt at all: never a silent no-op, and never a silent run.
        var unattended = await _registry.InvokeAsync("mechanical.duplicate", One("Part"));

        Assert.Equal(CommandOutcome.Unavailable, unattended.Outcome);
        Assert.Contains("no input surface was supplied", unattended.Reason!, StringComparison.Ordinal);

        // Declined: nothing ran, and declining is not a failure.
        var declined = await _registry.InvokeAsync(
            "mechanical.duplicate", One("Part"),
            (_, _, _, _) => Task.FromResult<IReadOnlyDictionary<string, string>?>(null));

        Assert.Equal(CommandOutcome.Cancelled, declined.Outcome);
        Assert.Null(declined.Result);
    }

    [Fact]
    public async Task AConfirmationGatedCommand_ReceivesItsMessageAtThePrompt()
    {
        string? seen = null;
        await _registry.InvokeAsync(
            "calculations.delete", One("Calculation"),
            (_, _, message, _) =>
            {
                seen = message;
                return Task.FromResult<IReadOnlyDictionary<string, string>?>(null);
            });

        Assert.Equal("Delete the selected Calculation? This cannot be undone.", seen);
    }

    // ==================================================================
    // 11. The two record-result descriptors bind independently
    // ==================================================================

    [Fact]
    public void RecordResultDescriptors_CarryTheirOwnBindings_OverTheOneSharedCommand()
    {
        var verification = Binding("verification.record-result");
        var manufacturing = Binding("manufacturing.record-inspection-result");

        // Two bindings, not one shared object: a binding belongs to a
        // descriptor, not to a command type.
        Assert.NotSame(verification, manufacturing);

        // Different Kind scopes, which is the whole point of binding per
        // descriptor: the same command reaches two disciplines' objects.
        Assert.Equal(["VerificationActivity"], verification.AppliesToKinds);
        Assert.Equal(["Inspection"], manufacturing.AppliesToKinds);

        Assert.True(_registry.Evaluate("verification.record-result", One("VerificationActivity")).IsAvailable);
        Assert.False(_registry.Evaluate("verification.record-result", One("Inspection")).IsAvailable);
        Assert.True(_registry.Evaluate("manufacturing.record-inspection-result", One("Inspection")).IsAvailable);
        Assert.False(_registry.Evaluate("manufacturing.record-inspection-result", One("VerificationActivity")).IsAvailable);
    }

    [Fact]
    public void RecordResultBindings_BothBuildTheOneSharedCommand_WithTheirOwnKind()
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["outcome"] = "Pass",
            ["method"] = "Inspection",
        };

        var verification = (RecordVerificationResultCommand)Binding("verification.record-result")
            .Build(One("VerificationActivity"), values);
        var manufacturing = (RecordVerificationResultCommand)Binding("manufacturing.record-inspection-result")
            .Build(One("Inspection"), values);

        Assert.Equal("VerificationActivity", verification.TargetKind);
        Assert.Equal("Inspection", manufacturing.TargetKind);
        Assert.Equal(VerificationOutcome.Pass, verification.Outcome);
        Assert.Equal(VerificationOutcome.Pass, manufacturing.Outcome);

        // Every optional collection stays at the command's own default.
        foreach (var command in new[] { verification, manufacturing })
        {
            Assert.Empty(command.Criteria);
            Assert.Empty(command.Evidence);
            Assert.Empty(command.LinkedDocumentIds);
            Assert.Empty(command.LinkedCalculationRecordIds);
            Assert.Empty(command.ReferencedMaterialIds);
        }
    }

    [Fact]
    public void BothRecordResultBindings_DefaultToTheExistingMethodValue()
    {
        Assert.Equal("Inspection", Parameter(Binding("verification.record-result"), "method").DefaultValue);
        Assert.Equal("Inspection", Parameter(Binding("manufacturing.record-inspection-result"), "method").DefaultValue);
    }

    // ==================================================================
    // 12. Macro eligibility matches the approved classification
    // ==================================================================

    [Fact]
    public void ExactlyTheApprovedCommands_CanRunUnattended()
    {
        // A command can run with no person present exactly when it needs
        // neither a value nor a confirmation — which is what RequiresPrompt
        // already means. Nothing new decides eligibility.
        var unattended = ProductionDescriptors
            .Where(d => d.Binding is { IsInvocable: true, RequiresPrompt: false })
            .Select(d => d.Id)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();

        // The set is the assertion. Two counts restating its size followed
        // this line until `WP-F` (`F-11`); both were derivable from it, so
        // adding a macro-safe command broke three assertions where one is
        // enough — and the one that remains names the command.
        Assert.Equal(MacroSafe.OrderBy(id => id, StringComparer.Ordinal).ToList(), unattended);
    }

    [Fact]
    public void NoParameterisedOrConfirmationGatedCommand_IsUnattended()
    {
        foreach (var descriptor in ProductionDescriptors)
        {
            var binding = descriptor.Binding!;
            if (!binding.IsInvocable)
                continue;

            var unattended = !binding.RequiresPrompt;
            var gated = binding.Parameters.Count > 0 || binding.ConfirmationMessage is not null;

            Assert.NotEqual(unattended, gated);

            if (unattended)
            {
                Assert.Contains(descriptor.Id, MacroSafe);
                Assert.Empty(binding.Parameters);
                Assert.Null(binding.ConfirmationMessage);
            }
        }
    }

    [Fact]
    public async Task AMacroSafeCommand_RunsWithNoPromptAtAll()
    {
        // No prompt supplied, and it still executes — the definition of
        // macro-eligible. It reports its handler's own failure against an
        // object that does not exist, which is exactly the proof that the
        // real handler ran.
        var invocation = await _registry.InvokeAsync("calculations.approve", One("Calculation"));

        Assert.Equal(CommandOutcome.Executed, invocation.Outcome);
        Assert.NotNull(invocation.Result);
        Assert.False(invocation.Result!.Succeeded);
        Assert.False(string.IsNullOrWhiteSpace(invocation.Result.Message));
    }

    // ==================================================================
    // 13. No binding invokes a handler; construction stays routed
    // ==================================================================

    [Fact]
    public async Task BuildingACommand_ConstructsIt_AndRunsNothing()
    {
        var service = (IRequirementsService)_host.Services!.GetService(typeof(IRequirementsService));
        var before = (await service.ListAsync()).Count;

        var command = Binding("requirements.create").Build(
            CommandContext.Empty,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["identifier"] = "REQ-NEVER-BUILT",
                ["statement"] = "Building is not executing.",
            });

        // A command object, and nothing else: no result, and no side effect.
        Assert.IsType<CreateRequirementCommand>(command);

        var after = await service.ListAsync();
        Assert.Equal(before, after.Count);
        Assert.DoesNotContain(after, r => r.Identifier == "REQ-NEVER-BUILT");
    }

    [Fact]
    public async Task EveryBoundCommand_ReachesItsHandlerOnlyThroughTheRegistrysOwnDispatchPath()
    {
        // A command whose handler is registered in the shared
        // CommandHandlerTable dispatches; the registry is the only thing
        // that puts a built command into it. Proven by the handler's own
        // result coming back — a binding that called a handler itself could
        // not produce a CommandInvocation at all.
        var invocation = await _registry.InvokeAsync(
            "requirements.set-owner",
            One("Requirement"),
            Answering(("owner", "A. Engineer")));

        Assert.Equal(CommandOutcome.Executed, invocation.Outcome);
        Assert.NotNull(invocation.Result);
    }

    [Fact]
    public void NoRegistrationSource_CallsAHandlerOrADispatcherFromInsideABinding()
    {
        foreach (var (file, source) in RegistrationSources())
        {
            // Comments are stripped first: several of these files' own
            // <remarks> legitimately name ICommandDispatcher.DispatchAsync
            // when explaining what a caller that already holds the data does.
            // The rule under test is about executable code.
            var code = string.Join("\n", source
                .Split('\n')
                .Select(line => line.Trim())
                .Where(line => !line.StartsWith("//", StringComparison.Ordinal)
                               && !line.StartsWith("///", StringComparison.Ordinal)));

            // The registrations still register handlers, which is the point;
            // what none of them may do is run one.
            Assert.DoesNotContain("HandleAsync", code, StringComparison.Ordinal);
            Assert.DoesNotContain("DispatchAsync", code, StringComparison.Ordinal);
            Assert.DoesNotContain("commandDispatcher.Dispatch", code, StringComparison.Ordinal);

            // And every handler registration is still exactly that.
            Assert.DoesNotContain("Handler().Handle", code, StringComparison.Ordinal);
            Assert.False(string.IsNullOrWhiteSpace(file));
        }
    }

    // ==================================================================
    // What Stage 3 must not have changed
    // ==================================================================

    [Fact]
    public void NoProductionDescriptor_GainedACreateDefaultFactory()
    {
        // CreateDefault is what the Command Palette and the Macro Manager
        // both gate on today. Stage 3 sets bindings and nothing else, so
        // neither surface's own behaviour can have moved.
        Assert.All(ProductionDescriptors, d => Assert.Null(d.CreateDefault));
    }

    [Fact]
    public void EveryDescriptorKeptItsIdentity()
    {
        // The invariant here is uniqueness, not the number — the canonical
        // count lives once, in EveryBindableAuditedDescriptor_HasAnInvocableBinding
        // (`WP-F`, `F-11`).
        Assert.Equal(
            ProductionDescriptors.Count,
            ProductionDescriptors.Select(d => d.Id).Distinct(StringComparer.Ordinal).Count());
        Assert.All(ProductionDescriptors, d => Assert.False(string.IsNullOrWhiteSpace(d.DisplayName)));
        Assert.All(ProductionDescriptors, d => Assert.False(string.IsNullOrWhiteSpace(d.Description)));

        foreach (var discipline in Disciplines)
            Assert.Contains(ProductionDescriptors, d => d.Category == discipline);
    }
}
