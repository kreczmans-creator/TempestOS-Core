using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tempest.Calc.Library.ManualTesting;

/// <summary>What a guided step asks of the tester (Addendum C, C-5). The first five are the runbook's manual steps; an example is a worked example with its inputs and expected outputs.</summary>
public enum GuidedStepKind
{
    /// <summary>A step of the runbook's smoke test (section 4).</summary>
    Smoke,

    /// <summary>A linked chain (section 6).</summary>
    Pack,

    /// <summary>An import or a licensed-record check (section 7).</summary>
    Import,

    /// <summary>A step of the lifting checks behind the feature flag (section 8).</summary>
    Lifting,

    /// <summary>A legacy-record check (section 9).</summary>
    Legacy,

    /// <summary>One worked example of one worksheet template: open it with the example's inputs, compare the outputs.</summary>
    Example,
}

/// <summary>
/// One expected output of a worked example, as the example's README prints it: the symbol, the number text exactly as written, and the unit (empty for a ratio).
/// <paramref name="Computed"/> is the README's "Computed" column: what this build calculated for the example when its sheet was made. The expected value
/// is a hand calculation or a published figure held to the example's own verification tolerance, so in a few examples the two differ in the last digits
/// printed; the auto-compare uses the computed value only to tell such a difference (decide by hand) from a real regression.
/// </summary>
public sealed record GuidedExpectedValue(string Symbol, string Value, string Unit, string? Computed = null);

/// <summary>One input of a worked example as its sheet lists it, so the panel can show what was loaded.</summary>
public sealed record GuidedInputRow(string Symbol, string Description, string Value, string Unit);

/// <summary>The governing check of a worked example: its name, whether it passes and the utilisation as written.</summary>
public sealed record GuidedGoverning(string Name, string Outcome, string Utilisation);

/// <summary>One step of the guided test script.</summary>
public sealed record GuidedTestStep(
    string Id,
    GuidedStepKind Kind,
    string Section,
    string Title,
    string? Discipline = null,
    string? TemplateId = null,
    string? ExampleId = null,
    string? TemplateVersion = null,
    string? InputsPath = null,
    IReadOnlyList<GuidedInputRow>? Inputs = null,
    string? InputsJson = null,
    IReadOnlyList<GuidedExpectedValue>? Expected = null,
    GuidedGoverning? Governing = null,
    IReadOnlyList<string>? Instructions = null,
    string? PassWhen = null,
    string? ExampleRef = null)
{
    /// <summary>Whether the step opens a worksheet with loaded inputs (an example, or a manual step that pastes one).</summary>
    public bool OpensWorksheet => Kind == GuidedStepKind.Example;
}

/// <summary>
/// The structured, versioned test script the Guided test walks (Addendum C, C-5). It is generated from the worked examples and the
/// runbook by <c>scripts/generate-guided-test-script.py</c> and embedded in this assembly, because the docs folder is not present in an
/// installed build; a test fails when the embedded copy no longer matches the docs. Every size in it is capped (<c>Max*</c>), so a
/// damaged or hand-edited script is refused with a plain message rather than loaded.
/// </summary>
public sealed class GuidedTestScript
{
    /// <summary>The embedded resource's name.</summary>
    public const string ResourceName = "Tempest.Calc.Library.ManualTesting.guided-test-script.json";

    /// <summary>The script format this build reads.</summary>
    public const int SupportedFormat = 1;

    /// <summary>The most steps a script may hold.</summary>
    public const int MaxSteps = 400;

    /// <summary>The most expected outputs one step may hold.</summary>
    public const int MaxExpectedPerStep = 200;

    /// <summary>The most input rows one step may list.</summary>
    public const int MaxInputRowsPerStep = 400;

    /// <summary>The most instruction lines one step may hold.</summary>
    public const int MaxInstructionLines = 60;

    /// <summary>The longest text of any one field (title, instruction line, value).</summary>
    public const int MaxTextLength = 2_000;

    /// <summary>The longest <c>inputs.json</c> a step may carry.</summary>
    public const int MaxInputsJsonChars = 200_000;

    /// <summary>The largest script file read, in bytes.</summary>
    public const int MaxScriptBytes = 4_000_000;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private static readonly Lazy<GuidedTestScript> Embedded = new(LoadEmbedded, LazyThreadSafetyMode.ExecutionAndPublication);

    private readonly Dictionary<string, GuidedTestStep> _byId;

    private GuidedTestScript(string version, string release, IReadOnlyList<GuidedTestStep> steps)
    {
        ScriptVersion = version;
        Release = release;
        Steps = steps;
        _byId = steps.ToDictionary(s => s.Id, StringComparer.Ordinal);
    }

    /// <summary>The script the build ships.</summary>
    public static GuidedTestScript Current => Embedded.Value;

    /// <summary>The version a run records: the release and a short hash of the steps (for example <c>0.26.0+83487d9e</c>); it changes whenever a step does.</summary>
    public string ScriptVersion { get; }

    /// <summary>The release the script was written for.</summary>
    public string Release { get; }

    /// <summary>The steps in the order the tester walks them.</summary>
    public IReadOnlyList<GuidedTestStep> Steps { get; }

    /// <summary>A step by id, or <see langword="null"/>.</summary>
    public GuidedTestStep? Find(string stepId) => stepId is not null && _byId.TryGetValue(stepId, out var step) ? step : null;

    /// <summary>The example a manual step opens (the smoke test pastes one), or the step itself when it is an example.</summary>
    public GuidedTestStep? ExampleOf(GuidedTestStep step)
    {
        ArgumentNullException.ThrowIfNull(step);
        return step.Kind == GuidedStepKind.Example ? step : step.ExampleRef is { } id ? Find(id) : null;
    }

    /// <summary>The template ids the script has an example for, ordered.</summary>
    public IReadOnlyList<string> TemplateIds => [.. Steps.Where(s => s.Kind == GuidedStepKind.Example).Select(s => s.TemplateId!).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];

    /// <summary>Reads and validates a script. A script that is too large, of another format or malformed is refused with a one-sentence reason.</summary>
    public static GuidedTestScript Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        if (json.Length > MaxScriptBytes)
            throw new InvalidDataException($"The guided test script is larger than {MaxScriptBytes / 1_000_000} MB and was not loaded.");

        Document? document;

        try
        {
            document = JsonSerializer.Deserialize<Document>(json, Json);
        }
        catch (JsonException failure)
        {
            throw new InvalidDataException($"The guided test script could not be read: {failure.Message}", failure);
        }

        if (document is null || document.Steps is null)
            throw new InvalidDataException("The guided test script has no steps.");

        if (document.Format != SupportedFormat)
            throw new InvalidDataException($"The guided test script is format {document.Format}; this build reads format {SupportedFormat}.");

        if (string.IsNullOrWhiteSpace(document.ScriptVersion) || document.ScriptVersion.Length > 64)
            throw new InvalidDataException("The guided test script has no usable version.");

        Validate(document.Steps);
        return new GuidedTestScript(document.ScriptVersion, document.Release ?? string.Empty, document.Steps);
    }

    private static void Validate(IReadOnlyList<GuidedTestStep> steps)
    {
        if (steps.Count == 0 || steps.Count > MaxSteps)
            throw new InvalidDataException($"The guided test script has {steps.Count} steps; it must have between 1 and {MaxSteps}.");

        var ids = new HashSet<string>(StringComparer.Ordinal);

        foreach (var step in steps)
        {
            if (string.IsNullOrWhiteSpace(step.Id) || step.Id.Length > 200 || !ids.Add(step.Id))
                throw new InvalidDataException($"A guided test step has a missing, over-long or repeated id ('{Clip(step.Id)}').");

            Text(step.Id, "title", step.Title);
            Text(step.Id, "section", step.Section);

            foreach (var field in new[] { step.Discipline, step.TemplateId, step.ExampleId, step.TemplateVersion, step.InputsPath, step.PassWhen, step.ExampleRef })
                Text(step.Id, "field", field);

            if ((step.Expected?.Count ?? 0) > MaxExpectedPerStep)
                throw new InvalidDataException($"Guided test step '{step.Id}' lists more than {MaxExpectedPerStep} expected outputs.");

            if ((step.Inputs?.Count ?? 0) > MaxInputRowsPerStep)
                throw new InvalidDataException($"Guided test step '{step.Id}' lists more than {MaxInputRowsPerStep} inputs.");

            if ((step.Instructions?.Count ?? 0) > MaxInstructionLines)
                throw new InvalidDataException($"Guided test step '{step.Id}' has more than {MaxInstructionLines} instruction lines.");

            foreach (var line in step.Instructions ?? [])
                Text(step.Id, "instruction", line);

            foreach (var expected in step.Expected ?? [])
            {
                Text(step.Id, "expected output", expected.Symbol);
                Text(step.Id, "expected output", expected.Value);
                Text(step.Id, "expected output", expected.Unit);
            }

            foreach (var input in step.Inputs ?? [])
            {
                Text(step.Id, "input", input.Symbol);
                Text(step.Id, "input", input.Description);
                Text(step.Id, "input", input.Value);
                Text(step.Id, "input", input.Unit);
            }

            if (step.InputsJson is { Length: > MaxInputsJsonChars })
                throw new InvalidDataException($"Guided test step '{step.Id}' carries more than {MaxInputsJsonChars} characters of inputs.");

            if (step.Kind == GuidedStepKind.Example && (string.IsNullOrWhiteSpace(step.TemplateId) || string.IsNullOrWhiteSpace(step.InputsJson) || (step.Expected?.Count ?? 0) == 0))
                throw new InvalidDataException($"Guided test example '{step.Id}' needs a template, its inputs and its expected outputs.");
        }

        foreach (var step in steps.Where(s => s.ExampleRef is not null))
        {
            if (!ids.Contains(step.ExampleRef!))
                throw new InvalidDataException($"Guided test step '{step.Id}' points at the missing example '{step.ExampleRef}'.");
        }
    }

    private static void Text(string stepId, string what, string? text)
    {
        if (text is { Length: > MaxTextLength })
            throw new InvalidDataException($"Guided test step '{Clip(stepId)}' has a {what} longer than {MaxTextLength} characters.");
    }

    private static string Clip(string? text) => text is null ? string.Empty : text.Length <= 40 ? text : text[..40] + "…";

    private static GuidedTestScript LoadEmbedded()
    {
        using var stream = typeof(GuidedTestScript).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidDataException("This build does not carry its guided test script.");

        if (stream.Length > MaxScriptBytes)
            throw new InvalidDataException($"The guided test script is larger than {MaxScriptBytes / 1_000_000} MB and was not loaded.");

        using var reader = new StreamReader(stream, System.Text.Encoding.UTF8);
        return Parse(reader.ReadToEnd());
    }

    private sealed record Document(int Format, string? ScriptVersion, string? Release, IReadOnlyList<GuidedTestStep>? Steps);
}
