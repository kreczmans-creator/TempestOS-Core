using Tempest.Calc.Library;
using Tempest.Calc.Sheets;
using Tempest.Calc.Tests.Sheets;
using Tempest.Core.EngineeringData;
using Tempest.Core.Evidence;
using Tempest.Core.Identity;

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

/// <summary>A principal directory that names sign-in ids the way Windows does: the id is a SID, the name is the account.</summary>
internal sealed class FakeDirectory(IReadOnlyDictionary<string, string> names) : IPrincipalDirectory
{
    public string Describe(string? identityId) => identityId is not null && names.TryGetValue(identityId, out var name) ? name : identityId ?? "Unknown";
}

public sealed class GovernanceTests
{
    private static readonly CalcFeatureFlags LiftingOn = new([CalcFeatures.Lifting]);

    private const string Designer = "designer";
    private const string Competent = "competent.person";
    private const string FirstChecker = "checker.one";
    private const string SecondChecker = "checker.two";
    private const string CompetentName = "Pat Competent";
    private const string Qualification = "CEng MIMechE, LEEA Advanced Inspector";
    private const string Statement = "Reviewed the lug geometry, the material certificate and the calculation.";

    private static LibraryTestHost Host(bool on = true, bool gated = true, IPrincipalDirectory? principals = null) =>
        new(calculators: [new GovernedCalculator(gated)], features: on ? LiftingOn : null, principals: principals);

    private static string Id(bool gated = true) => gated ? GovernedCalculator.GatedId : GovernedCalculator.OpenId;

    private static async Task<CalcInstance> NewAsync(LibraryTestHost host, string name = "Lug 1", bool gated = true)
    {
        host.Principal = Designer;
        return await host.Service.CreateAsync(Id(gated), await host.ProjectAsync(), name, LibraryTestHost.ProvenancedInputs());
    }

    private static async Task<CalcActionResult> AttestAsync(LibraryTestHost host, Guid id, string principal = Competent, string name = CompetentName, string qualification = Qualification, string statement = Statement)
    {
        var before = host.Principal;
        host.Principal = principal;
        var result = await host.Service.AttestAsync(id, name, qualification, statement);
        host.Principal = before;
        return result;
    }

    private static async Task<CalcInstance> AttestedAsync(LibraryTestHost host, string name = "Lug 1")
    {
        var instance = await NewAsync(host, name);
        var attested = await AttestAsync(host, instance.Id);
        Assert.True(attested.Succeeded, attested.Reason);
        return attested.Instance!;
    }

    private static async Task<CalcInstance> CheckedAsync(LibraryTestHost host, bool gated = true)
    {
        var instance = gated ? await AttestedAsync(host) : await NewAsync(host, gated: false);
        host.Principal = FirstChecker;
        var check = await host.Service.CheckAsync(instance.Id, "Chris Checker", "Check Co", "Checked.", CheckOutcome.Accepted);
        Assert.True(check.Succeeded, check.Reason);
        return (await host.Store.FindInstanceAsync(instance.Id))!;
    }

    private static async Task<CalcInstance> CountersignedAsync(LibraryTestHost host)
    {
        var instance = await CheckedAsync(host);
        host.Principal = SecondChecker;
        var second = await host.Service.CountersignAsync(instance.Id, "Dana Second", "Check Co", "Second check made.", CheckOutcome.Accepted);
        Assert.True(second.Succeeded, second.Reason);
        return (await host.Store.FindInstanceAsync(instance.Id))!;
    }

    // ------------------------------------------------------------------
    // The feature flag
    // ------------------------------------------------------------------

    [Fact]
    public async Task The_Worksheet_That_Owns_A_Piece_Of_Evidence_Can_Be_Found_From_It()
    {
        var host = Host();
        host.Principal = Designer;
        var project = await host.ProjectAsync();
        var calc = await host.Service.CreateAsync(Id(), project, "Lug 1", LibraryTestHost.ProvenancedInputs());
        var pack = await host.Service.CreatePackAsync(project, "Lug pack", [calc.Id]);

        // A calculation's Evidence leads back to the calculation, a pack's Evidence to the pack; anything else leads nowhere.
        var (instance, none) = await host.Store.FindByEvidenceAsync(calc.EvidenceId!.Value);
        Assert.Equal(calc.Id, instance!.Id);
        Assert.Null(none);

        var packRecord = (await host.Store.FindPackAsync(pack.Id))!;
        var (noInstance, found) = await host.Store.FindByEvidenceAsync(packRecord.EvidenceId!.Value);
        Assert.Null(noInstance);
        Assert.Equal(pack.Id, found!.Id);

        Assert.Equal((null, null), await host.Store.FindByEvidenceAsync(Guid.NewGuid()));
    }

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
            var ex = await Assert.ThrowsAsync<CalcFeatureDisabledException>(() => host.Service.CreateAsync(reference, null, "Lug", LibraryTestHost.ProvenancedInputs()));
            Assert.Contains("switched off", ex.Message, StringComparison.Ordinal);
            Assert.Contains("HKLM\\SOFTWARE\\Policies\\TempestOS\\Features", ex.Message, StringComparison.Ordinal);
            Assert.Contains("review checklist", ex.Message, StringComparison.Ordinal);
        }

        var unknown = await Assert.ThrowsAsync<KeyNotFoundException>(() => host.Service.CreateAsync("test.nothing", null, "x", null));
        Assert.DoesNotContain("switched off", unknown.Message, StringComparison.Ordinal);
        Assert.IsNotType<CalcFeatureDisabledException>(unknown);
    }

    [Fact]
    public void A_Preview_Or_Port_Lookup_Of_The_Withheld_Calculator_Is_Refused()
    {
        var host = Host(on: false);
        Assert.Throws<CalcFeatureDisabledException>(() => host.Service.Preview(GovernedCalculator.GatedId, new CalcInputSet(LibraryTestHost.ProvenancedInputs())));
        Assert.Throws<CalcFeatureDisabledException>(() => host.Service.OutputPorts(GovernedCalculator.GatedId));
        Assert.Throws<CalcFeatureDisabledException>(() => host.Catalogue.Get(GovernedCalculator.GatedId));
    }

    [Fact]
    public async Task Importing_Json_Or_Csv_Inputs_Does_Not_Open_The_Gate()
    {
        var host = Host(on: false);
        var template = new GovernedCalculator().Template;
        var set = new CalcInputSet(LibraryTestHost.ProvenancedInputs());

        var (fromJson, jsonNotices) = CalcInputJson.Read(CalcInputJson.Write(set), template);
        Assert.DoesNotContain(jsonNotices, n => n.Severity == CalcNoticeSeverity.Block);
        var (fromCsv, csvNotices) = CalcCsv.ImportInputs(template, CalcCsv.ExportInputs(template, set));
        Assert.DoesNotContain(csvNotices, n => n.Severity == CalcNoticeSeverity.Block);

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

        var on = new LibraryTestHost(calculators: [new GovernedCalculator()], packTemplates: [template], features: LiftingOn);
        Assert.Null(on.Service.ValidatePackTemplate(template));
    }

    [Fact]
    public async Task An_Existing_Instance_Cannot_Be_Edited_Attested_Checked_Countersigned_Issued_Or_Revised_When_The_Flag_Is_Off()
    {
        var on = Host(on: true);
        var counter = await CountersignedAsync(on);
        var draft = await NewAsync(on, "Draft lug");

        var off = new CalcInstanceService(new CalcCatalogue([new GovernedCalculator(), new DirectStressCalculator()]), on.Store, on.Evidence, on.Context, renderer: null, applicationVersion: "test") { PrincipalOverride = () => FirstChecker };

        void AssertSwitchedOff(CalcActionResult result)
        {
            Assert.False(result.Succeeded);
            Assert.Contains("switched off", result.Reason, StringComparison.Ordinal);
        }

        AssertSwitchedOff(await off.IssueAsync(counter.Id, "ISS-1", "Client"));
        AssertSwitchedOff(await off.ReviseAsync(counter.Id));
        AssertSwitchedOff(await off.CountersignAsync(draft.Id, "Dana Second", "Check Co", "second", CheckOutcome.Accepted));
        AssertSwitchedOff(await off.AttestAsync(draft.Id, CompetentName, Qualification, Statement));
        AssertSwitchedOff(await off.CheckAsync(draft.Id, "Chris Checker", "Check Co", "x", CheckOutcome.Accepted));
        AssertSwitchedOff(await off.UpdateInputsAsync(draft.Id, LibraryTestHost.ProvenancedInputs()));
        AssertSwitchedOff(await off.BindAsync(draft.Id, "F", counter.Id, "sigma"));
        AssertSwitchedOff(await off.BindCellAsync(draft.Id, "x", 0, "y", counter.Id, "sigma"));
        AssertSwitchedOff(await off.AttachExternalEvidenceAsync(draft.Id, "report.pdf", "application/pdf", new byte[] { 1, 2, 3 }));
        await Assert.ThrowsAsync<CalcFeatureDisabledException>(() => off.RunAsync(draft.Id));
        await Assert.ThrowsAsync<CalcFeatureDisabledException>(() => off.CreatePackAsync(draft.ProjectId, "Pack", [draft.Id]));

        Assert.Equal(CalcInstanceStatus.Checked, (await on.Store.FindInstanceAsync(counter.Id))!.Status);
    }

    [Fact]
    public async Task Nothing_Can_Take_A_Value_From_A_Withheld_Calculation_Even_An_Undeclared_Output()
    {
        var on = Host(on: true);
        var project = await on.ProjectAsync();
        on.Principal = Designer;
        var upstream = await on.Service.CreateAsync(GovernedCalculator.GatedId, project, "Upstream lug", LibraryTestHost.ProvenancedInputs());
        var plain = await on.Service.CreateAsync("test.direct-stress", project, "Plain", LibraryTestHost.ProvenancedInputs());

        var off = new CalcInstanceService(new CalcCatalogue([new GovernedCalculator(), new DirectStressCalculator()]), on.Store, on.Evidence, on.Context, renderer: null, applicationVersion: "test") { PrincipalOverride = () => Designer };
        var refused = await off.BindAsync(plain.Id, "F", upstream.Id, "sigma");
        Assert.False(refused.Succeeded);
        Assert.Contains("switched off", refused.Reason, StringComparison.Ordinal);

        var undeclared = await off.BindAsync(plain.Id, "F", upstream.Id, "u");
        Assert.False(undeclared.Succeeded);
        Assert.Contains("switched off", undeclared.Reason, StringComparison.Ordinal);

        // With the flag on the same link is made.
        var bound = await on.Service.BindAsync(plain.Id, "A", upstream.Id, "sigma");
        Assert.False(bound.Succeeded && bound.Reason is not null);
    }

    [Fact]
    public async Task A_Pack_With_A_Withheld_Member_Cannot_Be_Checked_Countersigned_Or_Issued_When_The_Flag_Is_Off()
    {
        var on = Host(on: true);
        var project = await on.ProjectAsync();
        on.Principal = Designer;
        var lug = await on.Service.CreateAsync(GovernedCalculator.GatedId, project, "Lug", LibraryTestHost.ProvenancedInputs());
        Assert.True((await AttestAsync(on, lug.Id)).Succeeded);
        var pack = await on.Service.CreatePackAsync(project, "Lift pack", [lug.Id]);
        on.Principal = FirstChecker;
        Assert.True((await on.Service.CheckPackAsync(pack.Id, "Chris Checker", "Check Co", "ok", CheckOutcome.Accepted)).Succeeded);
        on.Principal = SecondChecker;
        Assert.True((await on.Service.CountersignPackAsync(pack.Id, "Dana Second", "Check Co", "second", CheckOutcome.Accepted)).Succeeded);

        var off = new CalcInstanceService(new CalcCatalogue([new GovernedCalculator(), new DirectStressCalculator()]), on.Store, on.Evidence, on.Context, renderer: null, applicationVersion: "test") { PrincipalOverride = () => "someone.else" };

        var issue = await off.IssuePackAsync(pack.Id, "ISS-1", "Client");
        Assert.False(issue.Succeeded);
        Assert.Contains("switched off", issue.Reason, StringComparison.Ordinal);
        Assert.False((await off.CheckPackAsync(pack.Id, "Chris Checker", "Check Co", "ok", CheckOutcome.Accepted)).Succeeded);
        Assert.False((await off.CountersignPackAsync(pack.Id, "Dana Second", "Check Co", "ok", CheckOutcome.Accepted)).Succeeded);
        Assert.Equal(CalcInstanceStatus.Checked, (await on.Store.FindPackAsync(pack.Id))!.Status);
    }

    // ------------------------------------------------------------------
    // Where the flag is read from
    // ------------------------------------------------------------------

    [Fact]
    public void The_Flag_Is_Off_By_Default_And_Without_A_File()
    {
        Assert.False(CalcFeatureFlags.None.IsEnabled(CalcFeatures.Lifting));
        Assert.False(CalcFeatureFlags.FromSource(CalcFeatureSources.None, out _).IsEnabled(CalcFeatures.Lifting));
        Assert.False(new CalcCatalogue([new GovernedCalculator()]).Features.IsEnabled(CalcFeatures.Lifting));
        Assert.Empty(new CalcCatalogue([new GovernedCalculator()]).Current());
        Assert.True(CalcFeatureFlags.None.IsEnabled(null));
        Assert.True(CalcFeatureFlags.None.IsEnabled(string.Empty));
        Assert.Null(CalcFeatureFlags.None.SourceOf(CalcFeatures.Lifting));
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
        Assert.Null(issued.Instance.Attestation);
        Assert.Null(issued.Instance.Issue!.FeatureNote);
        Assert.DoesNotContain("Optional feature", issued.Instance.Issue.SheetText, StringComparison.Ordinal);

        Assert.False((await host.Service.CountersignAsync(plain.Id, "Dana Second", "Check Co", "second", CheckOutcome.Accepted)).Succeeded);
        Assert.False((await host.Service.AttestAsync(plain.Id, CompetentName, Qualification, Statement)).Succeeded);
    }

    // ------------------------------------------------------------------
    // The competent-person attestation
    // ------------------------------------------------------------------

    [Fact]
    public async Task Check_Is_Refused_Until_A_Competent_Person_Has_Attested()
    {
        var host = Host();
        var instance = await NewAsync(host);
        host.Principal = FirstChecker;

        var refused = await host.Service.CheckAsync(instance.Id, "Chris Checker", "Check Co", "ok", CheckOutcome.Accepted);
        Assert.False(refused.Succeeded);
        Assert.Contains("competent-person attestation", refused.Reason, StringComparison.Ordinal);
        Assert.Equal(CalcInstanceStatus.Draft, (await host.Store.FindInstanceAsync(instance.Id))!.Status);

        // Typed attestation text does not stand in for the attestation: the author filling the fields in changes nothing.
        host.Principal = Designer;
        var typed = LibraryTestHost.ProvenancedInputs().Concat(
        [
            CalcInputValue.Choice(CalcAttestation.NameSymbol, "Dr Pat Competent", CalcProvenance.Assumed("typed")),
            CalcInputValue.Choice(CalcAttestation.QualificationSymbol, Qualification, CalcProvenance.Assumed("typed")),
            CalcInputValue.Choice(CalcAttestation.StatementSymbol, Statement, CalcProvenance.Assumed("typed")),
        ]).ToList();
        Assert.True((await host.Service.UpdateInputsAsync(instance.Id, typed)).Succeeded);
        host.Principal = FirstChecker;
        Assert.Contains("competent-person attestation", (await host.Service.CheckAsync(instance.Id, "Chris Checker", "Check Co", "ok", CheckOutcome.Accepted)).Reason, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("", Qualification, Statement, "name")]
    [InlineData("  ", Qualification, Statement, "name")]
    [InlineData("Pat Competent", "", Statement, "qualification")]
    [InlineData("Pat Competent", "CE", Statement, "qualification")]
    [InlineData("Pat Competent", Qualification, "", "statement")]
    [InlineData("Pat Competent", Qualification, "ok", "statement")]
    [InlineData("Pat Competent", Qualification, "                    ", "statement")]
    public async Task An_Attestation_Needs_A_Name_A_Qualification_And_A_Statement(string name, string qualification, string statement, string field)
    {
        var host = Host();
        var instance = await NewAsync(host);
        var refused = await AttestAsync(host, instance.Id, name: name, qualification: qualification, statement: statement);
        Assert.False(refused.Succeeded);
        Assert.Contains(field, refused.Reason, StringComparison.Ordinal);
        Assert.Null((await host.Store.FindInstanceAsync(instance.Id))!.Attestation);
    }

    [Theory]
    [InlineData("designer", "Pat Competent")]
    [InlineData("DESIGNER", "Pat Competent")]
    [InlineData("competent.person", "Designer")]
    [InlineData("competent.person", "  DESIGNER ")]
    public async Task The_Creator_Cannot_Attest_Their_Own_Calculation_By_Sign_In_Or_By_Name(string principal, string name)
    {
        var host = Host();
        var instance = await NewAsync(host);
        var refused = await AttestAsync(host, instance.Id, principal, name);
        Assert.False(refused.Succeeded);
        Assert.Contains("independent competent-person attestation", refused.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Someone_Who_Edited_The_Inputs_Cannot_Attest_Either()
    {
        var host = Host();
        var instance = await NewAsync(host);
        host.Principal = "edith.editor";
        Assert.True((await host.Service.UpdateInputsAsync(instance.Id, LibraryTestHost.ProvenancedInputs())).Succeeded);

        var refused = await AttestAsync(host, instance.Id, "edith.editor", "Edith Editor");
        Assert.False(refused.Succeeded);
        Assert.Contains("independent competent-person attestation", refused.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task With_Windows_Style_Sign_In_Ids_The_Author_Still_Cannot_Attest_Under_Their_Own_Name()
    {
        // On Windows the principal id is the account SID: the author's id never resembles the name they would type.
        const string authorSid = "S-1-5-21-1111111111-2222222222-3333333333-1001";
        const string attesterSid = "S-1-5-21-1111111111-2222222222-3333333333-1002";
        var directory = new FakeDirectory(new Dictionary<string, string> { [authorSid] = "Jane Smith", [attesterSid] = "Pat Competent" });
        var host = Host(principals: directory);
        host.Principal = authorSid;
        var instance = await host.Service.CreateAsync(GovernedCalculator.GatedId, await host.ProjectAsync(), "Lug", LibraryTestHost.ProvenancedInputs());

        // The author, signed in, typing their own real name or any name.
        host.Principal = authorSid;
        var self = await host.Service.AttestAsync(instance.Id, "Jane Smith", Qualification, Statement);
        Assert.False(self.Succeeded);
        Assert.Contains("independent competent-person attestation", self.Reason, StringComparison.Ordinal);

        // Another person signed in, but typing the author's real name as the competent person.
        host.Principal = attesterSid;
        var typedAuthor = await host.Service.AttestAsync(instance.Id, "jane.smith", Qualification, Statement);
        Assert.False(typedAuthor.Succeeded);
        Assert.Contains("independent competent-person attestation", typedAuthor.Reason, StringComparison.Ordinal);

        // Someone else, in their own name, attests it.
        var good = await host.Service.AttestAsync(instance.Id, "Pat Competent", Qualification, Statement);
        Assert.True(good.Succeeded, good.Reason);
        Assert.Equal(attesterSid, good.Instance!.Attestation!.Principal);

        // The SID-signed author cannot check it as the attester either way round, and the attester cannot check it.
        host.Principal = attesterSid;
        var attesterChecks = await host.Service.CheckAsync(instance.Id, "Pat Competent", "Check Co", "ok", CheckOutcome.Accepted);
        Assert.False(attesterChecks.Succeeded);
        Assert.Contains("independent", attesterChecks.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_Attestation_Is_Made_By_The_Signed_In_Person_And_Is_Printed_With_Its_Source()
    {
        var host = Host();
        var instance = await NewAsync(host);
        host.Principal = Competent;
        var result = await host.Service.AttestAsync(instance.Id, CompetentName, Qualification, Statement);
        Assert.True(result.Succeeded, result.Reason);

        var attestation = result.Instance!.Attestation!;
        Assert.Equal(Competent, attestation.Principal);
        Assert.Equal(CompetentName, attestation.Name);

        var inputs = result.Instance.Inputs.ToDictionary(i => i.Symbol);
        Assert.Equal(CompetentName, inputs[CalcAttestation.NameSymbol].Text);
        Assert.Contains("Attested by competent.person", inputs[CalcAttestation.NameSymbol].Provenance!.Reference ?? inputs[CalcAttestation.NameSymbol].Provenance!.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(Competent, result.Instance.Authors);
    }

    [Fact]
    public async Task Nobody_Signed_In_Cannot_Attest_And_Only_A_Draft_Of_A_Template_That_Asks_Takes_One()
    {
        var host = Host();
        var instance = await NewAsync(host);
        var unknown = await AttestAsync(host, instance.Id, EngineeringDocumentStore.UnknownAuthorPrincipalId);
        Assert.False(unknown.Succeeded);
        Assert.Contains("nobody is signed in", unknown.Reason, StringComparison.Ordinal);

        Assert.False((await host.Service.AttestAsync(Guid.NewGuid(), CompetentName, Qualification, Statement)).Succeeded);

        var checkedOne = await CheckedAsync(Host());
        Assert.NotNull(checkedOne.Attestation);

        var open = new LibraryTestHost(calculators: [new GovernedCalculator(gated: false, attestation: false, secondCheck: false)], features: LiftingOn);
        var plain = await NewAsync(open, gated: false);
        var refused = await AttestAsync(open, plain.Id);
        Assert.False(refused.Succeeded);
        Assert.Contains("does not require one", refused.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_Checked_Calculation_Is_Not_Attested_Afterwards()
    {
        var host = Host();
        var instance = await CheckedAsync(host);
        var refused = await AttestAsync(host, instance.Id, "another.competent", "Another Competent");
        Assert.False(refused.Succeeded);
        Assert.Contains("while it is a draft", refused.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Changing_An_Input_After_The_Attestation_Voids_It()
    {
        var host = Host();
        var instance = await AttestedAsync(host);
        host.Principal = Designer;
        var changed = LibraryTestHost.ProvenancedInputs(f: 15).Concat(instance.Inputs.Where(i => CalcAttestation.Symbols.Contains(i.Symbol))).ToList();
        var updated = await host.Service.UpdateInputsAsync(instance.Id, changed);
        Assert.True(updated.Succeeded, updated.Reason);
        Assert.Null(updated.Instance!.Attestation);

        host.Principal = FirstChecker;
        Assert.Contains("competent-person attestation", (await host.Service.CheckAsync(instance.Id, "Chris Checker", "Check Co", "ok", CheckOutcome.Accepted)).Reason, StringComparison.Ordinal);

        // Attested again on the new inputs, it stands.
        Assert.True((await AttestAsync(host, instance.Id)).Succeeded);
        host.Principal = FirstChecker;
        Assert.True((await host.Service.CheckAsync(instance.Id, "Chris Checker", "Check Co", "ok", CheckOutcome.Accepted)).Succeeded);
    }

    [Fact]
    public async Task The_Attester_Cannot_Be_A_Checker_Of_The_Calculation()
    {
        var host = Host();
        var instance = await AttestedAsync(host);

        // By sign-in and by typed name.
        host.Principal = Competent;
        var bySignIn = await host.Service.CheckAsync(instance.Id, "Someone Else", "Check Co", "ok", CheckOutcome.Accepted);
        Assert.False(bySignIn.Succeeded);
        Assert.Contains("independent of its competent-person attestation", bySignIn.Reason, StringComparison.Ordinal);

        host.Principal = FirstChecker;
        var byName = await host.Service.CheckAsync(instance.Id, CompetentName, "Check Co", "ok", CheckOutcome.Accepted);
        Assert.False(byName.Succeeded);

        Assert.True((await host.Service.CheckAsync(instance.Id, "Chris Checker", "Check Co", "ok", CheckOutcome.Accepted)).Succeeded);

        // And not the second checker either.
        host.Principal = Competent;
        var second = await host.Service.CountersignAsync(instance.Id, "Dana Second", "Check Co", "second", CheckOutcome.Accepted);
        Assert.False(second.Succeeded);
        Assert.Contains("independent of its competent-person attestation", second.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_Checker_Can_Reject_A_Calculation_That_Has_Not_Been_Attested()
    {
        var host = Host();
        var instance = await NewAsync(host);
        host.Principal = FirstChecker;
        var rejected = await host.Service.CheckAsync(instance.Id, "Chris Checker", "Check Co", "The sling angle is wrong.", CheckOutcome.Rejected);
        Assert.False(rejected.Succeeded);
        Assert.Contains("rejected the calculation", rejected.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain("attestation", rejected.Reason, StringComparison.Ordinal);
        Assert.Equal(CalcInstanceStatus.Draft, (await host.Store.FindInstanceAsync(instance.Id))!.Status);
    }

    [Fact]
    public async Task Issue_Re_Reads_The_Attestation_Even_If_The_Stored_Instance_Was_Altered_Underneath()
    {
        var host = Host();
        var counter = await CountersignedAsync(host);

        // The attestation removed from a checked record written straight into the store (the service never would).
        await host.Store.SaveInstanceAsync(counter with { Attestation = null, UpdatedAt = counter.UpdatedAt.AddSeconds(1) });
        host.Principal = "issuer";
        var removed = await host.Service.IssueAsync(counter.Id, "ISS-1", "Client");
        Assert.False(removed.Succeeded);
        Assert.Contains("competent-person attestation", removed.Reason, StringComparison.Ordinal);

        // An attestation by the first checker, forged in.
        var forged = counter with { Attestation = counter.Attestation! with { Principal = FirstChecker, Name = "Chris Checker" }, UpdatedAt = counter.UpdatedAt.AddSeconds(2) };
        await host.Store.SaveInstanceAsync(forged);
        var byChecker = await host.Service.IssueAsync(counter.Id, "ISS-1", "Client");
        Assert.False(byChecker.Succeeded);
        Assert.Contains("also one of its checkers", byChecker.Reason, StringComparison.Ordinal);

        // One by the author.
        await host.Store.SaveInstanceAsync(counter with { Attestation = counter.Attestation! with { Principal = Designer, Name = "Designer" }, UpdatedAt = counter.UpdatedAt.AddSeconds(3) });
        var byAuthor = await host.Service.IssueAsync(counter.Id, "ISS-1", "Client");
        Assert.False(byAuthor.Succeeded);
        Assert.Contains("independent competent-person attestation", byAuthor.Reason, StringComparison.Ordinal);

        // Inputs altered after the attestation.
        await host.Store.SaveInstanceAsync(counter with { Inputs = [.. counter.Inputs.Select(i => i.Symbol == "F" ? i with { Number = 99 } : i)], UpdatedAt = counter.UpdatedAt.AddSeconds(4) });
        var changed = await host.Service.IssueAsync(counter.Id, "ISS-1", "Client");
        Assert.False(changed.Succeeded);
        Assert.Contains("inputs changed after the competent-person attestation", changed.Reason, StringComparison.Ordinal);
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

        host.Principal = FirstChecker;
        var sameSignIn = await host.Service.CountersignAsync(instance.Id, "Someone Else", "Check Co", "second", CheckOutcome.Accepted);
        Assert.False(sameSignIn.Succeeded);
        Assert.Contains("two different checkers", sameSignIn.Reason, StringComparison.Ordinal);

        host.Principal = SecondChecker;
        var sameName = await host.Service.CountersignAsync(instance.Id, "chris  CHECKER", "Check Co", "second", CheckOutcome.Accepted);
        Assert.False(sameName.Succeeded);
        Assert.Contains("two different checkers", sameName.Reason, StringComparison.Ordinal);

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
    public async Task The_Second_Check_Needs_A_Name_An_Organisation_And_A_Statement_And_Comes_After_The_First()
    {
        var host = Host();
        var draft = await AttestedAsync(host);
        host.Principal = SecondChecker;

        var early = await host.Service.CountersignAsync(draft.Id, "Dana Second", "Check Co", "second", CheckOutcome.Accepted);
        Assert.False(early.Succeeded);
        Assert.Contains("after the first check", early.Reason, StringComparison.Ordinal);

        var checkedOne = await CheckedAsync(Host());
        var h2 = Host();
        var again = await CheckedAsync(h2);
        h2.Principal = SecondChecker;
        Assert.False((await h2.Service.CountersignAsync(again.Id, "Dana Second", "Check Co", "", CheckOutcome.Accepted)).Succeeded);
        Assert.False((await h2.Service.CountersignAsync(again.Id, "Dana Second", "Check Co", "  ", CheckOutcome.Accepted)).Succeeded);
        Assert.False((await h2.Service.CountersignAsync(again.Id, "Dana Second", "Check Co", "ok", CheckOutcome.Rejected)).Succeeded);
        Assert.Null((await h2.Store.FindInstanceAsync(again.Id))!.SecondCheck);
        Assert.NotNull(checkedOne);

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
        var instance = await CheckedAsync(host, gated: false);
        host.Principal = SecondChecker;
        var refused = await host.Service.CountersignAsync(instance.Id, "Dana Second", "Check Co", "second", CheckOutcome.Accepted);
        Assert.False(refused.Succeeded);
        Assert.Contains("does not require one", refused.Reason, StringComparison.Ordinal);

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
        Assert.Null(revised.Instance.Attestation);
        Assert.Equal(CalcInstanceStatus.Draft, revised.Instance.Status);
        Assert.Equal("B", revised.Instance.Revision);
    }

    [Fact]
    public async Task A_Second_Check_Forged_Into_The_Store_By_The_First_Checker_Does_Not_Pass_Issue()
    {
        var host = Host();
        var instance = await CheckedAsync(host);

        var forged = instance with { SecondCheck = new CalcCountersignature("Chris Checker", "Check Co", "forged", FirstChecker, DateTimeOffset.UtcNow), UpdatedAt = instance.UpdatedAt.AddSeconds(1) };
        await host.Store.SaveInstanceAsync(forged);

        host.Principal = "issuer";
        var refused = await host.Service.IssueAsync(instance.Id, "ISS-1", "Client");
        Assert.False(refused.Succeeded);
        Assert.Contains("two different checkers", refused.Reason, StringComparison.Ordinal);

        var byAuthor = forged with { SecondCheck = new CalcCountersignature("Designer", "Check Co", "forged", Designer, DateTimeOffset.UtcNow), UpdatedAt = forged.UpdatedAt.AddSeconds(1) };
        await host.Store.SaveInstanceAsync(byAuthor);
        var again = await host.Service.IssueAsync(instance.Id, "ISS-1", "Client");
        Assert.False(again.Succeeded);
        Assert.Contains("independent second check", again.Reason, StringComparison.Ordinal);

        // Signed by the attester.
        await host.Store.SaveInstanceAsync(byAuthor with { SecondCheck = new CalcCountersignature(CompetentName, "Check Co", "forged", Competent, DateTimeOffset.UtcNow), UpdatedAt = byAuthor.UpdatedAt.AddSeconds(1) });
        var byAttester = await host.Service.IssueAsync(instance.Id, "ISS-1", "Client");
        Assert.False(byAttester.Succeeded);
        Assert.Contains("also one of its checkers", byAttester.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task With_Two_Independent_Checkers_And_An_Attestation_The_Sheet_Issues_Prints_Both_Signatures_And_Stamps_The_Feature()
    {
        var host = Host();
        var instance = await CountersignedAsync(host);
        Assert.Equal("Dana Second", instance.SecondCheck!.Name);
        Assert.Equal(SecondChecker, instance.SecondCheck.Principal);

        host.Principal = "issuer";
        var issued = await host.Service.IssueAsync(instance.Id, "ISS-1", "Client");
        Assert.True(issued.Succeeded, issued.Reason);

        var text = issued.Instance!.Issue!.SheetText;
        Assert.Contains("Chris Checker; second check: Dana Second", text, StringComparison.Ordinal);
        Assert.Contains(CompetentName, text, StringComparison.Ordinal);
        Assert.Contains("LEEA Advanced Inspector", text, StringComparison.Ordinal);
        Assert.Contains("Attested by competent.person", text, StringComparison.Ordinal);
        Assert.Equal(Competent, issued.Instance.Attestation!.Principal);
        Assert.Equal("lifting feature enabled (set in code (test or tool host))", issued.Instance.Issue.FeatureNote);
        Assert.Contains("Optional feature: lifting feature enabled", issued.Instance.Issue.SheetText, StringComparison.Ordinal);

        host.Principal = "checker.three";
        Assert.False((await host.Service.CountersignAsync(instance.Id, "Third Person", "Check Co", "third", CheckOutcome.Accepted)).Succeeded);
    }

    [Fact]
    public async Task A_Revision_Starts_Without_The_Earlier_Attestation_Or_Second_Check()
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
        Assert.Null(revised.Instance.Attestation);

        // Nor does the new draft carry the earlier attestation's text, so its sheet does not read as attested (review N6).
        Assert.DoesNotContain(revised.Instance.Inputs, i => CalcAttestation.Symbols.Contains(i.Symbol));
        Assert.DoesNotContain("Attested by", (await host.Service.RunAsync(revised.Instance.Id))!.SheetText, StringComparison.Ordinal);
        Assert.Contains(instance.Inputs, i => i.Symbol == CalcAttestation.NameSymbol); // the issued revision keeps its own

        // A fresh attestation writes them again, and the revision is attested only then.
        host.Principal = Competent;
        var again = await host.Service.AttestAsync(revised.Instance.Id, CompetentName, Qualification, Statement);
        Assert.True(again.Succeeded, again.Reason);
        Assert.Contains("Attested by", (await host.Service.RunAsync(revised.Instance.Id))!.SheetText, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------
    // Packs
    // ------------------------------------------------------------------

    private static async Task<(LibraryTestHost Host, CalcPack Pack, CalcInstance Member)> PackAsync(bool attest = true)
    {
        var host = Host();
        var project = await host.ProjectAsync();
        host.Principal = Designer;
        var lug = await host.Service.CreateAsync(GovernedCalculator.GatedId, project, "Lug", LibraryTestHost.ProvenancedInputs());

        if (attest)
            Assert.True((await AttestAsync(host, lug.Id)).Succeeded);

        host.Principal = Designer;
        var pack = await host.Service.CreatePackAsync(project, "Lift pack", [lug.Id]);
        return (host, pack, lug);
    }

    [Fact]
    public async Task A_Pack_Is_Not_Checked_While_A_Member_Lacks_Its_Attestation_But_Can_Be_Rejected()
    {
        var (host, pack, _) = await PackAsync(attest: false);
        host.Principal = FirstChecker;
        var refused = await host.Service.CheckPackAsync(pack.Id, "Chris Checker", "Check Co", "ok", CheckOutcome.Accepted);
        Assert.False(refused.Succeeded);
        Assert.Contains("Pack member 'Lug'", refused.Reason, StringComparison.Ordinal);
        Assert.Contains("competent-person attestation", refused.Reason, StringComparison.Ordinal);

        var rejected = await host.Service.CheckPackAsync(pack.Id, "Chris Checker", "Check Co", "No.", CheckOutcome.Rejected);
        Assert.False(rejected.Succeeded);
        Assert.Contains("rejected the pack", rejected.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_Pack_Member_Cannot_Be_Attested_By_Its_Own_Author_Or_Checked_By_Its_Attester()
    {
        var (host, pack, member) = await PackAsync(attest: false);
        Assert.False((await AttestAsync(host, member.Id, Designer, "Designer")).Succeeded);
        Assert.True((await AttestAsync(host, member.Id)).Succeeded);

        host.Principal = Competent;
        var byAttester = await host.Service.CheckPackAsync(pack.Id, "Someone", "Check Co", "ok", CheckOutcome.Accepted);
        Assert.False(byAttester.Succeeded);
        Assert.Contains("independent of its competent-person attestation", byAttester.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_Pack_Needs_A_Second_Check_To_Issue_And_Cannot_Have_It_From_The_First_Checker_An_Author_Or_The_Attester()
    {
        var (host, pack, _) = await PackAsync();
        host.Principal = FirstChecker;
        Assert.True((await host.Service.CheckPackAsync(pack.Id, "Chris Checker", "Check Co", "ok", CheckOutcome.Accepted)).Succeeded);

        host.Principal = "issuer";
        var early = await host.Service.IssuePackAsync(pack.Id, "ISS-1", "Client");
        Assert.False(early.Succeeded);
        Assert.Contains("second, independent check", early.Reason, StringComparison.Ordinal);

        host.Principal = FirstChecker;
        Assert.Contains("two different checkers", (await host.Service.CountersignPackAsync(pack.Id, "Other Name", "Check Co", "second", CheckOutcome.Accepted)).Reason, StringComparison.Ordinal);
        host.Principal = SecondChecker;
        Assert.Contains("two different checkers", (await host.Service.CountersignPackAsync(pack.Id, "Chris Checker", "Check Co", "second", CheckOutcome.Accepted)).Reason, StringComparison.Ordinal);
        host.Principal = Designer;
        Assert.False((await host.Service.CountersignPackAsync(pack.Id, "Other Name", "Check Co", "second", CheckOutcome.Accepted)).Succeeded);
        host.Principal = SecondChecker;
        Assert.False((await host.Service.CountersignPackAsync(pack.Id, "Designer", "Check Co", "second", CheckOutcome.Accepted)).Succeeded);
        host.Principal = Competent;
        Assert.Contains("independent of its competent-person attestation", (await host.Service.CountersignPackAsync(pack.Id, "Anyone Else", "Check Co", "second", CheckOutcome.Accepted)).Reason, StringComparison.Ordinal);
        host.Principal = SecondChecker;
        Assert.False((await host.Service.CountersignPackAsync(pack.Id, "Dana Second", "Check Co", "", CheckOutcome.Accepted)).Succeeded);

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
        Assert.Contains("lifting feature enabled", stored.Issue.FeatureNote, StringComparison.Ordinal);
        Assert.Contains("Optional feature: lifting feature enabled", stored.Issue.SheetText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Issue_Of_A_Pack_Holds_The_Second_Checker_To_The_Authors_Of_Every_Member_Not_Only_Those_Already_Looked_At()
    {
        var host = Host();
        var project = await host.ProjectAsync();
        host.Principal = Designer;
        var governed = await host.Service.CreateAsync(GovernedCalculator.GatedId, project, "Lug", LibraryTestHost.ProvenancedInputs());
        Assert.True((await AttestAsync(host, governed.Id)).Succeeded);
        host.Principal = "second.author";
        var plain = await host.Service.CreateAsync("test.direct-stress", project, "Plain", LibraryTestHost.ProvenancedInputs());
        host.Principal = Designer;
        var pack = await host.Service.CreatePackAsync(project, "Mixed pack", [governed.Id, plain.Id]);
        host.Principal = FirstChecker;
        Assert.True((await host.Service.CheckPackAsync(pack.Id, "Chris Checker", "Check Co", "ok", CheckOutcome.Accepted)).Succeeded);

        // The second signature is the author of the member that comes after the governed one; the service would never record it, the store can.
        var stored = (await host.Store.FindPackAsync(pack.Id))!;
        await host.Store.SavePackAsync(stored with { SecondCheck = new CalcCountersignature("Second Author", "Check Co", "forged", "second.author", DateTimeOffset.UtcNow), UpdatedAt = stored.UpdatedAt.AddSeconds(1) });

        host.Principal = "issuer";
        var refused = await host.Service.IssuePackAsync(pack.Id, "ISS-1", "Client");
        Assert.False(refused.Succeeded);
        Assert.Contains("independent second check", refused.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_Rejected_Pack_Second_Check_Stops_Issue_And_The_Pack_Can_Be_Reopened()
    {
        var (host, pack, _) = await PackAsync();
        host.Principal = FirstChecker;
        Assert.True((await host.Service.CheckPackAsync(pack.Id, "Chris Checker", "Check Co", "ok", CheckOutcome.Accepted)).Succeeded);
        host.Principal = SecondChecker;
        var rejected = await host.Service.CountersignPackAsync(pack.Id, "Dana Second", "Check Co", "No, the angle is wrong.", CheckOutcome.Rejected);
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
        var refused = await host.Service.CountersignPackAsync(pack.Id, "Dana Second", "Check Co", "second", CheckOutcome.Accepted);
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
