using Tempest.Core.Events;
using Tempest.Core.Projects;
using Tempest.Core.Settings;
using Tempest.Desktop.Documents;

namespace Tempest.Desktop.Tests;

/// <summary>
/// `v0.25.1` PO decisions: every exported document is revision controlled,
/// named by its project, designator and number — a re-export is the same
/// number at the next revision (<c>TESTY-TRIALX-REG-001-R1</c>, then <c>-R2</c>).
/// </summary>
public sealed class ExportRevisionTests
{
    private sealed class BlankRenderer : IDocumentRenderer<string>
    {
        public string DocumentType => "REGISTER";
        public string TemplateName => "drawing-register";
        public ReadOnlyMemory<byte> Render(string model, OrganisationIdentity? identity = null) => new byte[] { 1, 2, 3 };
    }

    [Fact]
    public async Task ExportingTheSameNumberAgain_TakesTheNextRevision_AndItSurvivesARestart()
    {
        var settings = new SettingsProvider(new InMemoryPersistenceStore(), new EventBus());
        var picker = new StubFilePicker();
        var folder = Directory.CreateTempSubdirectory().FullName;
        var reference = ProjectNumbering.ExportReference("TESTY-TRIALX", ProjectNumbering.Register, "001");

        var exporter = new DocumentExporter(picker) { Revisions = new ExportRevisionLog(settings) };
        picker.SetNextSavePath(Path.Combine(folder, "a.pdf"));
        Assert.True((await exporter.ExportAsync(new BlankRenderer(), "m", reference)).Succeeded);
        Assert.Equal("TESTY-TRIALX-REG-001-R1-drawing-register.pdf", picker.SaveRequests[^1].SuggestedFileName);

        picker.SetNextSavePath(Path.Combine(folder, "b.pdf"));
        await exporter.ExportAsync(new BlankRenderer(), "m", reference);
        Assert.Equal("TESTY-TRIALX-REG-001-R2-drawing-register.pdf", picker.SaveRequests[^1].SuggestedFileName);

        // A cancelled export takes no revision.
        picker.SetNextSavePath(null);
        await exporter.ExportAsync(new BlankRenderer(), "m", reference);

        // A fresh log over the same settings — the next launch — carries on.
        var relaunched = new DocumentExporter(picker) { Revisions = new ExportRevisionLog(settings) };
        picker.SetNextSavePath(Path.Combine(folder, "c.pdf"));
        await relaunched.ExportAsync(new BlankRenderer(), "m", reference);
        Assert.Equal("TESTY-TRIALX-REG-001-R3-drawing-register.pdf", picker.SaveRequests[^1].SuggestedFileName);
    }

    [Fact]
    public async Task ADocumentWithItsOwnRevision_IsNamedByIt_AndNotCounted()
    {
        var settings = new SettingsProvider(new InMemoryPersistenceStore(), new EventBus());
        var picker = new StubFilePicker();
        var log = new ExportRevisionLog(settings);
        var exporter = new DocumentExporter(picker) { Revisions = log };
        picker.SetNextSavePath(Path.Combine(Directory.CreateTempSubdirectory().FullName, "d.pdf"));

        await exporter.ExportAsync(new BlankRenderer(), "m", "TESTY-TRIALX-DOC-004", revision: 3);

        Assert.Equal("TESTY-TRIALX-DOC-004-R3-drawing-register.pdf", picker.SaveRequests[^1].SuggestedFileName);
        Assert.Equal(1, await log.NextAsync("TESTY-TRIALX-DOC-004"));
    }
}
