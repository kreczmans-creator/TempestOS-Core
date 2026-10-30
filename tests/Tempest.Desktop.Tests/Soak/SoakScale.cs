namespace Tempest.Desktop.Tests.Soak;

/// <summary>
/// How big one soak is (Addendum C, C-7). <see cref="Full"/> is the specification's numbers (500 calculation instances, chains of
/// depth 20, 100 revise and supersede cycles, 1,000 Monte Carlo runs, imports at their size caps) and runs only when
/// <c>TEMPEST_SOAK=full</c> is set; <see cref="Ci"/> is about a tenth of that and always runs as an ordinary test. The caps a
/// file or a request may not pass are never scaled: the refusal checks run at the real cap in both modes.
/// </summary>
internal sealed record SoakScale
{
    /// <summary>The environment variable that switches the full soak on (value <c>full</c>).</summary>
    public const string EnvironmentVariable = "TEMPEST_SOAK";

    /// <summary>The environment variable naming a file the soak appends one progress line per step to, so a long run can be watched.</summary>
    public const string ProgressVariable = "TEMPEST_SOAK_LOG";

    public required string Name { get; init; }

    /// <summary>Whether the store runs at <c>PRAGMA synchronous = FULL</c> (the product's setting) rather than the <c>NORMAL</c> every other Desktop test uses.</summary>
    public required bool FullDurability { get; init; }

    /// <summary>Scenario 1: calculation instances created across every template, each run and saved again.</summary>
    public required int Instances { get; init; }

    /// <summary>Scenario 2: independent chains of linked calculations (two chains share each project, to prove one chain's change leaves the other fresh).</summary>
    public required int Chains { get; init; }

    /// <summary>Scenario 2: links in each chain; <see cref="Tempest.Calc.Library.CalcInstanceService.MaxChainDepth"/> is the cap, so a chain has depth + 1 calculations.</summary>
    public required int ChainDepth { get; init; }

    /// <summary>Scenario 3: revise, check, issue and supersede cycles on one calculation.</summary>
    public required int Cycles { get; init; }

    /// <summary>Scenario 4: Monte Carlo runs of the engine service, each at its default sample count.</summary>
    public required int EngineMonteCarloRuns { get; init; }

    /// <summary>Scenario 4: Monte Carlo runs through a real calculator (every sample is a whole calculator run).</summary>
    public required int CalculatorMonteCarloRuns { get; init; }

    /// <summary>Scenario 4: samples in each calculator Monte Carlo run.</summary>
    public required int CalculatorMonteCarloSamples { get; init; }

    /// <summary>Scenario 4: whether one calculator run is made at the calculator service's own sample cap (100,000 whole calculator runs).</summary>
    public required bool RunAtCalculatorCap { get; init; }

    /// <summary>Scenario 4: tornado sweeps.</summary>
    public required int TornadoSweeps { get; init; }

    /// <summary>Scenario 5: the share of each import cap the accepted "largest" files reach (1 is the cap itself).</summary>
    public required double ImportFraction { get; init; }

    /// <summary>Scenario 6: times the licensed reference record is revised (each attempt to release it from the licence lock failing first).</summary>
    public required int RecordRevisions { get; init; }

    /// <summary>Scenario 7: times the demonstration project is installed and removed.</summary>
    public required int DemoCycles { get; init; }

    /// <summary>Scenario 8: reference records in the store when the dashboard is built on a full store.</summary>
    public required int DashboardRecords { get; init; }

    /// <summary>Scenario 8: finished guided runs (of the whole script) in the store when the dashboard is built on a full store.</summary>
    public required int DashboardRuns { get; init; }

    /// <summary>Scenario 8: times the dashboard is built on the full store (and a fifth of that on the seeded one).</summary>
    public required int DashboardBuilds { get; init; }

    /// <summary>Scenario 9: guided runs started, filled in step by step (all 110 steps) and finished through the service; the run store's cap is 1,000, and a smaller number here is topped up to it directly.</summary>
    public required int GuidedRuns { get; init; }

    /// <summary>The scale <c>TEMPEST_SOAK=full</c> selects: the specification's numbers.</summary>
    public static SoakScale Full { get; } = new()
    {
        Name = "full",
        FullDurability = true,
        Instances = 500,
        Chains = 20,
        ChainDepth = Tempest.Calc.Library.CalcInstanceService.MaxChainDepth,
        Cycles = 100,
        EngineMonteCarloRuns = 1_000,
        CalculatorMonteCarloRuns = 1_000,
        CalculatorMonteCarloSamples = 10_000,
        RunAtCalculatorCap = true,
        TornadoSweeps = 1_000,
        ImportFraction = 1.0,
        RecordRevisions = 100,
        DemoCycles = 20,
        DashboardRecords = 10_000,
        DashboardRuns = 1_000,
        DashboardBuilds = 40,
        GuidedRuns = 1_000,
    };

    /// <summary>The always-on scale: about a tenth of <see cref="Full"/>, with the depth of a chain and the import caps' refusals unchanged.</summary>
    public static SoakScale Ci { get; } = new()
    {
        Name = "ci",
        FullDurability = false,
        Instances = 54,
        Chains = 2,
        ChainDepth = Tempest.Calc.Library.CalcInstanceService.MaxChainDepth,
        Cycles = 10,
        EngineMonteCarloRuns = 100,
        CalculatorMonteCarloRuns = 10,
        CalculatorMonteCarloSamples = 1_000,
        RunAtCalculatorCap = false,
        TornadoSweeps = 100,
        ImportFraction = 0.1,
        RecordRevisions = 10,
        DemoCycles = 2,
        DashboardRecords = 300,
        DashboardRuns = 20,
        DashboardBuilds = 5,
        GuidedRuns = 12,
    };

    /// <summary>Whether this run was asked for the full soak.</summary>
    public static bool FullRequested => string.Equals(Environment.GetEnvironmentVariable(EnvironmentVariable), "full", StringComparison.OrdinalIgnoreCase);

    /// <summary>Appends a progress line to the file named by <see cref="ProgressVariable"/>, if one is named; never throws.</summary>
    public static void Progress(string line)
    {
        try
        {
            if (Environment.GetEnvironmentVariable(ProgressVariable) is { Length: > 0 } path)
                File.AppendAllText(path, $"{DateTime.UtcNow:HH:mm:ss} {line}{Environment.NewLine}");
        }
        catch (IOException)
        {
            // Progress is a convenience for the person watching; losing a line never fails a soak.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
