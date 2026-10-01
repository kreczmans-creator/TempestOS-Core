using Tempest.Core.Configuration;
using Tempest.Core.Projects;
using Tempest.Core.Tests.Plugins;

namespace Tempest.Core.Tests.Projects;

/// <summary>
/// <see cref="ProjectFolderService"/> (PO decision 2026-10-01: "Folders
/// should be generated in D:\01 Projects on this computer. It should first
/// search for the customer, if none then make new, likewise project ref,
/// then a standard set of folders within that") — every case runs against
/// a per-test temp root, never the real D: drive.
/// </summary>
public sealed class ProjectFolderServiceTests
{
    [Fact]
    public void Ensure_WithNoExistingFolders_CreatesTheCustomerCodeThenTheProjectIdentifier_AndReportsCreated()
    {
        using var temp = new TempDirectory();
        var service = new ProjectFolderService(new ProjectFolderOptions(temp.Path, []));

        var outcome = service.Ensure(new ProjectFolderRequest("ACME1-BRIDG1", "Apollo Pump Redesign", "ACME1", "Acme Engineering Ltd"));

        var expected = Path.Combine(temp.Path, "ACME1", "ACME1-BRIDG1");
        Assert.Equal(ProjectFolderStatus.Created, outcome.Status);
        Assert.Equal(expected, outcome.ProjectFolder);
        Assert.True(Directory.Exists(expected));
        Assert.Equal(2, outcome.CreatedFolders.Count);
    }

    [Fact]
    public void Ensure_FindsAnExistingCustomerFolderInTheOldCodeAndNameForm_IgnoringCase_RatherThanCreatingASecond()
    {
        using var temp = new TempDirectory();
        Directory.CreateDirectory(Path.Combine(temp.Path, "acmex Acme (old name)"));
        Directory.CreateDirectory(Path.Combine(temp.Path, "ACMEXY Someone Else"));
        Directory.CreateDirectory(Path.Combine(temp.Path, "ACMEX-2 Not this one"));
        var service = new ProjectFolderService(new ProjectFolderOptions(temp.Path, []));

        var outcome = service.Ensure(new ProjectFolderRequest("P-0001", "Pump", "ACMEX", "Acme Engineering Ltd"));

        Assert.Equal(Path.Combine(temp.Path, "acmex Acme (old name)", "P-0001"), outcome.ProjectFolder);
        Assert.Equal(3, Directory.GetDirectories(temp.Path).Length);
    }

    [Fact]
    public void Ensure_PrefersACustomerFolderNamedExactlyTheCode_OverTheOldCodeAndNameForm()
    {
        using var temp = new TempDirectory();
        Directory.CreateDirectory(Path.Combine(temp.Path, "ACME1 Acme Engineering Ltd"));
        Directory.CreateDirectory(Path.Combine(temp.Path, "acme1"));
        Directory.CreateDirectory(Path.Combine(temp.Path, "ACME12"));
        var service = new ProjectFolderService(new ProjectFolderOptions(temp.Path, []));

        var outcome = service.Ensure(new ProjectFolderRequest("ACME1-BRIDG1", "Bridge", "ACME1", "Acme Engineering Ltd"));

        Assert.Equal(Path.Combine(temp.Path, "acme1", "ACME1-BRIDG1"), outcome.ProjectFolder);
        Assert.Equal(3, Directory.GetDirectories(temp.Path).Length);
    }

    [Fact]
    public void Ensure_ReusesTheOldIdentifierAndNameProjectFolder_ButNeverALongerIdentifier()
    {
        using var temp = new TempDirectory();
        var customer = Path.Combine(temp.Path, "ACME1");
        Directory.CreateDirectory(Path.Combine(customer, "ACME1-BRIDG12"));
        Directory.CreateDirectory(Path.Combine(customer, "ACME1-BRIDG1-old"));
        var existing = Path.Combine(customer, "ACME1-BRIDG1 Bridge refurbishment");
        Directory.CreateDirectory(existing);
        var service = new ProjectFolderService(new ProjectFolderOptions(temp.Path, []));

        var outcome = service.Ensure(new ProjectFolderRequest("ACME1-BRIDG1", "Bridge", "ACME1", "Acme Engineering Ltd"));

        Assert.Equal(ProjectFolderStatus.AlreadyExisted, outcome.Status);
        Assert.Equal(existing, outcome.ProjectFolder);
    }

    [Fact]
    public void Ensure_FindsAnExistingCustomerFolderByName_WhenNoFolderStartsWithTheCode()
    {
        using var temp = new TempDirectory();
        Directory.CreateDirectory(Path.Combine(temp.Path, "ACME ENGINEERING LTD"));
        var service = new ProjectFolderService(new ProjectFolderOptions(temp.Path, []));

        var outcome = service.Ensure(new ProjectFolderRequest("P-0002", "Valve", "ACMEX", "Acme Engineering Ltd"));

        Assert.Equal(Path.Combine(temp.Path, "ACME ENGINEERING LTD", "P-0002"), outcome.ProjectFolder);
        Assert.Single(Directory.GetDirectories(temp.Path));
    }

    [Fact]
    public void Ensure_FindsAnExistingProjectFolderByIdentifier_AndCreatesNothing()
    {
        using var temp = new TempDirectory();
        var existing = Path.Combine(temp.Path, "ACMEX Acme", "p-0027 renamed by hand");
        Directory.CreateDirectory(existing);
        Directory.CreateDirectory(Path.Combine(temp.Path, "ACMEX Acme", "P-00271 A different project"));
        var service = new ProjectFolderService(new ProjectFolderOptions(temp.Path, []));

        var outcome = service.Ensure(new ProjectFolderRequest("P-0027", "Apollo Pump Redesign", "ACMEX", "Acme"));

        Assert.Equal(ProjectFolderStatus.AlreadyExisted, outcome.Status);
        Assert.Equal(existing, outcome.ProjectFolder);
        Assert.Empty(outcome.CreatedFolders);
    }

    [Fact]
    public void Ensure_IsIdempotent_TheSecondCallCreatesNothing()
    {
        using var temp = new TempDirectory();
        var service = new ProjectFolderService(new ProjectFolderOptions(temp.Path, ["01 Quotes", @"02 Design\CAD"]));
        var request = new ProjectFolderRequest("P-0003", "Frame", null, "Bloggs & Co");

        var first = service.Ensure(request);
        var second = service.Ensure(request);

        Assert.Equal(ProjectFolderStatus.Created, first.Status);
        Assert.Equal(ProjectFolderStatus.AlreadyExisted, second.Status);
        Assert.Equal(first.ProjectFolder, second.ProjectFolder);
        Assert.True(Directory.Exists(Path.Combine(first.ProjectFolder!, "01 Quotes")));
        Assert.True(Directory.Exists(Path.Combine(first.ProjectFolder!, "02 Design", "CAD")));
        Assert.Equal(Path.Combine(temp.Path, "Bloggs & Co", "P-0003"), first.ProjectFolder);
    }

    [Fact]
    public void Ensure_WithAnEmptyStandardSubfolderSet_CreatesOnlyTheCustomerAndProjectFolders()
    {
        using var temp = new TempDirectory();
        var service = new ProjectFolderService(new ProjectFolderOptions(temp.Path, ProjectFolderOptions.DefaultStandardSubfolders));

        var outcome = service.Ensure(new ProjectFolderRequest("P-0004", "Bracket", "ACMEX", "Acme"));

        Assert.Empty(ProjectFolderOptions.DefaultStandardSubfolders);
        Assert.Empty(Directory.GetDirectories(outcome.ProjectFolder!));
    }

    [Fact]
    public void Ensure_WithNoCustomer_FilesTheProjectUnderTheNoCustomerFolder()
    {
        using var temp = new TempDirectory();
        var service = new ProjectFolderService(new ProjectFolderOptions(temp.Path, []));

        var outcome = service.Ensure(new ProjectFolderRequest("P-0005", "Internal tooling"));

        Assert.Equal(Path.Combine(temp.Path, ProjectFolderService.NoCustomerFolderName, "P-0005"), outcome.ProjectFolder);
    }

    [Fact]
    public void Ensure_FindsTheProjectUnderAnotherCustomerFolder_ByItsIdentifier_BeforeCreatingASecondTree()
    {
        using var temp = new TempDirectory();
        var existing = Path.Combine(temp.Path, ProjectFolderService.NoCustomerFolderName, "P-0011");
        Directory.CreateDirectory(existing);
        Directory.CreateDirectory(Path.Combine(temp.Path, "ACME1", "P-0012"));
        var service = new ProjectFolderService(new ProjectFolderOptions(temp.Path, []));

        // The client was set after the project was first filed under "_No customer".
        var outcome = service.Ensure(new ProjectFolderRequest("P-0011", "Pump", "BRAV1", "Bravo Engineering"));

        Assert.Equal(ProjectFolderStatus.AlreadyExisted, outcome.Status);
        Assert.Equal(existing, outcome.ProjectFolder);
        Assert.Empty(outcome.CreatedFolders);
        Assert.False(Directory.Exists(Path.Combine(temp.Path, "BRAV1")));
    }

    [Fact]
    public void Ensure_ACustomerWithNoCode_NeverAdoptsACodedCustomersFolder_ByName()
    {
        using var temp = new TempDirectory();
        var coded = Path.Combine(temp.Path, "BRAVO");
        Directory.CreateDirectory(coded);
        var service = new ProjectFolderService(new ProjectFolderOptions(temp.Path, []));

        var outcome = service.Ensure(new ProjectFolderRequest("P-0013", "Frame", null, "Bravo"));

        Assert.Equal(Path.Combine(temp.Path, "_Bravo", "P-0013"), outcome.ProjectFolder);
        Assert.Empty(Directory.GetDirectories(coded));
    }

    [Fact]
    public void Ensure_ACodedCustomer_NeverAdoptsTheFolderOfACustomerFiledByAName_StartingWithItsCode()
    {
        using var temp = new TempDirectory();
        var service = new ProjectFolderService(new ProjectFolderOptions(temp.Path, []));

        var byName = service.Ensure(new ProjectFolderRequest("P-0014", "Frame", null, "Bravo Ltd"));
        var byCode = service.Ensure(new ProjectFolderRequest("BRAVO-FRAME1", "Frame", "BRAVO", "Bravo Holdings"));

        Assert.Equal(Path.Combine(temp.Path, "_Bravo Ltd", "P-0014"), byName.ProjectFolder);
        Assert.Equal(Path.Combine(temp.Path, "BRAVO", "BRAVO-FRAME1"), byCode.ProjectFolder);
        Assert.Equal(["P-0014"], Directory.GetDirectories(Path.GetDirectoryName(byName.ProjectFolder)!).Select(Path.GetFileName));
    }

    [Fact]
    public void Ensure_SanitisesInvalidCharacters_AndNeverClimbsOutOfTheProjectFolder()
    {
        using var temp = new TempDirectory();
        var service = new ProjectFolderService(new ProjectFolderOptions(temp.Path, [@"..\..\escape", "Drawings?"]));

        var outcome = service.Ensure(new ProjectFolderRequest(string.Empty, "Pump: stage 2 / \"final\"?", null, "Acme <UK>."));

        var expected = Path.Combine(temp.Path, "Acme -UK-", "Pump- stage 2 - -final--");
        Assert.Equal(expected, outcome.ProjectFolder);
        Assert.True(Directory.Exists(Path.Combine(expected, "escape")));
        Assert.True(Directory.Exists(Path.Combine(expected, "Drawings-")));
        Assert.False(Directory.Exists(Path.Combine(temp.Path, "escape")));
    }

    [Theory]
    [InlineData("CON", "_CON")]
    [InlineData("nul.txt", "_nul.txt")]
    [InlineData("  spaced   out  ", "spaced out")]
    [InlineData("trailing...", "trailing")]
    [InlineData("tab\there", "tab-here")]
    [InlineData("", "")]
    public void SanitiseSegment_FollowsWindowsNamingRules(string raw, string expected) =>
        Assert.Equal(expected, ProjectFolderService.SanitiseSegment(raw));

    [Fact]
    public void Ensure_WithNoRoot_IsUnavailable_AndTouchesNothing()
    {
        var service = new ProjectFolderService(ProjectFolderOptions.Disabled);

        var outcome = service.Ensure(new ProjectFolderRequest("P-0007", "Anything", "ACMEX", "Acme"));

        Assert.Equal(ProjectFolderStatus.Unavailable, outcome.Status);
        Assert.Null(outcome.ProjectFolder);
        Assert.False(string.IsNullOrWhiteSpace(outcome.Message));
    }

    [Fact]
    public void Ensure_WithARootThatIsNotAFullPath_IsUnavailable_AndCreatesNothing()
    {
        var relative = "not-a-full-path-" + Guid.NewGuid().ToString("N");
        var service = new ProjectFolderService(new ProjectFolderOptions(relative, []));

        var outcome = service.Ensure(new ProjectFolderRequest("P-0008", "Anything"));

        Assert.Equal(ProjectFolderStatus.Unavailable, outcome.Status);
        Assert.False(Directory.Exists(relative));
    }

    [Fact]
    public void Ensure_WhenAFileIsInTheWay_ReportsFailed_RatherThanThrowing()
    {
        using var temp = new TempDirectory();
        var blockedRoot = Path.Combine(temp.Path, "blocked");
        File.WriteAllText(blockedRoot, "a file, not a folder");
        var service = new ProjectFolderService(new ProjectFolderOptions(blockedRoot, []));

        var outcome = service.Ensure(new ProjectFolderRequest("P-0009", "Anything"));

        Assert.Equal(ProjectFolderStatus.Failed, outcome.Status);
        Assert.Null(outcome.ProjectFolder);
    }

    [Fact]
    public void Ensure_CreatesTheRootItself_WhenItsDriveExists()
    {
        using var temp = new TempDirectory();
        var root = Path.Combine(temp.Path, "01 Projects");
        var service = new ProjectFolderService(new ProjectFolderOptions(root, []));

        var outcome = service.Ensure(new ProjectFolderRequest("P-0010", "Anything"));

        Assert.Equal(ProjectFolderStatus.Created, outcome.Status);
        Assert.Equal(root, outcome.CreatedFolders[0]);
    }

    [Fact]
    public void QuoteFolderFor_ReturnsTheProjectFolder_OrTheConfiguredQuoteSubfolder()
    {
        using var temp = new TempDirectory();
        var request = new ProjectFolderRequest("P-0011", "Quoted", "ACMEX", "Acme");

        var plain = new ProjectFolderService(new ProjectFolderOptions(temp.Path, [])).QuoteFolderFor(request);
        var withSubfolder = new ProjectFolderService(new ProjectFolderOptions(temp.Path, [], @"01 Commercial\Quotes")).QuoteFolderFor(request);

        var projectFolder = Path.Combine(temp.Path, "ACMEX", "P-0011");
        Assert.Equal(projectFolder, plain);
        Assert.Equal(Path.Combine(projectFolder, "01 Commercial", "Quotes"), withSubfolder);
        Assert.True(Directory.Exists(withSubfolder));
        Assert.Null(new ProjectFolderService(ProjectFolderOptions.Disabled).QuoteFolderFor(request));
    }

    [Fact]
    public void FromConfiguration_DefaultsToTheDDriveRootOnWindows_AndToNothingElsewhere()
    {
        var empty = Configuration();

        Assert.Equal(ProjectFolderOptions.DefaultWindowsRoot, ProjectFolderOptions.FromConfiguration(empty, isWindows: true).Root);
        Assert.Null(ProjectFolderOptions.FromConfiguration(empty, isWindows: false).Root);
        Assert.Empty(ProjectFolderOptions.FromConfiguration(empty, isWindows: true).StandardSubfolders);
        Assert.Null(ProjectFolderOptions.FromConfiguration(empty, isWindows: true).QuoteSubfolder);
    }

    [Fact]
    public void FromConfiguration_ReadsRoot_SubfoldersInEitherShape_AndQuoteSubfolder_AndAnEmptyRootSwitchesOff()
    {
        var configured = Configuration(
            (ProjectFolderOptions.FolderRootKey, @"E:\Work"),
            (ProjectFolderOptions.StandardSubfoldersKey, "01 Quotes; 02 Design"),
            (ProjectFolderOptions.StandardSubfoldersKey + ":1", "04 Site"),
            (ProjectFolderOptions.StandardSubfoldersKey + ":0", "03 Build"),
            (ProjectFolderOptions.QuoteSubfolderKey, "01 Quotes"));

        var options = ProjectFolderOptions.FromConfiguration(configured, isWindows: true);

        Assert.Equal(@"E:\Work", options.Root);
        Assert.Equal(["01 Quotes", "02 Design", "03 Build", "04 Site"], options.StandardSubfolders);
        Assert.Equal("01 Quotes", options.QuoteSubfolder);

        var off = ProjectFolderOptions.FromConfiguration(Configuration((ProjectFolderOptions.FolderRootKey, "")), isWindows: true);
        Assert.Null(off.Root);
        Assert.Null(ProjectFolderOptions.FromConfiguration(Configuration((ProjectFolderOptions.FolderRootKey, "OFF")), isWindows: true).Root);
    }

    private static IConfigurationProvider Configuration(params (string Key, string Value)[] entries) =>
        new ConfigurationBuilder()
            .AddSource(new MemoryConfigurationSource(entries.Select(e => new KeyValuePair<string, string>(e.Key, e.Value))))
            .Build();
}
