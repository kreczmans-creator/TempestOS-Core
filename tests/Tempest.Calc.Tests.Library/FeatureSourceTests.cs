using Tempest.Calc.Library;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Library;

/// <summary>
/// `ADR-0173`, review N2: the application installs per user, so a feature flag is read only from a source the machine's administrator controls: the
/// HKLM policy key on Windows, a root-owned file elsewhere. Nothing the user can write, the install folder included, is a source.
/// </summary>
public sealed class FeatureSourceTests
{
    private const string FilePath = "/etc/tempestos/features.json";

    private sealed class FakeProbe : IUnixFileProbe
    {
        public Dictionary<string, UnixFileFacts> Facts { get; } = new(StringComparer.Ordinal);

        public UnixFileFacts? Probe(string path) => Facts.TryGetValue(path, out var facts) ? facts : null;
    }

    private static readonly UnixFileFacts Trusted = new(0, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead, false);
    private static readonly UnixFileFacts TrustedDirectory = new(0, (UnixFileMode)0b111_101_101, false);

    /// <summary>A real file holding <paramref name="json"/> that the fake probe reports as the given facts, so the content rules run for real.</summary>
    private sealed class Placed : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "tempest-features-" + Guid.NewGuid().ToString("N"));

        public Placed(string json, UnixFileFacts? file = null, UnixFileFacts? directory = null)
        {
            Directory.CreateDirectory(_root);
            Path_ = Path.Combine(_root, "features.json");
            File.WriteAllText(Path_, json);
            Probe.Facts[Path_] = file ?? Trusted;
            Probe.Facts[_root] = directory ?? TrustedDirectory;
        }

        public string Path_ { get; }

        public FakeProbe Probe { get; } = new();

        public UnixFeatureFileSource Source => new(Path_, Probe);

        public void Dispose()
        {
            try { Directory.Delete(_root, true); } catch (IOException) { }
        }
    }

    // ---- the root-owned file ----

    [Theory]
    [InlineData("{ \"Lifting\": true }", true)]
    [InlineData("{ \"Lifting\": \"true\" }", true)]
    [InlineData("{ \"Lifting\": \" TRUE \" }", true)]
    [InlineData("{ // note\n \"Lifting\": true, }", true)]
    [InlineData("{ \"Lifting\": false }", false)]
    [InlineData("{ \"Lifting\": 1 }", false)]
    [InlineData("{ \"Lifting\": \"yes\" }", false)]
    [InlineData("{ \"lifting\": true }", false)]
    [InlineData("{ \"Liftin\": true }", false)]
    [InlineData("{ \"Calc\": { \"Features\": { \"Lifting\": true } } }", false)]
    [InlineData("[ true ]", false)]
    [InlineData("{ not json", false)]
    [InlineData("", false)]
    public void A_Trusted_File_Switches_The_Feature_On_Only_For_An_Exact_True(string json, bool expected)
    {
        using var placed = new Placed(json);
        var flags = CalcFeatureFlags.FromSource(placed.Source, out _);

        Assert.Equal(expected, flags.IsEnabled(CalcFeatures.Lifting));

        if (expected)
            Assert.Contains("root-owned file", flags.SourceOf(CalcFeatures.Lifting), StringComparison.Ordinal);
    }

    private static readonly (string What, UnixFileFacts? File, UnixFileFacts? Directory, string Reason)[] UntrustedCases =
    [
        ("a file owned by an ordinary user", new UnixFileFacts(1000, UnixFileMode.UserRead | UnixFileMode.UserWrite, false), null, "not owned by root"),
        ("a file writable by its group", new UnixFileFacts(0, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupWrite, false), null, "writable by someone other than root"),
        ("a file writable by anyone", new UnixFileFacts(0, (UnixFileMode)0b110_110_110, false), null, "writable by someone other than root"),
        ("a symbolic link", new UnixFileFacts(0, UnixFileMode.UserRead | UnixFileMode.UserWrite, true), null, "symbolic link"),
        ("a directory owned by an ordinary user", null, new UnixFileFacts(1000, (UnixFileMode)0b111_101_101, false), "not owned by root"),
        ("a directory the group can write", null, new UnixFileFacts(0, (UnixFileMode)0b111_111_101, false), "writable by someone other than root"),
        ("a directory anyone can write", null, new UnixFileFacts(0, (UnixFileMode)0b111_111_111, false), "writable by someone other than root"),
        ("a directory that is a symbolic link", null, new UnixFileFacts(0, (UnixFileMode)0b111_101_101, true), "symbolic link"),
    ];

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    public void A_File_Or_Directory_The_User_Could_Write_Is_Ignored_Even_When_It_Says_True(int index)
    {
        var (what, file, directory, reason) = UntrustedCases[index];
        using var placed = new Placed("{ \"Lifting\": true }", file, directory);
        var flags = CalcFeatureFlags.FromSource(placed.Source, out var problem);

        Assert.False(flags.IsEnabled(CalcFeatures.Lifting), what);
        Assert.Contains("every feature stays off", problem, StringComparison.Ordinal);
        Assert.Contains(reason, problem, StringComparison.Ordinal);
    }

    [Fact]
    public void A_Missing_File_Or_Directory_Leaves_Every_Feature_Off()
    {
        var probe = new FakeProbe();
        Assert.False(CalcFeatureFlags.FromSource(new UnixFeatureFileSource(FilePath, probe), out var none).IsEnabled(CalcFeatures.Lifting));
        Assert.Null(none);

        probe.Facts[FilePath] = Trusted; // the file but no examinable directory
        Assert.False(CalcFeatureFlags.FromSource(new UnixFeatureFileSource(FilePath, probe), out var noDirectory).IsEnabled(CalcFeatures.Lifting));
        Assert.Contains("directory could not be examined", noDirectory, StringComparison.Ordinal);
    }

    [Fact]
    public void A_File_Over_The_Size_Limit_Is_Not_Read()
    {
        using var placed = new Placed("{ \"x\": \"" + new string('a', UnixFeatureFileSource.MaxFileBytes) + "\", \"Lifting\": true }");
        Assert.False(CalcFeatureFlags.FromSource(placed.Source, out var problem).IsEnabled(CalcFeatures.Lifting));
        Assert.Contains("larger than", problem, StringComparison.Ordinal);
    }

    [Fact]
    public void The_Real_Probe_Reads_Mode_And_Link_State_And_A_Real_User_Writable_File_Is_Ignored()
    {
        if (OperatingSystem.IsWindows())
            return;

        var root = Path.Combine(Path.GetTempPath(), "tempest-features-real-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            var path = Path.Combine(root, "features.json");
            File.WriteAllText(path, "{ \"Lifting\": true }");
            File.SetUnixFileMode(path, (UnixFileMode)0b110_110_110);
            File.SetUnixFileMode(root, (UnixFileMode)0b111_101_101);

            var probe = new StatFileProbe();
            var facts = probe.Probe(path);
            Assert.NotNull(facts);
            Assert.Equal((UnixFileMode)0b110_110_110, facts.Value.Mode);
            Assert.False(facts.Value.IsSymbolicLink);

            // Whoever owns it, a file anyone can write is not trusted.
            var flags = CalcFeatureFlags.FromSource(new UnixFeatureFileSource(path, probe), out var problem);
            Assert.False(flags.IsEnabled(CalcFeatures.Lifting));
            Assert.Contains("every feature stays off", problem, StringComparison.Ordinal);

            var link = Path.Combine(root, "link.json");
            File.CreateSymbolicLink(link, path);
            Assert.True(probe.Probe(link)!.Value.IsSymbolicLink);
            Assert.Null(probe.Probe(Path.Combine(root, "missing.json")));
        }
        finally
        {
            try { Directory.Delete(root, true); } catch (IOException) { }
        }
    }

    // ---- the Windows policy key ----

    private static Func<string, string, PolicyValue> Registry(PolicyValue value, Action<string, string>? seen = null) => (key, name) =>
    {
        seen?.Invoke(key, name);
        return name == "Lifting" ? value : new PolicyValue(false, false, 0);
    };

    [Theory]
    [InlineData(true, true, 1L, true)]
    [InlineData(true, true, 0L, false)]
    [InlineData(true, true, 2L, false)]
    [InlineData(true, true, -1L, false)]
    [InlineData(true, false, 1L, false)] // a string "1", an expanded string or a QWORD is not a DWORD
    [InlineData(false, false, 0L, false)]
    public void Only_A_Dword_Of_One_Under_The_Machine_Policy_Key_Switches_The_Feature_On(bool exists, bool isDword, long number, bool expected)
    {
        string? key = null;
        var source = new WindowsPolicyFeatureSource(Registry(new PolicyValue(exists, isDword, number), (k, _) => key = k));
        var flags = CalcFeatureFlags.FromSource(source, out var problem);

        Assert.Equal(expected, flags.IsEnabled(CalcFeatures.Lifting));
        Assert.Null(problem);
        Assert.Equal(@"SOFTWARE\Policies\TempestOS\Features", key);

        if (expected)
            Assert.Contains(@"HKLM\SOFTWARE\Policies\TempestOS\Features", flags.SourceOf(CalcFeatures.Lifting), StringComparison.Ordinal);
    }

    [Fact]
    public void An_Unreadable_Policy_Key_Leaves_Every_Feature_Off_And_Says_Why()
    {
        var source = new WindowsPolicyFeatureSource((_, _) => throw new UnauthorizedAccessException());
        var flags = CalcFeatureFlags.FromSource(source, out var problem);

        Assert.False(flags.IsEnabled(CalcFeatures.Lifting));
        Assert.Contains("every feature stays off", problem, StringComparison.Ordinal);
        Assert.Contains(@"HKLM\SOFTWARE\Policies\TempestOS\Features", problem, StringComparison.Ordinal);
    }

    // ---- what is not a source ----

    [Fact]
    public void The_Machine_Source_Ignores_The_Environment_The_Command_Line_The_Current_Directory_And_An_Appsettings_File()
    {
        // This machine has no administrator policy for the test to read; if one is installed, this test has nothing to say.
        if (OperatingSystem.IsWindows() || File.Exists(CalcFeatureSources.UnixFeatureFile))
            return;

        var elsewhere = Path.Combine(Path.GetTempPath(), "tempest-features-cwd-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(elsewhere);
        File.WriteAllText(Path.Combine(elsewhere, "appsettings.json"), "{ \"Calc\": { \"Features\": { \"Lifting\": true } }, \"Lifting\": true }");
        var oldCwd = Directory.GetCurrentDirectory();
        var variables = new[] { "TEMPEST_Calc__Features__Lifting", "Calc__Features__Lifting", "Calc:Features:Lifting", "TEMPEST_Lifting", "Lifting" };
        var oldValues = variables.Select(Environment.GetEnvironmentVariable).ToArray();

        try
        {
            Directory.SetCurrentDirectory(elsewhere);

            foreach (var variable in variables)
                Environment.SetEnvironmentVariable(variable, "true");

            // A beside-the-executable appsettings.json (the earlier source) is ignored too.
            var beside = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            var existed = File.Exists(beside);
            var before = existed ? File.ReadAllText(beside) : null;

            try
            {
                File.WriteAllText(beside, "{ \"Calc\": { \"Features\": { \"Lifting\": true } } }");
                Assert.False(CalcFeatureFlags.FromSource(CalcFeatureSources.ForThisMachine(), out _).IsEnabled(CalcFeatures.Lifting));
            }
            finally
            {
                if (existed)
                    File.WriteAllText(beside, before!);
                else
                    File.Delete(beside);
            }
        }
        finally
        {
            Directory.SetCurrentDirectory(oldCwd);

            for (var i = 0; i < variables.Length; i++)
                Environment.SetEnvironmentVariable(variables[i], oldValues[i]);

            try { Directory.Delete(elsewhere, true); } catch (IOException) { }
        }
    }

    [Fact]
    public void A_Source_That_Throws_Is_No_Source()
    {
        var flags = CalcFeatureFlags.FromSource(new ThrowingSource(), out var problem);

        Assert.False(flags.IsEnabled(CalcFeatures.Lifting));
        Assert.Contains("every feature stays off", problem, StringComparison.Ordinal);
    }

    [Fact]
    public void The_Machine_Source_Is_The_Policy_Key_On_Windows_And_The_Root_Owned_File_Elsewhere()
    {
        var source = CalcFeatureSources.ForThisMachine();
        Assert.IsType(OperatingSystem.IsWindows() ? typeof(WindowsPolicyFeatureSource) : typeof(UnixFeatureFileSource), source);
        Assert.Equal("/etc/tempestos/features.json", CalcFeatureSources.UnixFeatureFile);
        Assert.Equal(@"SOFTWARE\Policies\TempestOS\Features", CalcFeatureSources.WindowsPolicyKey);
    }

    private sealed class ThrowingSource : ICalcFeatureSource
    {
        public CalcFeatureFlags Read(out string? problem) => throw new InvalidOperationException("boom");
    }
}
