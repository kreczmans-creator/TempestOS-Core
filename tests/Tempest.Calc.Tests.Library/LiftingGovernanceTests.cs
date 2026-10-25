using Tempest.Calc.Library;
using Tempest.Calc.Sheets;
using Tempest.Calc.Structural;
using Tempest.Core.Configuration;
using Tempest.Core.Evidence;

namespace Tempest.Calc.Tests.Library;

/// <summary>
/// S8 (struct.lifting) end to end through the Library with the real calculator: the feature flag closes every way in while it is off
/// (the catalogue, a direct template id, JSON and CSV import, a pack template, the service API, an instance that already exists), and
/// the attestation and the second check govern Issue when it is on (ADR-0173). The flag is on in this test host only.
/// </summary>
public sealed class LiftingGovernanceTests
{
    private const string Designer = "designer";
    private const string FirstChecker = "checker.one";
    private const string SecondChecker = "checker.two";

    private static readonly CalcFeatureFlags LiftingOn = new([CalcFeatures.Lifting]);

    private static LibraryTestHost Host(bool on, IReadOnlyList<CalcPackTemplate>? packTemplates = null) =>
        new(calculators: [new LiftingLugCalculator()], features: on ? LiftingOn : null, packTemplates: packTemplates);

    /// <summary>The first known-answer case of S8 with a source on every input (Issue demands one), and an attestation.</summary>
    private static IReadOnlyList<CalcInputValue> Inputs(string? attestor = "Dr Pat Competent", string? statement = "Reviewed the lug geometry, the material certificate and this calculation.")
    {
        var basis = CalcProvenance.Assumed("Worked inputs of the S8 test case.");
        var list = new LiftingLugCalculator().Template.TestCases[0].Inputs.Select(v => v with { Provenance = basis }).ToList();

        if (attestor is not null)
        {
            list.Add(CalcInputValue.Choice(CalcAttestation.NameSymbol, attestor, basis));
            list.Add(CalcInputValue.Choice(CalcAttestation.QualificationSymbol, "CEng MIMechE, LEEA Advanced Inspector", basis));
        }

        if (statement is not null)
            list.Add(CalcInputValue.Choice(CalcAttestation.StatementSymbol, statement, basis));

        return list;
    }

    // ------------------------------------------------------------------
    // The gate, flag off
    // ------------------------------------------------------------------

    [Fact]
    public void The_Shipped_Composition_Has_No_Lifting_Calculator_Without_The_Flag_And_Exactly_One_More_With_It()
    {
        var off = new CalcCatalogue(ShippedCalculators.All);
        var on = new CalcCatalogue(ShippedCalculators.All, LiftingOn);

        Assert.DoesNotContain(off.Current(), e => e.Template.Id == LiftingLugCalculator.Id);
        Assert.DoesNotContain(off.All(), e => e.Template.Id == LiftingLugCalculator.Id);
        Assert.DoesNotContain(off.ByDiscipline(CalcDiscipline.Structural), e => e.Template.Id == LiftingLugCalculator.Id);
        Assert.Null(off.Find(LiftingLugCalculator.Id));
        Assert.Null(off.Find("struct.lifting@1.0.0"));
        Assert.True(off.IsWithheld(LiftingLugCalculator.Id));

        Assert.Contains(on.Current(), e => e.Template.Id == LiftingLugCalculator.Id);
        Assert.Equal(off.Current().Count + 1, on.Current().Count);

        // Ctrl+K entries are one per Current() entry, filed under its discipline: none for S8 with the flag off.
        Assert.DoesNotContain(off.Current().Select(e => $"calc.worksheet.{e.Template.Id}"), id => id == "calc.worksheet.struct.lifting");
        Assert.Contains(on.Current().Select(e => $"calc.worksheet.{e.Template.Id}"), id => id == "calc.worksheet.struct.lifting");
    }

    [Fact]
    public void No_Other_Shipped_Calculator_Is_Gated_Or_Asks_For_Extra_Governance()
    {
        foreach (var calculator in ShippedCalculators.All.Where(c => c.Template.Id != LiftingLugCalculator.Id))
        {
            var t = calculator.Template;
            Assert.Null(t.FeatureFlag);
            Assert.False(t.RequiresAttestation, t.Id);
            Assert.False(t.RequiresSecondCheck, t.Id);
            var canonical = CalcTemplateHasher.CanonicalText(t, []);
            Assert.DoesNotContain("feature=", canonical, StringComparison.Ordinal);
            Assert.DoesNotContain("requires-", canonical, StringComparison.Ordinal);
        }

        var s8 = new LiftingLugCalculator().Template;
        Assert.Equal(CalcFeatures.Lifting, s8.FeatureFlag);
        Assert.Contains("feature=lifting", CalcTemplateHasher.CanonicalText(s8, []), StringComparison.Ordinal);
    }

    [Fact]
    public void No_Shipped_Pack_Template_Names_The_Lifting_Calculator()
    {
        Assert.DoesNotContain(CalcPackTemplates.All.SelectMany(p => p.Steps), step => step.TemplateId.StartsWith("struct.lifting", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("struct.lifting")]
    [InlineData("struct.lifting@1.0.0")]
    public async Task A_Direct_Template_Id_Cannot_Create_A_Calculation(string reference)
    {
        var host = Host(on: false);
        var project = await host.ProjectAsync();

        var ex = await Assert.ThrowsAsync<CalcFeatureDisabledException>(() => host.Service.CreateAsync(reference, project, "Lug", Inputs()));
        Assert.Contains("switched off", ex.Message, StringComparison.Ordinal);
        Assert.Contains("Calc:Features:Lifting", ex.Message, StringComparison.Ordinal);
        Assert.Contains("Lifting lugs and spreader beams", ex.Message, StringComparison.Ordinal);
        Assert.Empty(await host.Store.ListInstancesAsync(project));
    }

    [Fact]
    public async Task Json_And_Csv_Import_Cannot_Create_A_Lifting_Calculation()
    {
        var host = Host(on: false);
        var template = new LiftingLugCalculator().Template;
        var set = new CalcInputSet(Inputs());

        var (fromJson, jsonNotices) = CalcInputJson.Read(CalcInputJson.Write(set), template);
        Assert.DoesNotContain(jsonNotices, n => n.Severity == CalcNoticeSeverity.Block);
        var (fromCsv, csvNotices) = CalcCsv.ImportInputs(template, CalcCsv.ExportInputs(template, set));
        Assert.DoesNotContain(csvNotices, n => n.Severity == CalcNoticeSeverity.Block);
        Assert.Equal(fromJson.Values.Count, fromCsv.Values.Count);

        await Assert.ThrowsAsync<CalcFeatureDisabledException>(() => host.Service.CreateAsync("struct.lifting", null, "From JSON", [.. fromJson.Values]));
        await Assert.ThrowsAsync<CalcFeatureDisabledException>(() => host.Service.CreateAsync("struct.lifting", null, "From CSV", [.. fromCsv.Values]));
        Assert.Empty(await host.Store.ListInstancesAsync(null));
    }

    [Fact]
    public async Task A_Pack_Template_With_A_Lifting_Step_Cannot_Be_Instantiated()
    {
        var template = new CalcPackTemplate("lifting-beam-test", "Lifting beam", "Lug then beam.", [new CalcPackStep("lug", "struct.lifting", "Lug 1", Inputs())], []);
        var host = Host(on: false, packTemplates: [template]);
        var project = await host.ProjectAsync();

        Assert.Contains("switched off", host.Service.ValidatePackTemplate(template), StringComparison.Ordinal);
        var (result, refusal) = await host.Service.InstantiatePackTemplateAsync(template, project, "Lifting beam pack");
        Assert.Null(result);
        Assert.Contains("switched off", refusal, StringComparison.Ordinal);
        Assert.Empty(await host.Store.ListInstancesAsync(project));
        Assert.Empty(await host.Store.ListPacksAsync(project));

        // The same template works with the flag on, in the test host.
        var on = Host(on: true, packTemplates: [template]);
        Assert.Null(on.Service.ValidatePackTemplate(template));
    }

    [Fact]
    public void The_Service_Api_Refuses_A_Preview_Ports_And_A_Catalogue_Get_While_The_Flag_Is_Off()
    {
        var host = Host(on: false);
        Assert.Throws<CalcFeatureDisabledException>(() => host.Service.Preview("struct.lifting", new CalcInputSet(Inputs())));
        Assert.Throws<CalcFeatureDisabledException>(() => host.Service.OutputPorts("struct.lifting"));
        Assert.Throws<CalcFeatureDisabledException>(() => host.Catalogue.Get("struct.lifting"));
        Assert.Throws<CalcFeatureDisabledException>(() => host.Catalogue.Get("struct.lifting@1.0.0"));
        Assert.Null(host.Catalogue.Find("struct.lifting"));
    }

    [Fact]
    public async Task A_Lifting_Calculation_That_Already_Exists_Cannot_Be_Issued_When_The_Flag_Is_Switched_Off()
    {
        var on = Host(on: true);
        on.Principal = Designer;
        var instance = await on.Service.CreateAsync("struct.lifting", await on.ProjectAsync(), "Lug", Inputs());
        on.Principal = FirstChecker;
        Assert.True((await on.Service.CheckAsync(instance.Id, "Chris Checker", "Check Co", "ok", CheckOutcome.Accepted)).Succeeded);
        on.Principal = SecondChecker;
        Assert.True((await on.Service.CountersignAsync(instance.Id, "Dana Second", "Check Co", "second", CheckOutcome.Accepted)).Succeeded);

        // Everything is in place to issue it, but the installation has the feature off.
        var off = new CalcInstanceService(new CalcCatalogue([new LiftingLugCalculator()]), on.Store, on.Evidence, on.Context, renderer: null, applicationVersion: "test") { PrincipalOverride = () => "issuer" };
        var refused = await off.IssueAsync(instance.Id, "ISS-1", "Client");
        Assert.False(refused.Succeeded);
        Assert.Contains("switched off", refused.Reason, StringComparison.Ordinal);
        Assert.Equal(CalcInstanceStatus.Checked, (await on.Store.FindInstanceAsync(instance.Id))!.Status);

        // And a record written into the store around the service, with the flag off, is just as unissuable.
        var forged = instance with { Id = Guid.NewGuid(), Name = "Forged", Status = CalcInstanceStatus.Checked, UpdatedAt = instance.UpdatedAt.AddSeconds(1) };
        await on.Store.SaveInstanceAsync(forged);
        Assert.False((await off.IssueAsync(forged.Id, "ISS-2", "Client")).Succeeded);

        // Switched back on, the first one issues.
        on.Principal = "issuer";
        var issued = await on.Service.IssueAsync(instance.Id, "ISS-1", "Client");
        Assert.True(issued.Succeeded, issued.Reason);
    }

    [Fact]
    public void The_Installation_Configuration_Is_The_Only_Switch()
    {
        IConfigurationProvider Config(string? value) => new ConfigurationBuilder().AddSource(new MemoryConfigurationSource(value is null ? [] : [new KeyValuePair<string, string>("Calc:Features:Lifting", value)])).Build();

        Assert.DoesNotContain(new CalcCatalogue(ShippedCalculators.All, CalcFeatureFlags.FromConfiguration(Config(null))).Current(), e => e.Template.Id == LiftingLugCalculator.Id);
        Assert.DoesNotContain(new CalcCatalogue(ShippedCalculators.All, CalcFeatureFlags.FromConfiguration(Config("false"))).Current(), e => e.Template.Id == LiftingLugCalculator.Id);
        Assert.DoesNotContain(new CalcCatalogue(ShippedCalculators.All, CalcFeatureFlags.FromConfiguration(Config("1"))).Current(), e => e.Template.Id == LiftingLugCalculator.Id);
        Assert.Contains(new CalcCatalogue(ShippedCalculators.All, CalcFeatureFlags.FromConfiguration(Config("true"))).Current(), e => e.Template.Id == LiftingLugCalculator.Id);

        // A catalogue has no way to be turned on later.
        var catalogue = new CalcCatalogue(ShippedCalculators.All);
        Assert.False(catalogue.Features.IsEnabled(CalcFeatures.Lifting));
        Assert.DoesNotContain(typeof(CalcFeatureFlags).GetProperties(), p => p.SetMethod is { IsPublic: true });
        Assert.DoesNotContain(typeof(CalcFeatureFlags).GetMethods().Select(m => m.Name), n => n is "Enable" or "Set" or "Add" or "Toggle");
    }

    // ------------------------------------------------------------------
    // Governance, flag on (the test host only)
    // ------------------------------------------------------------------

    [Fact]
    public async Task Check_Is_Refused_Without_The_Attestation_And_When_The_Author_Attests()
    {
        var host = Host(on: true);
        host.Principal = Designer;
        var project = await host.ProjectAsync();
        var missing = await host.Service.CreateAsync("struct.lifting", project, "No attestation", Inputs(attestor: null, statement: null));
        var byAuthor = await host.Service.CreateAsync("struct.lifting", project, "Author attests", Inputs(attestor: "Designer"));
        var weak = await host.Service.CreateAsync("struct.lifting", project, "Weak statement", Inputs(statement: "fine"));
        host.Principal = FirstChecker;

        var noAttestation = await host.Service.CheckAsync(missing.Id, "Chris Checker", "Check Co", "ok", CheckOutcome.Accepted);
        Assert.False(noAttestation.Succeeded);
        Assert.Contains("competent-person attestation", noAttestation.Reason, StringComparison.Ordinal);

        var author = await host.Service.CheckAsync(byAuthor.Id, "Chris Checker", "Check Co", "ok", CheckOutcome.Accepted);
        Assert.False(author.Succeeded);
        Assert.Contains("created or edited", author.Reason, StringComparison.Ordinal);

        var statement = await host.Service.CheckAsync(weak.Id, "Chris Checker", "Check Co", "ok", CheckOutcome.Accepted);
        Assert.False(statement.Succeeded);
        Assert.Contains("attest_statement", statement.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_Author_Cannot_Check_And_Issue_Needs_A_Second_Different_Checker()
    {
        var host = Host(on: true);
        host.Principal = Designer;
        var instance = await host.Service.CreateAsync("struct.lifting", await host.ProjectAsync(), "Lug", Inputs());

        // The first check must be independent of the author.
        Assert.Contains("needs an independent check", (await host.Service.CheckAsync(instance.Id, "Chris Checker", "Check Co", "ok", CheckOutcome.Accepted)).Reason, StringComparison.Ordinal);

        host.Principal = FirstChecker;
        Assert.True((await host.Service.CheckAsync(instance.Id, "Chris Checker", "Check Co", "ok", CheckOutcome.Accepted)).Succeeded);

        // One checked signature is not enough.
        host.Principal = "issuer";
        var one = await host.Service.IssueAsync(instance.Id, "ISS-1", "Client");
        Assert.False(one.Succeeded);
        Assert.Contains("second, independent check", one.Reason, StringComparison.Ordinal);

        // Not the first checker again, not the author.
        host.Principal = FirstChecker;
        Assert.Contains("two different checkers", (await host.Service.CountersignAsync(instance.Id, "Chris Checker", "Check Co", "again", CheckOutcome.Accepted)).Reason, StringComparison.Ordinal);
        host.Principal = Designer;
        Assert.Contains("independent", (await host.Service.CountersignAsync(instance.Id, "Dana Second", "Check Co", "mine", CheckOutcome.Accepted)).Reason, StringComparison.Ordinal);

        // A different, independent second checker releases it.
        host.Principal = SecondChecker;
        Assert.True((await host.Service.CountersignAsync(instance.Id, "Dana Second", "Check Co", "Second check made.", CheckOutcome.Accepted)).Succeeded);

        host.Principal = "issuer";
        var issued = await host.Service.IssueAsync(instance.Id, "ISS-1", "Client");
        Assert.True(issued.Succeeded, issued.Reason);

        var text = issued.Instance!.Issue!.SheetText;
        Assert.Contains("design calculation, not a thorough examination", text, StringComparison.Ordinal);
        Assert.Contains("LOLER", text, StringComparison.Ordinal);
        Assert.Contains("PUWER", text, StringComparison.Ordinal);
        Assert.Contains("Chris Checker; second check: Dana Second", text, StringComparison.Ordinal);
        Assert.Contains("Dr Pat Competent", text, StringComparison.Ordinal);
        Assert.Contains("competent person", text, StringComparison.Ordinal);
        Assert.Contains("checklist", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_Pack_Holding_A_Lifting_Calculation_Needs_Its_Own_Attestation_And_Second_Check()
    {
        var host = Host(on: true);
        var project = await host.ProjectAsync();
        host.Principal = Designer;
        var unattested = await host.Service.CreateAsync("struct.lifting", project, "Lug unattested", Inputs(attestor: null, statement: null));
        var pack = await host.Service.CreatePackAsync(project, "Lifting beam", [unattested.Id]);

        host.Principal = FirstChecker;
        var refused = await host.Service.CheckPackAsync(pack.Id, "Chris Checker", "Check Co", "ok", CheckOutcome.Accepted);
        Assert.False(refused.Succeeded);
        Assert.Contains("competent-person attestation", refused.Reason, StringComparison.Ordinal);

        // Fix it: a new calculation with the attestation, in its own pack.
        host.Principal = Designer;
        var lug = await host.Service.CreateAsync("struct.lifting", project, "Lug", Inputs());
        var good = await host.Service.CreatePackAsync(project, "Lifting beam 2", [lug.Id]);
        host.Principal = FirstChecker;
        Assert.True((await host.Service.CheckPackAsync(good.Id, "Chris Checker", "Check Co", "ok", CheckOutcome.Accepted)).Succeeded);

        host.Principal = "issuer";
        var withoutSecond = await host.Service.IssuePackAsync(good.Id, "ISS-1", "Client");
        Assert.False(withoutSecond.Succeeded);
        Assert.Contains("second, independent check", withoutSecond.Reason, StringComparison.Ordinal);

        host.Principal = SecondChecker;
        Assert.True((await host.Service.CountersignPackAsync(good.Id, "Dana Second", "Check Co", "second", CheckOutcome.Accepted)).Succeeded);

        host.Principal = "issuer";
        var issued = await host.Service.IssuePackAsync(good.Id, "ISS-1", "Client");
        Assert.True(issued.Succeeded, issued.Reason);
        Assert.Equal(CalcInstanceStatus.Issued, (await host.Store.FindInstanceAsync(lug.Id))!.Status);
    }

    [Fact]
    public async Task The_Lifting_Calculation_Does_Not_Change_The_Governance_Of_Any_Other_Calculator_In_The_Same_Host()
    {
        var host = Host(on: true);
        host.Principal = Designer;
        var plain = await host.Service.CreateAsync("test.direct-stress", await host.ProjectAsync(), "Plain", LibraryTestHost.ProvenancedInputs());
        host.Principal = FirstChecker;
        Assert.True((await host.Service.CheckAsync(plain.Id, "Chris Checker", "Check Co", "ok", CheckOutcome.Accepted)).Succeeded);
        Assert.True((await host.Service.IssueAsync(plain.Id, "ISS-1", "Client")).Succeeded);
    }
}
