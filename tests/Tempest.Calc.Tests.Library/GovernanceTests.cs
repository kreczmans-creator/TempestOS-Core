using Tempest.Calc.Library;
using Tempest.Calc.Sheets;
using Tempest.Calc.Tests.Sheets;
using Tempest.Core.Configuration;
using Tempest.Core.EngineeringData;
using Tempest.Core.Evidence;

namespace Tempest.Calc.Tests.Library;

/// <summary>
/// The direct-stress test calculator gated behind the lifting flag and demanding the competent-person attestation and the second
/// independent check (ADR-0173). It stands in for S8 so the generic mechanism is tested on its own, not through one calculator.
/// </summary>
internal sealed class GovernedCalculator : ICalcCalculator
{
    public const string GatedId = "test.governed";
    public const string OpenId = "test.governed-open";

    private readonly DirectStressCalculator _inner = new();

    /// <param name="gated">Behind the lifting flag (default) or always available but still governed.</param>
    public GovernedCalculator(bool gated = true, bool attestation = true, bool secondCheck = true)
    {
        Template = _inner.Template with
        {
            Id = gated ? GatedId : OpenId,
            Inputs =
            [
                .. _inner.Template.Inputs,
                new CalcInputSpec(CalcAttestation.NameSymbol, "Competent person", CalcInputKind.Text, IsOptional: true),
                new CalcInputSpec(CalcAttestation.QualificationSymbol, "Qualification", CalcInputKind.Text, IsOptional: true),
                new CalcInputSpec(CalcAttestation.StatementSymbol, "Attestation statement", CalcInputKind.Text, IsOptional: true),
            ],
            IndependentCheck = true,
            FeatureFlag = gated ? CalcFeatures.Lifting : null,
            RequiresAttestation = attestation,
            RequiresSecondCheck = secondCheck,
        };
    }

    public CalcTemplate Template { get; }

    public void Run(CalcRunContext c) => _inner.Run(c);
}

public sealed class GovernanceTests
{
    private static readonly CalcFeatureFlags LiftingOn = new([CalcFeatures.Lifting]);

    private const string Designer = "designer";
    private const string FirstChecker = "checker.one";
    private const string SecondChecker = "checker.two";

    private static LibraryTestHost Host(bool on = true, bool gated = true) =>
        new(calculators: [new GovernedCalculator(gated)], features: on ? LiftingOn : null);

    private static string Id(bool gated = true) => gated ? GovernedCalculator.GatedId : GovernedCalculator.OpenId;

    private static IReadOnlyList<CalcInputValue> Inputs(string? name = "Dr Pat Competent", string? qualification = "CEng MIMechE, LEEA Advanced Inspector", string? statement = "Reviewed the lug geometry, the material certificate and the calculation.")
    {
        var list = LibraryTestHost.ProvenancedInputs().ToList();
        var basis = CalcProvenance.Assumed("Attestation given by the named person.");

        if (name is not null)
            list.Add(CalcInputValue.Choice(CalcAttestation.NameSymbol, name, basis));

        if (qualification is not null)
            list.Add(CalcInputValue.Choice(CalcAttestation.QualificationSymbol, qualification, basis));

        if (statement is not null)
            list.Add(CalcInputValue.Choice(CalcAttestation.StatementSymbol, statement, basis));

        return list;
    }

    private static async Task<CalcInstance> NewAsync(LibraryTestHost host, IReadOnlyList<CalcInputValue>? inputs = null, string name = "Lug 1", bool gated = true)
    {
        host.Principal = Designer;
        return await host.Service.CreateAsync(Id(gated), await host.ProjectAsync(), name, inputs ?? Inputs());
    }

    private static async Task<CalcInstance> CheckedAsync(LibraryTestHost host, IReadOnlyList<CalcInputValue>? inputs = null, bool gated = true)
    {
        var instance = await NewAsync(host, inputs, gated: gated);
        host.Principal = FirstChecker;
        var check = await host.Service.CheckAsync(instance.Id, "Chris Checker", "Check Co", "Checked.", CheckOutcome.Accepted);
        Assert.True(check.Succeeded, check.Reason);
        return (await host.Store.FindInstanceAsync(instance.Id))!;
    }

    private static async Task<CalcInstance> CountersignedAsync(LibraryTestHost host, bool gated = true)
    {
        var instance = await CheckedAsync(host, gated: gated);
        host.Principal = SecondChecker;
        var second = await host.Service.CountersignAsync(instance.Id, "Dana Second", "Check Co", "Second check made.", CheckOutcome.Accepted);
        Assert.True(second.Succeeded, second.Reason);
        return (await host.Store.FindInstanceAsync(instance.Id))!;
    }

    // ------------------------------------------------------------------
    // The feature flag
    // ------------------------------------------------------------------

    [Fact]
    public void With_The_Flag_Off_The_Calculator_Is_Not_In_The_Catalogue_Or_The_Search_Lists()
    {
        var host = Host(on: false);
        var catalogue = host.Catalogue;

        Assert.DoesNotContain(catalogue.Current(), e => e.Template.Id == GovernedCalculator.GatedId);
        Assert.DoesNotContain(catalogue.All(), e => e.Template.Id == GovernedCalculator.GatedId);
        Assert.DoesNotContain(catalogue.ByDiscipline(CalcDiscipline.Structural), e => e.Template.Id == GovernedCalculator.GatedId);
        Assert.Null(catalogue.Find(GovernedCalculator.GatedId));
        Assert.Null(catalogue.Find(GovernedCalculator.GatedId + "@1.0.0"));
        Assert.True(catalogue.IsWithheld(GovernedCalculator.GatedId));
        Assert.True(catalogue.IsWithheld(GovernedCalculator.GatedId + "@1.0.0"));

        // The Ctrl+K entries and the worksheet tree are built from Current(); the ungated calculators are still there.
        Assert.Contains(catalogue.Current(), e => e.Template.Id == "test.direct-stress");
    }

    [Fact]
    public void With_The_Flag_On_The_Calculator_Is_Listed_And_Found()
    {
        var host = Host(on: true);
        Assert.Contains(host.Catalogue.Current(), e => e.Template.Id == GovernedCalculator.GatedId);
        Assert.NotNull(host.Catalogue.Find(GovernedCalculator.GatedId));
        Assert.False(host.Catalogue.IsWithheld(GovernedCalculator.GatedId));
        Assert.Null(host.Catalogue.FeatureRefusal(GovernedCalculator.GatedId));
    }

    [Fact]
    public async Task Create_Through_The_Service_Is_Refused_With_A_Clear_Message()
    {
        var host = Host(on: false);

        foreach (var reference in new[] { GovernedCalculator.GatedId, GovernedCalculator.GatedId + "@1.0.0" })
        {
            var ex = await Assert.ThrowsAsync<CalcFeatureDisabledException>(() => host.Service.CreateAsync(reference, null, "Lug", Inputs()));
            Assert.Contains("switched off", ex.Message, StringComparison.Ordinal);
            Assert.Contains("Calc:Features:Lifting", ex.Message, StringComparison.Ordinal);
            Assert.Contains("review checklist", ex.Message, StringComparison.Ordinal);
        }

        // An unknown template is still the ordinary "not registered", not the switched-off message.
        var unknown = await Assert.ThrowsAsync<KeyNotFoundException>(() => host.Service.CreateAsync("test.nothing", null, "x", null));
        Assert.DoesNotContain("switched off", unknown.Message, StringComparison.Ordinal);
        Assert.IsNotType<CalcFeatureDisabledException>(unknown);
    }

    [Fact]
    public void A_Preview_Or_Port_Lookup_Of_The_Withheld_Calculator_Is_Refused()
    {
        var host = Host(on: false);
        Assert.Throws<CalcFeatureDisabledException>(() => host.Service.Preview(GovernedCalculator.GatedId, new CalcInputSet(Inputs())));
        Assert.Throws<CalcFeatureDisabledException>(() => host.Service.OutputPorts(GovernedCalculator.GatedId));
        Assert.Throws<CalcFeatureDisabledException>(() => host.Catalogue.Get(GovernedCalculator.GatedId));
    }

    [Fact]
    public async Task Importing_Json_Or_Csv_Inputs_Does_Not_Open_The_Gate()
    {
        var host = Host(on: false);
        var template = new GovernedCalculator().Template;

        // The parsers work on any template they are handed (they create nothing) ...
        var json = CalcInputJson.Write(new CalcInputSet(Inputs()));
        var (fromJson, jsonNotices) = CalcInputJson.Read(json, template);
        Assert.DoesNotContain(jsonNotices, n => n.Severity == CalcNoticeSeverity.Block);
        var csv = CalcCsv.ExportInputs(template, new CalcInputSet(Inputs()));
        var (fromCsv, csvNotices) = CalcCsv.ImportInputs(template, csv);
        Assert.DoesNotContain(csvNotices, n => n.Severity == CalcNoticeSeverity.Block);

        // ... and what they produce cannot be made into a calculation.
        await Assert.ThrowsAsync<CalcFeatureDisabledException>(() => host.Service.CreateAsync(GovernedCalculator.GatedId, null, "From JSON", [.. fromJson.Values]));
        await Assert.ThrowsAsync<CalcFeatureDisabledException>(() => host.Service.CreateAsync(GovernedCalculator.GatedId, null, "From CSV", [.. fromCsv.Values]));
        Assert.Empty(await host.Store.ListInstancesAsync(null));
    }

    [Fact]
    public async Task A_Pack_Template_Naming_The_Withheld_Calculator_Is_Refused_And_Creates_Nothing()
    {
        var template = new CalcPackTemplate(Guid.NewGuid().ToString("N"), "Lift pack", "A lifting chain.", [new CalcPackStep("lug", GovernedCalculator.GatedId, "Lug")], []);
        var host = new LibraryTestHost(calculators: [new GovernedCalculator()], packTemplates: [template]);
        var project = await host.ProjectAsync();

        Assert.Contains("switched off", host.Service.ValidatePackTemplate(template), StringComparison.Ordinal);
        var (result, refusal) = await host.Service.InstantiatePackTemplateAsync(template, project, "Lifting chain");
        Assert.Null(result);
        Assert.Contains("switched off", refusal, StringComparison.Ordinal);
        Assert.Empty(await host.Store.ListInstancesAsync(project));
        Assert.Empty(await host.Store.ListPacksAsync(project));

        // With the flag on the same template is valid.
        var on = new LibraryTestHost(calculators: [new GovernedCalculator()], packTemplates: [template], features: LiftingOn);
        Assert.Null(on.Service.ValidatePackTemplate(template));
    }

    [Fact]
    public async Task An_Existing_Instance_Cannot_Be_Edited_Checked_Countersigned_Issued_Or_Revised_When_The_Flag_Is_Off()
    {
        var on = Host(on: true);
        var counter = await CountersignedAsync(on);
        var draft = await NewAsync(on, name: "Draft lug");

        // The same store, evidence and people, but a catalogue built with the flag off (the installation switched the feature off).
        var off = new CalcInstanceService(new CalcCatalogue([new GovernedCalculator(), new DirectStressCalculator()]), on.Store, on.Evidence, on.Context, renderer: null, applicationVersion: "test") { PrincipalOverride = () => FirstChecker };

        void AssertSwitchedOff(CalcActionResult result)
        {
            Assert.False(result.Succeeded);
            Assert.Contains("switched off", result.Reason, StringComparison.Ordinal);
        }

        AssertSwitchedOff(await off.IssueAsync(counter.Id, "ISS-1", "Client"));
        AssertSwitchedOff(await off.ReviseAsync(counter.Id));
        AssertSwitchedOff(await off.CountersignAsync(draft.Id, "Dana Second", "Check Co", "x", CheckOutcome.Accepted));
        AssertSwitchedOff(await off.CheckAsync(draft.Id, "Chris Checker", "Check Co", "x", CheckOutcome.Accepted));
        AssertSwitchedOff(await off.UpdateInputsAsync(draft.Id, Inputs()));
        AssertSwitchedOff(await off.BindAsync(draft.Id, "F", counter.Id, "sigma"));
        AssertSwitchedOff(await off.BindCellAsync(draft.Id, "x", 0, "y", counter.Id, "sigma"));
        await Assert.ThrowsAsync<CalcFeatureDisabledException>(() => off.RunAsync(draft.Id));
        await Assert.ThrowsAsync<CalcFeatureDisabledException>(() => off.CreatePackAsync(draft.ProjectId, "Pack", [draft.Id]));

        Assert.Equal(CalcInstanceStatus.Checked, (await on.Store.FindInstanceAsync(counter.Id))!.Status);
    }

    [Fact]
    public async Task A_Pack_With_A_Withheld_Member_Cannot_Be_Checked_Countersigned_Or_Issued_When_The_Flag_Is_Off()
    {
        var on = Host(on: true);
        var project = await on.ProjectAsync();
        on.Principal = Designer;
        var lug = await on.Service.CreateAsync(GovernedCalculator.GatedId, project, "Lug", Inputs());
        var pack = await on.Service.CreatePackAsync(project, "Lift pack", [lug.Id]);
        on.Principal = FirstChecker;
        Assert.True((await on.Service.CheckPackAsync(pack.Id, "Chris Checker", "Check Co", "ok", CheckOutcome.Accepted)).Succeeded);
        on.Principal = SecondChecker;
        Assert.True((await on.Service.CountersignPackAsync(pack.Id, "Dana Second", "Check Co", "ok", CheckOutcome.Accepted)).Succeeded);

        var off = new CalcInstanceService(new CalcCatalogue([new GovernedCalculator(), new DirectStressCalculator()]), on.Store, on.Evidence, on.Context, renderer: null, applicationVersion: "test") { PrincipalOverride = () => "someone.else" };

        var issue = await off.IssuePackAsync(pack.Id, "ISS-1", "Client");
        Assert.False(issue.Succeeded);
        Assert.Contains("switched off", issue.Reason, StringComparison.Ordinal);

        var check = await off.CheckPackAsync(pack.Id, "Chris Checker", "Check Co", "ok", CheckOutcome.Accepted);
        Assert.False(check.Succeeded);

        var counter = await off.CountersignPackAsync(pack.Id, "Dana Second", "Check Co", "ok", CheckOutcome.Accepted);
        Assert.False(counter.Succeeded);
        Assert.Equal(CalcInstanceStatus.Checked, (await on.Store.FindPackAsync(pack.Id))!.Status);
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("TRUE", true)]
    [InlineData("  True  ", true)]
    [InlineData("false", false)]
    [InlineData("1", false)]
    [InlineData("yes", false)]
    [InlineData("on", false)]
    [InlineData("", false)]
    [InlineData("truee", false)]
    public void The_Flag_Is_On_Only_For_An_Exact_True_In_The_Installation_Configuration(string value, bool expected)
    {
        var configuration = new ConfigurationBuilder().AddSource(new MemoryConfigurationSource([new KeyValuePair<string, string>("Calc:Features:Lifting", value)])).Build();
        Assert.Equal(expected, CalcFeatureFlags.FromConfiguration(configuration).IsEnabled(CalcFeatures.Lifting));
        Assert.Equal("Calc:Features:Lifting", CalcFeatureFlags.ConfigurationKey(CalcFeatures.Lifting));
    }

    [Fact]
    public void The_Flag_Is_Off_By_Default_And_Without_Configuration()
    {
        Assert.False(CalcFeatureFlags.None.IsEnabled(CalcFeatures.Lifting));
        Assert.False(CalcFeatureFlags.FromConfiguration(null).IsEnabled(CalcFeatures.Lifting));
        Assert.False(CalcFeatureFlags.FromConfiguration(new ConfigurationBuilder().Build()).IsEnabled(CalcFeatures.Lifting));
        Assert.False(new CalcCatalogue([new GovernedCalculator()]).Features.IsEnabled(CalcFeatures.Lifting));
        Assert.Empty(new CalcCatalogue([new GovernedCalculator()]).Current());
        Assert.True(CalcFeatureFlags.None.IsEnabled(null));
        Assert.True(CalcFeatureFlags.None.IsEnabled(string.Empty));
    }

    [Fact]
    public void A_Template_Cannot_Declare_Governance_It_Cannot_Meet()
    {
        var unknownFlag = new GovernedCalculator().Template with { FeatureFlag = "no-such-feature" };
        Assert.Throws<ArgumentException>(() => new CalcCatalogue().Register(new FixedTemplateCalculator(unknownFlag)));

        var noInputs = new GovernedCalculator().Template with { Inputs = new DirectStressCalculator().Template.Inputs };
        Assert.Throws<ArgumentException>(() => new CalcCatalogue(LiftingOn).Register(new FixedTemplateCalculator(noInputs)));

        var notIndependent = new GovernedCalculator().Template with { IndependentCheck = false };
        Assert.Throws<ArgumentException>(() => new CalcCatalogue(LiftingOn).Register(new FixedTemplateCalculator(notIndependent)));
    }

    [Fact]
    public void Only_A_Template_That_Opts_In_Changes_Its_Hash()
    {
        var plain = new DirectStressCalculator();
        var canonical = CalcTemplateHasher.CanonicalText(plain.Template, []);
        Assert.DoesNotContain("feature=", canonical, StringComparison.Ordinal);
        Assert.DoesNotContain("requires-", canonical, StringComparison.Ordinal);

        var baseHash = CalcTemplateHasher.Hash(plain.Template, []);
        Assert.NotEqual(baseHash, CalcTemplateHasher.Hash(plain.Template with { FeatureFlag = CalcFeatures.Lifting }, []));
        Assert.NotEqual(baseHash, CalcTemplateHasher.Hash(plain.Template with { RequiresAttestation = true }, []));
        Assert.NotEqual(baseHash, CalcTemplateHasher.Hash(plain.Template with { RequiresSecondCheck = true }, []));
        Assert.Equal(baseHash, CalcTemplateHasher.Hash(plain.Template with { RequiresAttestation = false, RequiresSecondCheck = false, FeatureFlag = null }, []));
    }

    [Fact]
    public async Task An_Ordinary_Template_Is_Governed_Exactly_As_Before()
    {
        var host = Host(on: false);
        var project = await host.ProjectAsync();
        host.Principal = Designer;
        var plain = await host.Service.CreateAsync("test.direct-stress", project, "Plain", LibraryTestHost.ProvenancedInputs());
        host.Principal = FirstChecker;
        Assert.True((await host.Service.CheckAsync(plain.Id, "Chris Checker", "Check Co", "ok", CheckOutcome.Accepted)).Succeeded);
        var issued = await host.Service.IssueAsync(plain.Id, "ISS-1", "Client");
        Assert.True(issued.Succeeded, issued.Reason);
        Assert.Null(issued.Instance!.SecondCheck);
        Assert.Null(issued.Instance.CheckerPrincipal);

        var countersign = await host.Service.CountersignAsync(plain.Id, "Dana Second", "Check Co", "x", CheckOutcome.Accepted);
        Assert.False(countersign.Succeeded);
    }

    // ------------------------------------------------------------------
    // The competent-person attestation
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(null, "CEng", "Reviewed the lug geometry and the calculation.", CalcAttestation.NameSymbol)]
    [InlineData("  ", "CEng", "Reviewed the lug geometry and the calculation.", CalcAttestation.NameSymbol)]
    [InlineData("Pat", null, "Reviewed the lug geometry and the calculation.", CalcAttestation.QualificationSymbol)]
    [InlineData("Pat", "  ", "Reviewed the lug geometry and the calculation.", CalcAttestation.QualificationSymbol)]
    [InlineData("Pat", "CEng", null, CalcAttestation.StatementSymbol)]
    [InlineData("Pat", "CEng", "ok", CalcAttestation.StatementSymbol)]
    [InlineData("Pat", "CEng", "                    ", CalcAttestation.StatementSymbol)]
    public async Task Check_Is_Refused_While_Any_Part_Of_The_Attestation_Is_Missing_Or_Empty(string? name, string? qualification, string? statement, string field)
    {
        var host = Host();
        var instance = await NewAsync(host, Inputs(name, qualification, statement));
        host.Principal = FirstChecker;

        var refused = await host.Service.CheckAsync(instance.Id, "Chris Checker", "Check Co", "ok", CheckOutcome.Accepted);
        Assert.False(refused.Succeeded);
        Assert.Contains("competent-person attestation", refused.Reason, StringComparison.Ordinal);
        Assert.Contains($"'{field}'", refused.Reason, StringComparison.Ordinal);
        Assert.Equal(CalcInstanceStatus.Draft, (await host.Store.FindInstanceAsync(instance.Id))!.Status);
    }

    [Theory]
    [InlineData("designer")]
    [InlineData("Designer")]
    [InlineData("  DESIGNER ")]
    public async Task The_Creator_Cannot_Attest_Their_Own_Calculation(string name)
    {
        var host = Host();
        var instance = await NewAsync(host, Inputs(name: name));
        host.Principal = FirstChecker;

        var refused = await host.Service.CheckAsync(instance.Id, "Chris Checker", "Check Co", "ok", CheckOutcome.Accepted);
        Assert.False(refused.Succeeded);
        Assert.Contains("independent competent-person attestation", refused.Reason, StringComparison.Ordinal);
        Assert.Contains("created or edited", refused.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_Sign_In_Written_Differently_Is_Still_The_Same_Person()
    {
        var host = Host();
        host.Principal = "jane.smith";
        var project = await host.ProjectAsync();
        var instance = await host.Service.CreateAsync(GovernedCalculator.GatedId, project, "Lug", Inputs(name: "Jane Smith"));
        host.Principal = FirstChecker;
        Assert.Contains("created or edited", (await host.Service.CheckAsync(instance.Id, "Chris Checker", "Check Co", "ok", CheckOutcome.Accepted)).Reason, StringComparison.Ordinal);

        host.Principal = "ACME\\jane.smith";
        var other = await host.Service.CreateAsync(GovernedCalculator.GatedId, project, "Lug 2", Inputs(name: "J. SMITH Jane Smith"));
        Assert.False(Service_Names_Differ("ACME\\jane.smith", "Jane Smith"));
        host.Principal = FirstChecker;
        Assert.True((await host.Service.CheckAsync(other.Id, "Chris Checker", "Check Co", "ok", CheckOutcome.Accepted)).Succeeded);
    }

    private static bool Service_Names_Differ(string a, string b) => !CalcInstanceService.SamePerson(a, b);

    [Fact]
    public async Task Someone_Who_Edited_The_Inputs_Cannot_Attest_Either()
    {
        var host = Host();
        var instance = await NewAsync(host, Inputs(name: "Edith Editor"));
        host.Principal = "edith.editor";
        Assert.True((await host.Service.UpdateInputsAsync(instance.Id, Inputs(name: "Edith Editor"))).Succeeded);

        host.Principal = FirstChecker;
        var refused = await host.Service.CheckAsync(instance.Id, "Chris Checker", "Check Co", "ok", CheckOutcome.Accepted);
        Assert.False(refused.Succeeded);
        Assert.Contains("created or edited", refused.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_Independent_Attestation_Lets_The_Calculation_Be_Checked()
    {
        var host = Host();
        var instance = await NewAsync(host);
        host.Principal = FirstChecker;
        Assert.True((await host.Service.CheckAsync(instance.Id, "Chris Checker", "Check Co", "ok", CheckOutcome.Accepted)).Succeeded);
        var stored = await host.Store.FindInstanceAsync(instance.Id);
        Assert.Equal(FirstChecker, stored!.CheckerPrincipal);
    }

    [Fact]
    public async Task Issue_Re_Reads_The_Attestation_Even_If_The_Stored_Instance_Was_Altered_Underneath()
    {
        var host = Host();
        var counter = await CountersignedAsync(host);

        // A checked instance written straight into the store with the attestation blanked (the service never would).
        var tampered = counter with { Inputs = [.. counter.Inputs.Where(i => i.Symbol != CalcAttestation.StatementSymbol)], UpdatedAt = counter.UpdatedAt.AddSeconds(1) };
        await host.Store.SaveInstanceAsync(tampered);

        host.Principal = "issuer";
        var refused = await host.Service.IssueAsync(counter.Id, "ISS-1", "Client");
        Assert.False(refused.Succeeded);
        Assert.Contains("competent-person attestation", refused.Reason, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------
    // The second independent check
    // ------------------------------------------------------------------

    [Fact]
    public async Task Issue_Is_Refused_Until_A_Second_Checker_Has_Signed()
    {
        var host = Host();
        var instance = await CheckedAsync(host);
        host.Principal = "issuer";

        var refused = await host.Service.IssueAsync(instance.Id, "ISS-1", "Client");
        Assert.False(refused.Succeeded);
        Assert.Contains("second, independent check", refused.Reason, StringComparison.Ordinal);
        Assert.Equal(CalcInstanceStatus.Checked, (await host.Store.FindInstanceAsync(instance.Id))!.Status);
    }

    [Fact]
    public async Task The_Second_Check_Cannot_Be_Given_By_The_First_Checker()
    {
        var host = Host();
        var instance = await CheckedAsync(host);

        // Same sign-in.
        host.Principal = FirstChecker;
        var sameSignIn = await host.Service.CountersignAsync(instance.Id, "Someone Else", "Check Co", "second", CheckOutcome.Accepted);
        Assert.False(sameSignIn.Succeeded);
        Assert.Contains("two different checkers", sameSignIn.Reason, StringComparison.Ordinal);

        // Same typed name from another sign-in.
        host.Principal = SecondChecker;
        var sameName = await host.Service.CountersignAsync(instance.Id, "chris  CHECKER", "Check Co", "second", CheckOutcome.Accepted);
        Assert.False(sameName.Succeeded);
        Assert.Contains("two different checkers", sameName.Reason, StringComparison.Ordinal);

        // The first checker's sign-in typed as the name.
        var signInAsName = await host.Service.CountersignAsync(instance.Id, "Checker One", "Check Co", "second", CheckOutcome.Accepted);
        Assert.False(signInAsName.Succeeded);

        Assert.Null((await host.Store.FindInstanceAsync(instance.Id))!.SecondCheck);
    }

    [Theory]
    [InlineData(Designer, "Anyone")]
    [InlineData(SecondChecker, "Designer")]
    public async Task The_Second_Check_Cannot_Be_Given_By_The_Creator_Or_An_Editor(string principal, string typedName)
    {
        var host = Host();
        var instance = await CheckedAsync(host);
        host.Principal = principal;

        var refused = await host.Service.CountersignAsync(instance.Id, typedName, "Check Co", "second", CheckOutcome.Accepted);
        Assert.False(refused.Succeeded);
        Assert.Contains("independent", refused.Reason, StringComparison.Ordinal);
        Assert.Null((await host.Store.FindInstanceAsync(instance.Id))!.SecondCheck);
    }

    [Fact]
    public async Task The_Second_Check_Needs_Someone_Signed_In()
    {
        var host = Host();
        var instance = await CheckedAsync(host);
        host.Principal = EngineeringDocumentStore.UnknownAuthorPrincipalId;

        var refused = await host.Service.CountersignAsync(instance.Id, "Dana Second", "Check Co", "second", CheckOutcome.Accepted);
        Assert.False(refused.Succeeded);
        Assert.Contains("nobody is signed in", refused.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_Second_Check_Needs_A_Name_And_An_Organisation_And_Comes_After_The_First()
    {
        var host = Host();
        var draft = await NewAsync(host);
        host.Principal = SecondChecker;

        var early = await host.Service.CountersignAsync(draft.Id, "Dana Second", "Check Co", "second", CheckOutcome.Accepted);
        Assert.False(early.Succeeded);
        Assert.Contains("after the first check", early.Reason, StringComparison.Ordinal);

        Assert.False((await host.Service.CountersignAsync(draft.Id, " ", "Check Co", "second", CheckOutcome.Accepted)).Succeeded);
        Assert.False((await host.Service.CountersignAsync(draft.Id, "Dana Second", " ", "second", CheckOutcome.Accepted)).Succeeded);
        Assert.False((await host.Service.CountersignAsync(Guid.NewGuid(), "Dana Second", "Check Co", "second", CheckOutcome.Accepted)).Succeeded);
    }

    [Fact]
    public async Task A_Calculation_Takes_One_Second_Check_Only()
    {
        var host = Host();
        var instance = await CountersignedAsync(host);
        host.Principal = "checker.three";

        var again = await host.Service.CountersignAsync(instance.Id, "Third Person", "Check Co", "third", CheckOutcome.Accepted);
        Assert.False(again.Succeeded);
        Assert.Contains("already has a second check", again.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_Template_Without_The_Requirement_Takes_No_Second_Check()
    {
        var host = new LibraryTestHost(calculators: [new GovernedCalculator(gated: false, attestation: false, secondCheck: false)], features: LiftingOn);
        var instance = await CheckedAsync(host, Inputs(null, null, null), gated: false);
        host.Principal = SecondChecker;
        var refused = await host.Service.CountersignAsync(instance.Id, "Dana Second", "Check Co", "second", CheckOutcome.Accepted);
        Assert.False(refused.Succeeded);
        Assert.Contains("does not require one", refused.Reason, StringComparison.Ordinal);

        // And it issues without one, and without an attestation.
        host.Principal = "issuer";
        Assert.True((await host.Service.IssueAsync(instance.Id, "ISS-1", "Client")).Succeeded);
    }

    [Fact]
    public async Task A_Rejected_Second_Check_Stops_Issue_And_The_Calculation_Can_Be_Revised()
    {
        var host = Host();
        var instance = await CheckedAsync(host);
        host.Principal = SecondChecker;

        var rejected = await host.Service.CountersignAsync(instance.Id, "Dana Second", "Check Co", "The sling angle is wrong.", CheckOutcome.Rejected);
        Assert.False(rejected.Succeeded);
        Assert.Contains("rejected", rejected.Reason, StringComparison.Ordinal);

        host.Principal = "issuer";
        var issue = await host.Service.IssueAsync(instance.Id, "ISS-1", "Client");
        Assert.False(issue.Succeeded);
        Assert.Contains("rejected by its second checker", issue.Reason, StringComparison.Ordinal);

        host.Principal = Designer;
        var revised = await host.Service.ReviseAsync(instance.Id);
        Assert.True(revised.Succeeded, revised.Reason);
        Assert.Null(revised.Instance!.SecondCheck);
        Assert.Equal(CalcInstanceStatus.Draft, revised.Instance.Status);
        Assert.Equal("B", revised.Instance.Revision);
    }

    [Fact]
    public async Task A_Second_Check_Forged_Into_The_Store_By_The_First_Checker_Does_Not_Pass_Issue()
    {
        var host = Host();
        var instance = await CheckedAsync(host);

        // Bypassing CountersignAsync: a countersignature by the same person written straight into the store.
        var forged = instance with { SecondCheck = new CalcCountersignature("Chris Checker", "Check Co", "forged", FirstChecker, DateTimeOffset.UtcNow), UpdatedAt = instance.UpdatedAt.AddSeconds(1) };
        await host.Store.SaveInstanceAsync(forged);

        host.Principal = "issuer";
        var refused = await host.Service.IssueAsync(instance.Id, "ISS-1", "Client");
        Assert.False(refused.Succeeded);
        Assert.Contains("two different checkers", refused.Reason, StringComparison.Ordinal);

        // Likewise one signed by the creator.
        var byAuthor = forged with { SecondCheck = new CalcCountersignature("Designer", "Check Co", "forged", Designer, DateTimeOffset.UtcNow), UpdatedAt = forged.UpdatedAt.AddSeconds(1) };
        await host.Store.SaveInstanceAsync(byAuthor);
        var again = await host.Service.IssueAsync(instance.Id, "ISS-1", "Client");
        Assert.False(again.Succeeded);
        Assert.Contains("independent second check", again.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task With_Two_Independent_Checkers_And_An_Attestation_The_Sheet_Issues_And_Prints_Both_Signatures()
    {
        var host = Host();
        var instance = await CountersignedAsync(host);
        Assert.Equal("Dana Second", instance.SecondCheck!.Name);
        Assert.Equal(SecondChecker, instance.SecondCheck.Principal);

        host.Principal = "issuer";
        var issued = await host.Service.IssueAsync(instance.Id, "ISS-1", "Client");
        Assert.True(issued.Succeeded, issued.Reason);

        var text = issued.Instance!.Issue!.SheetText;
        Assert.Contains("Chris Checker", text, StringComparison.Ordinal);
        Assert.Contains("second check: Dana Second", text, StringComparison.Ordinal);
        Assert.Contains("Dr Pat Competent", text, StringComparison.Ordinal);
        Assert.Contains("LEEA Advanced Inspector", text, StringComparison.Ordinal);

        // The issued record is immutable, second check and all.
        host.Principal = "checker.three";
        Assert.False((await host.Service.CountersignAsync(instance.Id, "Third Person", "Check Co", "third", CheckOutcome.Accepted)).Succeeded);
    }

    [Fact]
    public async Task A_Revision_Starts_Without_The_Earlier_Second_Check()
    {
        var host = Host();
        var instance = await CountersignedAsync(host);
        host.Principal = "issuer";
        Assert.True((await host.Service.IssueAsync(instance.Id, "ISS-1", "Client")).Succeeded);

        host.Principal = Designer;
        var revised = await host.Service.ReviseAsync(instance.Id);
        Assert.True(revised.Succeeded, revised.Reason);
        Assert.Null(revised.Instance!.SecondCheck);
        Assert.Null(revised.Instance.CheckerPrincipal);
    }

    // ------------------------------------------------------------------
    // Packs
    // ------------------------------------------------------------------

    private static async Task<(LibraryTestHost Host, CalcPack Pack, CalcInstance Member)> PackAsync(IReadOnlyList<CalcInputValue>? inputs = null)
    {
        var host = Host();
        var project = await host.ProjectAsync();
        host.Principal = Designer;
        var lug = await host.Service.CreateAsync(GovernedCalculator.GatedId, project, "Lug", inputs ?? Inputs());
        var pack = await host.Service.CreatePackAsync(project, "Lift pack", [lug.Id]);
        return (host, pack, lug);
    }

    [Fact]
    public async Task A_Pack_Is_Not_Checked_While_A_Member_Lacks_Its_Attestation()
    {
        var (host, pack, _) = await PackAsync(Inputs(statement: null));
        host.Principal = FirstChecker;
        var refused = await host.Service.CheckPackAsync(pack.Id, "Chris Checker", "Check Co", "ok", CheckOutcome.Accepted);
        Assert.False(refused.Succeeded);
        Assert.Contains("Pack member 'Lug'", refused.Reason, StringComparison.Ordinal);
        Assert.Contains("competent-person attestation", refused.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_Pack_Member_Cannot_Be_Attested_By_Its_Own_Author()
    {
        var (host, pack, _) = await PackAsync(Inputs(name: "Designer"));
        host.Principal = FirstChecker;
        var refused = await host.Service.CheckPackAsync(pack.Id, "Chris Checker", "Check Co", "ok", CheckOutcome.Accepted);
        Assert.False(refused.Succeeded);
        Assert.Contains("created or edited", refused.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_Pack_Needs_A_Second_Check_To_Issue_And_Cannot_Have_It_From_The_First_Checker_Or_An_Author()
    {
        var (host, pack, _) = await PackAsync();
        host.Principal = FirstChecker;
        Assert.True((await host.Service.CheckPackAsync(pack.Id, "Chris Checker", "Check Co", "ok", CheckOutcome.Accepted)).Succeeded);

        host.Principal = "issuer";
        var early = await host.Service.IssuePackAsync(pack.Id, "ISS-1", "Client");
        Assert.False(early.Succeeded);
        Assert.Contains("second, independent check", early.Reason, StringComparison.Ordinal);

        host.Principal = FirstChecker;
        Assert.Contains("two different checkers", (await host.Service.CountersignPackAsync(pack.Id, "Other Name", "Check Co", "s", CheckOutcome.Accepted)).Reason, StringComparison.Ordinal);
        host.Principal = SecondChecker;
        Assert.Contains("two different checkers", (await host.Service.CountersignPackAsync(pack.Id, "Chris Checker", "Check Co", "s", CheckOutcome.Accepted)).Reason, StringComparison.Ordinal);
        host.Principal = Designer;
        Assert.False((await host.Service.CountersignPackAsync(pack.Id, "Other Name", "Check Co", "s", CheckOutcome.Accepted)).Succeeded);
        host.Principal = SecondChecker;
        Assert.False((await host.Service.CountersignPackAsync(pack.Id, "Designer", "Check Co", "s", CheckOutcome.Accepted)).Succeeded);

        Assert.Null((await host.Store.FindPackAsync(pack.Id))!.SecondCheck);
    }

    [Fact]
    public async Task A_Pack_With_Two_Independent_Checkers_Issues_And_Its_Sheets_Name_The_Second()
    {
        var (host, pack, member) = await PackAsync();
        host.Principal = FirstChecker;
        Assert.True((await host.Service.CheckPackAsync(pack.Id, "Chris Checker", "Check Co", "ok", CheckOutcome.Accepted)).Succeeded);
        host.Principal = SecondChecker;
        var second = await host.Service.CountersignPackAsync(pack.Id, "Dana Second", "Check Co", "second made", CheckOutcome.Accepted);
        Assert.True(second.Succeeded, second.Reason);

        host.Principal = "issuer";
        var issued = await host.Service.IssuePackAsync(pack.Id, "ISS-1", "Client");
        Assert.True(issued.Succeeded, issued.Reason);

        var stored = await host.Store.FindInstanceAsync(member.Id);
        Assert.Equal(CalcInstanceStatus.Issued, stored!.Status);
        Assert.Contains("second check: Dana Second", stored.Issue!.SheetText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_Rejected_Pack_Second_Check_Stops_Issue_And_The_Pack_Can_Be_Reopened()
    {
        var (host, pack, _) = await PackAsync();
        host.Principal = FirstChecker;
        Assert.True((await host.Service.CheckPackAsync(pack.Id, "Chris Checker", "Check Co", "ok", CheckOutcome.Accepted)).Succeeded);
        host.Principal = SecondChecker;
        var rejected = await host.Service.CountersignPackAsync(pack.Id, "Dana Second", "Check Co", "No.", CheckOutcome.Rejected);
        Assert.False(rejected.Succeeded);

        host.Principal = "issuer";
        var issue = await host.Service.IssuePackAsync(pack.Id, "ISS-1", "Client");
        Assert.False(issue.Succeeded);
        Assert.Contains("rejected by its second checker", issue.Reason, StringComparison.Ordinal);

        var reopened = await host.Service.ReopenPackAsync(pack.Id);
        Assert.True(reopened.Succeeded, reopened.Reason);
        var after = await host.Store.FindPackAsync(pack.Id);
        Assert.Null(after!.SecondCheck);
        Assert.Null(after.CheckerPrincipal);
    }

    [Fact]
    public async Task A_Pack_Without_A_Governed_Member_Takes_No_Second_Check()
    {
        var host = Host();
        var project = await host.ProjectAsync();
        host.Principal = Designer;
        var plain = await host.Service.CreateAsync("test.direct-stress", project, "Plain", LibraryTestHost.ProvenancedInputs());
        var pack = await host.Service.CreatePackAsync(project, "Plain pack", [plain.Id]);
        host.Principal = FirstChecker;
        Assert.True((await host.Service.CheckPackAsync(pack.Id, "Chris Checker", "Check Co", "ok", CheckOutcome.Accepted)).Succeeded);
        host.Principal = SecondChecker;
        var refused = await host.Service.CountersignPackAsync(pack.Id, "Dana Second", "Check Co", "s", CheckOutcome.Accepted);
        Assert.False(refused.Succeeded);
        Assert.Contains("no member's template requires one", refused.Reason, StringComparison.Ordinal);

        host.Principal = "issuer";
        Assert.True((await host.Service.IssuePackAsync(pack.Id, "ISS-1", "Client")).Succeeded);
    }

    [Fact]
    public void Person_Keys_Ignore_Case_Punctuation_And_Domains()
    {
        Assert.True(CalcInstanceService.SamePerson("Jane Smith", "jane.smith"));
        Assert.True(CalcInstanceService.SamePerson("ACME\\jane.smith", "Jane  Smith"));
        Assert.True(CalcInstanceService.SamePerson("jane.smith@example.org", "JANE_SMITH"));
        Assert.False(CalcInstanceService.SamePerson("Jane Smith", "John Smith"));
        Assert.False(CalcInstanceService.SamePerson(null, "John Smith"));
        Assert.False(CalcInstanceService.SamePerson("  ", "  "));
    }

    private sealed class FixedTemplateCalculator : ICalcCalculator
    {
        public FixedTemplateCalculator(CalcTemplate template) => Template = template;

        public CalcTemplate Template { get; }

        public void Run(CalcRunContext c) { }
    }
}
