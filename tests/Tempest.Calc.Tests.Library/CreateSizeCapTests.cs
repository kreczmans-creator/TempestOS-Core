using Tempest.Calc.Library;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Library;

/// <summary>
/// Found by the C-7 soak (C-7h): saving a calculation past the store's 4 MB cap threw from <c>CreateAsync</c> where <c>UpdateInputsAsync</c> gave a
/// refusal, and the sentence named the calculation by its identifier. A calculation too large to store is now refused the same way whichever act
/// meets the cap, in one sentence that names it by its title, and a refused creation makes nothing (no Evidence record, no register entry).
/// </summary>
public sealed class CreateSizeCapTests
{
    private static readonly string Huge = new('G', CalcInstanceStore.MaxDocumentBytes + 100_000);

    private static IReadOnlyList<CalcInputValue> InputsWith(string grade) =>
        [.. LibraryTestHost.ProvenancedInputs().Select(i => i.Symbol == "grade" ? i with { Text = grade } : i)];

    [Fact]
    public async Task A_Calculation_Past_The_Cap_Is_Refused_By_Its_Title_And_Nothing_Is_Made()
    {
        var host = new LibraryTestHost();
        var project = await host.ProjectAsync();
        var objectsBefore = (await host.Context.Repository.ListAllAsync()).Count;

        var result = await host.Service.TryCreateAsync("test.direct-stress", project, "Pump base frame", InputsWith(Huge));

        Assert.False(result.Succeeded);
        Assert.Null(result.Instance);
        Assert.Contains("'Pump base frame'", result.Reason, StringComparison.Ordinal);
        Assert.Contains("larger than 4 MB", result.Reason, StringComparison.Ordinal);
        Assert.Matches(@"^[^\r\n]{20,300}$", result.Reason!);
        Assert.DoesNotMatch(@"[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}", result.Reason!);

        // Refused before anything exists for it: no calculation, no Evidence record, no register entry.
        Assert.Empty(await host.Store.ListInstancesAsync());
        Assert.Equal(objectsBefore, (await host.Context.Repository.ListAllAsync()).Count);
    }

    [Fact]
    public async Task CreateAsync_Throws_The_Same_Sentence_As_A_Typed_Exception_And_Makes_Nothing()
    {
        var host = new LibraryTestHost();
        var project = await host.ProjectAsync();
        var objectsBefore = (await host.Context.Repository.ListAllAsync()).Count;

        var thrown = await Assert.ThrowsAsync<CalcInstanceTooLargeException>(() => host.Service.CreateAsync("test.direct-stress", project, "Pump base frame", InputsWith(Huge)));

        var result = await host.Service.TryCreateAsync("test.direct-stress", project, "Pump base frame", InputsWith(Huge));
        Assert.Equal(thrown.Message, result.Reason);
        Assert.IsAssignableFrom<InvalidOperationException>(thrown);
        Assert.Empty(await host.Store.ListInstancesAsync());
        Assert.Equal(objectsBefore, (await host.Context.Repository.ListAllAsync()).Count);
    }

    [Fact]
    public async Task Create_And_Update_Refuse_A_Calculation_Past_The_Cap_With_The_Same_Sentence()
    {
        var host = new LibraryTestHost();
        var project = await host.ProjectAsync();
        var created = await host.Service.TryCreateAsync("test.direct-stress", project, "Pump base frame", InputsWith("S275"));
        Assert.True(created.Succeeded, created.Reason);

        var update = await host.Service.UpdateInputsAsync(created.Instance!.Id, InputsWith(Huge));
        var create = await host.Service.TryCreateAsync("test.direct-stress", project, "Pump base frame", InputsWith(Huge));

        Assert.False(update.Succeeded);
        Assert.False(create.Succeeded);
        Assert.Equal(create.Reason, update.Reason);
        Assert.Contains("'Pump base frame'", update.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain(created.Instance.Id.ToString(), update.Reason, StringComparison.OrdinalIgnoreCase);

        // The refused update left the saved calculation as it was.
        Assert.Equal(CalcInstanceStore.Serialise(created.Instance), CalcInstanceStore.Serialise((await host.Store.FindInstanceAsync(created.Instance.Id))!));
    }

    [Fact]
    public async Task A_Calculation_Under_The_Cap_Is_Created_As_Before_Either_Way()
    {
        var host = new LibraryTestHost();
        var project = await host.ProjectAsync();

        var made = await host.Service.CreateAsync("test.direct-stress", project, "Plain", LibraryTestHost.ProvenancedInputs());
        var tried = await host.Service.TryCreateAsync("test.direct-stress", project, "Plain too", LibraryTestHost.ProvenancedInputs());

        Assert.True(tried.Succeeded, tried.Reason);
        Assert.Null(tried.Reason);
        Assert.NotNull(made.EvidenceId);
        Assert.NotNull(made.RegisterObjectId);
        Assert.NotNull(tried.Instance!.EvidenceId);
        Assert.NotNull(tried.Instance.RegisterObjectId);
        Assert.Equal(CalcInstanceStore.Serialise(tried.Instance), CalcInstanceStore.Serialise((await host.Store.FindInstanceAsync(tried.Instance.Id))!));
        Assert.Equal(made.Outputs.Select(o => o.Text), tried.Instance.Outputs.Select(o => o.Text));
    }

    [Fact]
    public async Task A_Calculation_Without_A_Project_Is_Refused_The_Same_Way()
    {
        var host = new LibraryTestHost(withProject: false);
        var result = await host.Service.TryCreateAsync("test.direct-stress", null, "Loose sheet", InputsWith(Huge));

        Assert.False(result.Succeeded);
        Assert.Contains("'Loose sheet'", result.Reason, StringComparison.Ordinal);
        Assert.Empty(await host.Store.ListInstancesAsync());
    }

    [Fact]
    public async Task A_Link_Refusal_On_Create_Is_Also_Given_As_A_Result_By_TryCreate()
    {
        var host = new LibraryTestHost();
        var project = await host.ProjectAsync();
        var inputs = LibraryTestHost.ProvenancedInputs().Select(i => i.Symbol == "F" ? i with { Provenance = CalcProvenance.Drawing(new string('R', CalcProvenance.MaxReferenceLength + 1), "B") } : i).ToList();

        await Assert.ThrowsAsync<CalcLinkRefusedException>(() => host.Service.CreateAsync("test.direct-stress", project, "Long reference", inputs));
        var result = await host.Service.TryCreateAsync("test.direct-stress", project, "Long reference", inputs);

        Assert.False(result.Succeeded);
        Assert.Contains("provenance reference is longer", result.Reason, StringComparison.Ordinal);
        Assert.Empty(await host.Store.ListInstancesAsync());
    }
}
