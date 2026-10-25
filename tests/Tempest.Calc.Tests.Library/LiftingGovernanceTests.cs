using Tempest.Calc.Library;
using Tempest.Calc.Sheets;
using Tempest.Calc.Structural;
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

    /// <summary>The first known-answer case of S8 with a source on every input (Issue demands one).</summary>
    private static IReadOnlyList<CalcInputValue> Inputs()
    {
        var basis = CalcProvenance.Assumed("Worked inputs of the S8 test case.");
        return [.. new LiftingLugCalculator().Template.TestCases[0].Inputs.Select(v => v with { Provenance = basis })];
    }

    private static async Task<CalcActionResult> AttestAsync(LibraryTestHost host, Guid id, string principal = "competent.person", string name = "Pat Competent")
    {
        var before = host.Principal;
        host.Principal = principal;
        var result = await host.Service.AttestAsync(id, name, "CEng MIMechE, LEEA Advanced Inspector", "Reviewed the lug geometry, the material certificate and this calculation.");
        host.Principal = before;
        return result;
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
        Assert.Contains("HKLM\\SOFTWARE\\Policies\\TempestOS\\Features", ex.Message, StringComparison.Ordinal);
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
        Assert.True((await AttestAsync(on, instance.Id)).Succeeded);
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
    public void The_Administrator_Source_Is_The_Only_Switch()
    {
        // A DWORD of 1 under the machine policy key switches the calculator into the catalogue; anything else leaves it withheld.
        static CalcCatalogue Catalogue(PolicyValue value) =>
            new(ShippedCalculators.All, CalcFeatureFlags.FromSource(new WindowsPolicyFeatureSource((_, name) => name == "Lifting" ? value : default), out _));

        Assert.Contains(Catalogue(new PolicyValue(true, true, 1)).Current(), e => e.Template.Id == LiftingLugCalculator.Id);
        Assert.DoesNotContain(Catalogue(default).Current(), e => e.Template.Id == LiftingLugCalculator.Id);
        Assert.DoesNotContain(Catalogue(new PolicyValue(true, false, 1)).Current(), e => e.Template.Id == LiftingLugCalculator.Id);

        // A catalogue has no way to be turned on later.
        var catalogue = new CalcCatalogue(ShippedCalculators.All);
        Assert.False(catalogue.Features.IsEnabled(CalcFeatures.Lifting));
        Assert.DoesNotContain(typeof(CalcFeatureFlags).GetProperties(), p => p.SetMethod is { IsPublic: true });
        Assert.DoesNotContain(typeof(CalcFeatureFlags).GetMethods().Select(m => m.Name), n => n is "Enable" or "Set" or "Add" or "Toggle");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task The_Generic_Evidence_Acts_Cannot_Check_Issue_Or_Revise_The_Evidence_Of_A_Lifting_Calculation(bool flagOn)
    {
        // The Evidence workspace commands call IEvidenceService directly; the evidence of a calculation is managed by its worksheet and they are refused.
        var on = Host(on: true);
        on.Principal = Designer;
        var instance = await on.Service.CreateAsync("struct.lifting", await on.ProjectAsync(), "Lug", Inputs());
        var evidenceId = instance.EvidenceId!.Value;
        var evidence = (Evidence)(await on.Context.Repository.FindAsync(evidenceId))!;
        Assert.Equal(Evidence.CalcWorksheetOwner, evidence.ManagedBy);

        on.Principal = FirstChecker;
        var check = await on.Evidence.RecordCheckAsync(evidenceId, "Chris Checker", "Check Co", "Checked.", CheckOutcome.Accepted);
        Assert.Equal(EvidenceRefusal.ManagedElsewhere, check.Refusal);
        Assert.Equal(EvidenceRefusal.ManagedElsewhere, (await on.Evidence.IssueAsync(evidenceId, "REF-1", "A", "Client")).Refusal);
        Assert.Equal(EvidenceRefusal.ManagedElsewhere, (await on.Evidence.IssueInOneTransactionAsync(evidenceId, "REF-1", "A", "Client")).Refusal);
        Assert.Equal(EvidenceRefusal.ManagedElsewhere, (await on.Evidence.ReviseAsync(evidenceId)).Refusal);
        Assert.Equal(EvidenceStatus.Draft, evidence.Status);

        // With the flag off or on the worksheet's own governance still decides.
        var service = flagOn ? on.Service : new CalcInstanceService(new CalcCatalogue([new LiftingLugCalculator()]), on.Store, on.Evidence, on.Context, renderer: null, applicationVersion: "test") { PrincipalOverride = () => FirstChecker };
        var worksheet = await service.CheckAsync(instance.Id, "Chris Checker", "Check Co", "ok", CheckOutcome.Accepted);
        Assert.False(worksheet.Succeeded);
        Assert.Contains(flagOn ? "competent-person attestation" : "switched off", worksheet.Reason, StringComparison.Ordinal);
        Assert.Equal(EvidenceStatus.Draft, evidence.Status);
    }

    [Fact]
    public async Task The_Startup_Sweep_Marks_Old_Calculation_Evidence_Managed_Even_When_The_Feature_Is_Off()
    {
        // Evidence made before the marker existed, for a lifting calculation and a pack, in a store whose installation has the feature off.
        var on = Host(on: true);
        on.Principal = Designer;
        var project = await on.ProjectAsync();
        var lug = await on.Service.CreateAsync("struct.lifting", project, "Old lug", Inputs());
        var other = await on.Service.CreateAsync("test.direct-stress", project, "Old stress", LibraryTestHost.ProvenancedInputs());
        var pack = await on.Service.CreatePackAsync(project, "Old pack", [other.Id]);
        // Point each at Evidence made the way it was before the marker existed: unmanaged.
        var oldLug = await on.Evidence.CreateAsync(project, "Old lug evidence", EvidenceClassification.Calculation);
        var oldOther = await on.Evidence.CreateAsync(project, "Old stress evidence", EvidenceClassification.Calculation);
        var oldPack = await on.Evidence.CreateAsync(project, "Old pack evidence", EvidenceClassification.Calculation);
        await on.Store.SaveInstanceAsync(lug with { EvidenceId = oldLug.Id, UpdatedAt = lug.UpdatedAt.AddSeconds(1) });
        await on.Store.SaveInstanceAsync(other with { EvidenceId = oldOther.Id, UpdatedAt = other.UpdatedAt.AddSeconds(1) });
        var storedPack = (await on.Store.FindPackAsync(pack.Id))!;
        await on.Store.SavePackAsync(storedPack with { EvidenceId = oldPack.Id, UpdatedAt = storedPack.UpdatedAt.AddSeconds(1) });
        var oldIds = new[] { oldLug.Id, oldOther.Id, oldPack.Id };
        Assert.Null(oldLug.ManagedBy);
        Assert.Null(oldOther.ManagedBy);
        Assert.Null(oldPack.ManagedBy);

        // The service of an installation with the feature off: the next check would be refused before it adopted anything.
        var off = new CalcInstanceService(new CalcCatalogue([new LiftingLugCalculator()]), on.Store, on.Evidence, on.Context, renderer: null, applicationVersion: "test") { PrincipalOverride = () => FirstChecker };
        Assert.Equal(3, await off.AdoptAllEvidenceAsync());

        foreach (var evidenceId in oldIds)
        {
            Assert.Equal(Evidence.CalcWorksheetOwner, ((Evidence)(await on.Context.Repository.FindAsync(evidenceId))!).ManagedBy);
            Assert.Equal(EvidenceRefusal.ManagedElsewhere, (await on.Evidence.RecordCheckAsync(evidenceId, "Chris Checker", "Check Co", "ok", CheckOutcome.Accepted)).Refusal);
        }

        // Idempotent.
        Assert.Equal(3, await off.AdoptAllEvidenceAsync());
    }

    [Fact]
    public async Task Calculation_Evidence_Made_Before_Evidence_Could_Be_Managed_Is_Adopted_At_The_Next_Check()
    {
        var host = Host(on: true);
        host.Principal = Designer;
        var project = await host.ProjectAsync();
        var instance = await host.Service.CreateAsync("test.direct-stress", project, "Old calc", LibraryTestHost.ProvenancedInputs());

        // Evidence created the way it was before: unmanaged, then pointed at by the instance.
        var legacy = await host.Evidence.CreateAsync(project, "Old calc evidence", EvidenceClassification.Calculation);
        await host.Store.SaveInstanceAsync(instance with { EvidenceId = legacy.Id, UpdatedAt = instance.UpdatedAt.AddSeconds(1) });
        Assert.Null(legacy.ManagedBy);

        host.Principal = FirstChecker;
        var checkedOne = await host.Service.CheckAsync(instance.Id, "Chris Checker", "Check Co", "ok", CheckOutcome.Accepted);
        Assert.True(checkedOne.Succeeded, checkedOne.Reason);
        Assert.Equal(Evidence.CalcWorksheetOwner, legacy.ManagedBy);
        Assert.Equal(EvidenceRefusal.ManagedElsewhere, (await host.Evidence.IssueAsync(legacy.Id, "REF-1", "A", "Client")).Refusal);
    }

    [Fact]
    public void The_Engines_Gated_Ids_Are_Tied_To_The_Templates_Feature_Flags_And_The_Registered_Definitions()
    {
        // Every shipped template that names a feature is gated by the engine under the same feature ...
        var flagged = ShippedCalculators.All.Where(c => c.Template.FeatureFlag is { Length: > 0 }).ToList();
        Assert.NotEmpty(flagged);
        Assert.All(flagged, c => Assert.Equal(c.Template.FeatureFlag, Tempest.Core.Calculations.CalculationFeatureGate.FeatureOf(c.Template.Id)));

        // ... and every id the engine gates is either such a template or an engine definition the product registers, under a feature the worksheet
        // catalogue knows. So adding a flagged template without gating it in the engine, or gating an id nothing carries, fails here.
        foreach (var id in Tempest.Core.Calculations.CalculationFeatureGate.GatedIds)
        {
            var feature = Tempest.Core.Calculations.CalculationFeatureGate.FeatureOf(id);
            Assert.Contains(feature, CalcFeatures.All);
            var isFlaggedTemplate = flagged.Any(c => c.Template.Id == id);
            var isRegisteredDefinition = Tempest.Core.Calculations.ProductCalculationCatalogue.CalculationIds.Contains(id);
            Assert.True(isFlaggedTemplate || isRegisteredDefinition, $"'{id}' is gated by the engine but is neither a flagged template nor a registered definition.");
        }

        Assert.Equal(flagged.Select(c => c.Template.Id).Order(StringComparer.Ordinal), Tempest.Core.Calculations.CalculationFeatureGate.GatedIds.Where(id => id != Tempest.Core.Calculations.CalculationFeatureGate.LiftingModuleId));
    }

    [Fact]
    public void The_Engine_And_The_Catalogue_Refuse_The_Lifting_Worksheet_In_The_Same_Words()
    {
        var catalogue = new CalcCatalogue([new LiftingLugCalculator()]);

        Assert.Equal(catalogue.FeatureRefusal("struct.lifting"), Tempest.Core.Calculations.CalculationFeatureGate.RefusalFor("struct.lifting", null));
        Assert.Equal(catalogue.FeatureRefusal("struct.lifting@1.1.0"), Tempest.Core.Calculations.CalculationFeatureGate.RefusalFor("struct.lifting", _ => false));
        Assert.Null(Tempest.Core.Calculations.CalculationFeatureGate.RefusalFor("struct.lifting", feature => feature == CalcFeatures.Lifting));
    }

    // ------------------------------------------------------------------
    // Governance, flag on (the test host only)
    // ------------------------------------------------------------------

    [Fact]
    public async Task Check_Is_Refused_Without_The_Attestation_And_The_Author_Cannot_Attest()
    {
        var host = Host(on: true);
        host.Principal = Designer;
        var project = await host.ProjectAsync();
        var missing = await host.Service.CreateAsync("struct.lifting", project, "No attestation", Inputs());
        host.Principal = FirstChecker;

        var noAttestation = await host.Service.CheckAsync(missing.Id, "Chris Checker", "Check Co", "ok", CheckOutcome.Accepted);
        Assert.False(noAttestation.Succeeded);
        Assert.Contains("competent-person attestation", noAttestation.Reason, StringComparison.Ordinal);

        var author = await AttestAsync(host, missing.Id, Designer, "Someone Competent");
        Assert.False(author.Succeeded);
        Assert.Contains("independent competent-person attestation", author.Reason, StringComparison.Ordinal);

        var byName = await AttestAsync(host, missing.Id, "competent.person", "Designer");
        Assert.False(byName.Succeeded);

        // The checker can reject it without an attestation, and a competent person can attest it.
        var rejected = await host.Service.CheckAsync(missing.Id, "Chris Checker", "Check Co", "No.", CheckOutcome.Rejected);
        Assert.Contains("rejected the calculation", rejected.Reason, StringComparison.Ordinal);
        Assert.True((await AttestAsync(host, missing.Id)).Succeeded);
    }

    [Fact]
    public async Task The_Author_Cannot_Check_And_Issue_Needs_A_Second_Different_Checker()
    {
        var host = Host(on: true);
        host.Principal = Designer;
        var instance = await host.Service.CreateAsync("struct.lifting", await host.ProjectAsync(), "Lug", Inputs());
        Assert.True((await AttestAsync(host, instance.Id)).Succeeded);

        Assert.Contains("needs an independent check", (await host.Service.CheckAsync(instance.Id, "Chris Checker", "Check Co", "ok", CheckOutcome.Accepted)).Reason, StringComparison.Ordinal);

        host.Principal = FirstChecker;
        Assert.True((await host.Service.CheckAsync(instance.Id, "Chris Checker", "Check Co", "ok", CheckOutcome.Accepted)).Succeeded);

        host.Principal = "issuer";
        var one = await host.Service.IssueAsync(instance.Id, "ISS-1", "Client");
        Assert.False(one.Succeeded);
        Assert.Contains("second, independent check", one.Reason, StringComparison.Ordinal);

        host.Principal = FirstChecker;
        Assert.Contains("two different checkers", (await host.Service.CountersignAsync(instance.Id, "Chris Checker", "Check Co", "again", CheckOutcome.Accepted)).Reason, StringComparison.Ordinal);
        host.Principal = Designer;
        Assert.Contains("independent", (await host.Service.CountersignAsync(instance.Id, "Dana Second", "Check Co", "mine", CheckOutcome.Accepted)).Reason, StringComparison.Ordinal);

        host.Principal = SecondChecker;
        Assert.True((await host.Service.CountersignAsync(instance.Id, "Dana Second", "Check Co", "Second check made.", CheckOutcome.Accepted)).Succeeded);

        host.Principal = "issuer";
        var issued = await host.Service.IssueAsync(instance.Id, "ISS-1", "Client");
        Assert.True(issued.Succeeded, issued.Reason);

        var text = issued.Instance!.Issue!.SheetText;
        Assert.Contains("design calculation, not a thorough examination", text, StringComparison.Ordinal);
        Assert.Contains("LOLER", text, StringComparison.Ordinal);
        Assert.Contains("PUWER", text, StringComparison.Ordinal);
        Assert.Contains("Signatures are not authenticated by TempestOS", text, StringComparison.Ordinal);
        Assert.Contains("Chris Checker; second check: Dana Second", text, StringComparison.Ordinal);
        Assert.Contains("Pat Competent", text, StringComparison.Ordinal);
        Assert.Contains("competent person", text, StringComparison.Ordinal);
        Assert.Contains("checklist", text, StringComparison.Ordinal);
        Assert.StartsWith("lifting feature enabled", issued.Instance.Issue.FeatureNote, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_Pack_Holding_A_Lifting_Calculation_Needs_Its_Own_Attestation_And_Second_Check()
    {
        var host = Host(on: true);
        var project = await host.ProjectAsync();
        host.Principal = Designer;
        var unattested = await host.Service.CreateAsync("struct.lifting", project, "Lug unattested", Inputs());
        var pack = await host.Service.CreatePackAsync(project, "Lifting beam", [unattested.Id]);

        host.Principal = FirstChecker;
        var refused = await host.Service.CheckPackAsync(pack.Id, "Chris Checker", "Check Co", "ok", CheckOutcome.Accepted);
        Assert.False(refused.Succeeded);
        Assert.Contains("competent-person attestation", refused.Reason, StringComparison.Ordinal);

        // Fix it: a new calculation with the attestation, in its own pack.
        host.Principal = Designer;
        var lug = await host.Service.CreateAsync("struct.lifting", project, "Lug", Inputs());
        Assert.True((await AttestAsync(host, lug.Id)).Succeeded);
        host.Principal = Designer;
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
