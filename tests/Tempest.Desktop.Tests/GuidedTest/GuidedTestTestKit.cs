using Tempest.Calc.Library.ManualTesting;
using Tempest.Calc.Sheets;
using Tempest.Core.Configuration;
using Tempest.Core.Persistence;
using Tempest.Desktop.CalcSheets;
using Tempest.Desktop.GuidedTest;

namespace Tempest.Desktop.Tests.GuidedTest;

/// <summary>A worksheet form stand-in: records what the guided test asked of it and answers with a canned preparation and, when asked, the real engine's run of the example.</summary>
internal sealed class FakeWorksheet : IGuidedWorksheetHost
{
    public List<(string Template, string Inputs, bool Discard)> Prepared { get; } = [];

    public GuidedPrepareOutcome Next { get; set; } = GuidedPrepareOutcome.Ready;

    public string Message { get; set; } = "Open.";

    /// <summary>The run <see cref="ShownRun"/> returns, by template id; empty means the form is not open on it.</summary>
    public Dictionary<string, CalcRunResult> Shown { get; } = new(StringComparer.Ordinal);

    public Dictionary<string, string> Versions { get; } = new(StringComparer.Ordinal);

    public GuidedPrepareResult Prepare(string templateId, string inputsJson, bool discardUnsavedEdits)
    {
        Prepared.Add((templateId, inputsJson, discardUnsavedEdits));
        return new GuidedPrepareResult(Next, Message);
    }

    public CalcRunResult? ShownRun(string templateId) => Shown.GetValueOrDefault(templateId);

    public string? ShippedTemplateVersion(string templateId) => Versions.GetValueOrDefault(templateId);

    /// <summary>The real calculator's run of an example's inputs, as the form would show it.</summary>
    public static CalcRunResult Engine(GuidedTestStep example)
    {
        var calculator = CalcCalculatorComposition.All().Single(c => c.Template.Id == example.TemplateId);
        return CalcRunner.Run(calculator, CalcInputJson.Read(example.InputsJson!, calculator.Template).Inputs);
    }
}

/// <summary>A run service over a real SQLite store in a throw-away folder, the tester and build the tests choose, and a view model on a fake worksheet.</summary>
internal sealed class GuidedKit : IAsyncDisposable
{
    private readonly SqlitePersistenceStore _store;

    private GuidedKit(SqlitePersistenceStore store, string root)
    {
        _store = store;
        Root = root;
        Service = Make();
        Worksheet = new FakeWorksheet();
        Model = new GuidedTestViewModel(Service, Worksheet);
    }

    public string Root { get; }

    public string TesterId { get; set; } = "s.kreczman";

    public string TesterName { get; set; } = "Steven Kreczman";

    public ManualTestRunService Service { get; }

    public FakeWorksheet Worksheet { get; }

    public GuidedTestViewModel Model { get; }

    public static GuidedKit Create()
    {
        var root = WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath();
        var store = new SqlitePersistenceStore(new ConfigurationBuilder()
            .AddSource(new MemoryConfigurationSource(
            [
                new KeyValuePair<string, string>(SqlitePersistenceStore.RootPathConfigurationKey, root),
                new KeyValuePair<string, string>(SqlitePersistenceStore.SynchronousConfigurationKey, SqlitePersistenceStore.RelaxedSynchronousValue),
            ]))
            .Build());
        return new GuidedKit(store, root);
    }

    /// <summary>A second view model over the same store, as a restart of the app would give.</summary>
    public GuidedTestViewModel Restart(IGuidedWorksheetHost? worksheet = null) => new(Make(), worksheet ?? Worksheet);

    /// <summary>A run service over the same store for another script (a run started by an older build).</summary>
    public ManualTestRunService MakeWith(GuidedTestScript script) => new(_store, script, () => "0.25.0 (old)", () => "old", () => (TesterId, TesterName));

    public ManualTestRunService Make() => new(_store, GuidedTestScript.Current, () => "0.26.0 (1a2b3c4)", () => "1a2b3c4", () => (TesterId, TesterName));

    public ValueTask DisposeAsync() => _store.DisposeAsync();
}
