using Tempest.Desktop.GuidedTest;

namespace Tempest.Desktop.Tests.GuidedTest;

/// <summary>
/// Security review SEC-C-09 (Addendum C): the guided results sheet is saved by the dashboard export's path rule. It is always a <c>.md</c> file, and the
/// <c>.md</c> the application adds never replaces a file the save picker did not ask about.
/// </summary>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class GuidedResultsSaveSecurityTests
{
    [Fact]
    public async Task SEC_C_09_The_Results_Sheet_Is_Always_A_Markdown_File_And_An_Added_Extension_Never_Replaces_A_File()
    {
        await using var kit = GuidedKit.Create();
        var model = kit.Model;
        await model.StartNewAsync();
        var folder = Path.Combine(Path.GetTempPath(), "tempest-guided-sec-" + Guid.NewGuid().ToString("N")[..10]);
        Directory.CreateDirectory(folder);

        try
        {
            // A name with another extension, or none, gets ".md": nothing is written under the name typed.
            await model.SaveResultsToPathAsync(Path.Combine(folder, "report.exe"));
            Assert.False(File.Exists(Path.Combine(folder, "report.exe")));
            Assert.StartsWith("# TempestOS guided test results", await File.ReadAllTextAsync(Path.Combine(folder, "report.exe.md")), StringComparison.Ordinal);
            Assert.Equal("Saved the results sheet as 'report.exe.md'.", model.Status);

            // The picker asked about "notes", not "notes.md": an existing notes.md is never replaced by the added extension.
            var existing = Path.Combine(folder, "notes.md");
            await File.WriteAllTextAsync(existing, "keep me");
            await model.SaveResultsToPathAsync(Path.Combine(folder, "notes"));
            Assert.Equal("keep me", await File.ReadAllTextAsync(existing));
            Assert.StartsWith("A file named notes.md already exists in that folder, so nothing was saved.", model.Status, StringComparison.Ordinal);

            // A name the person chose with ".md" is what the picker confirmed: it is written (through a temporary file).
            await model.SaveResultsToPathAsync(existing);
            Assert.StartsWith("# TempestOS guided test results", await File.ReadAllTextAsync(existing), StringComparison.Ordinal);
            Assert.Empty(Directory.GetFiles(folder, "*.tmp"));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Theory]
    [InlineData("sheet.md", "sheet.md")]
    [InlineData("sheet.MD", "sheet.MD")]
    [InlineData("sheet", "sheet.md")]
    [InlineData("sheet.txt", "sheet.txt.md")]
    public void SEC_C_09_The_Path_Rule_Adds_The_Extension_Only_When_It_Is_Missing(string picked, string expected)
    {
        var folder = Path.Combine(Path.GetTempPath(), "tempest-guided-rule-" + Guid.NewGuid().ToString("N")[..10]);
        Assert.Equal(Path.Combine(folder, expected), GuidedTestViewModel.ResultsSheetPath(Path.Combine(folder, picked), out var refusal));
        Assert.Null(refusal);
    }
}
