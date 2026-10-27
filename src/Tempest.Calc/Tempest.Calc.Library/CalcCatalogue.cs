using Tempest.Calc.Sheets;

namespace Tempest.Calc.Library;

/// <summary>One catalogue entry: a calculator, its template and its content hash, computed once.</summary>
public sealed record CalcCatalogueEntry(ICalcCalculator Calculator, string ContentHash)
{
    public CalcTemplate Template => Calculator.Template;

    public string Reference => Template.Reference;

    public string ShortHash => CalcTemplateHasher.Short(ContentHash);
}

/// <summary>How a template reference relates to what this catalogue has installed (ADR-0177, DC-14).</summary>
public enum CalcVersionState
{
    /// <summary>The formulas of exactly this version are installed: it runs as it was made.</summary>
    Installed,

    /// <summary>A version recorded as having existed whose formulas are not installed; the template's current version is.</summary>
    Superseded,

    /// <summary>An older version that the version record does not list (made on a build this one has no record of); the current version is installed.</summary>
    Unrecorded,

    /// <summary>A version newer than the current one installed here (made by a newer build).</summary>
    Newer,

    /// <summary>The template is installed but switched off by a feature flag (ADR-0173).</summary>
    Withheld,

    /// <summary>No template of this id is installed.</summary>
    Gone,
}

/// <summary>
/// What a catalogue knows of an <c>id@version</c> reference. <see cref="Entry"/> is set only when that version's own formulas are installed (state
/// <see cref="CalcVersionState.Installed"/>); <see cref="Current"/> is the template's current version, when one is installed; <see cref="RecordedHash"/>
/// is the content hash the version record holds for the reference, when it holds one.
/// </summary>
public sealed record CalcVersionResolution(string Reference, CalcVersionState State, CalcCatalogueEntry? Entry, CalcCatalogueEntry? Current, string? RecordedHash)
{
    /// <summary>Whether the reference names the template's current version.</summary>
    public bool IsCurrent => State == CalcVersionState.Installed && Entry is not null && ReferenceEquals(Entry, Current);

    /// <summary>Whether the version's formulas are not installed but the template's current version is, so a record made on it can be explained and carried forward.</summary>
    public bool IsSuperseded => State is CalcVersionState.Superseded or CalcVersionState.Unrecorded;
}

/// <summary>A calculator was asked for by a version whose formulas are not installed (a superseded one); a <see cref="KeyNotFoundException"/> whose message says what became of it.</summary>
public sealed class CalcVersionNotInstalledException(string message) : KeyNotFoundException(message);

/// <summary>
/// The registry of every calculator the application composes in, by id
/// and version. Static registration only: the calculator assemblies hand
/// their instances to the composer, nothing is loaded from a path.
/// <para>
/// A version is <b>resolvable</b> in two ways (ADR-0177). Its formulas can be registered beside the current ones (a frozen copy, which the
/// catalogue has always allowed): <see cref="Find"/> then returns it and a calculation made on it runs as it was made. Otherwise the
/// <see cref="History"/> (the committed manifest of every version ever recorded) lets <see cref="Resolve"/> say what the reference was and what it became.
/// <see cref="Find"/> never returns another version's formulas for a versioned reference: a calculation is never silently run on code it was not made on.
/// </para>
/// </summary>
public sealed class CalcCatalogue
{
    private readonly Dictionary<string, CalcCatalogueEntry> _byReference = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CalcCatalogueEntry> _current = new(StringComparer.Ordinal);

    // Calculators gated behind a feature flag that is off (ADR-0173): validated and kept, never listed, found, created or issued.
    private readonly Dictionary<string, CalcCatalogueEntry> _withheldByReference = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CalcCatalogueEntry> _withheldCurrent = new(StringComparer.Ordinal);
    private readonly object _gate = new();

    public CalcCatalogue() : this((CalcFeatureFlags?)null) { }

    public CalcCatalogue(CalcFeatureFlags? features, TemplateVersionHistory? history = null)
    {
        Features = features ?? CalcFeatureFlags.None;
        History = history ?? TemplateVersionHistory.Shipped;
    }

    public CalcCatalogue(IEnumerable<ICalcCalculator> calculators, CalcFeatureFlags? features = null, TemplateVersionHistory? history = null) : this(features, history)
    {
        ArgumentNullException.ThrowIfNull(calculators);

        foreach (var c in calculators)
            Register(c);
    }

    /// <summary>The optional features this catalogue was built with. Fixed for its lifetime.</summary>
    public CalcFeatureFlags Features { get; }

    /// <summary>Every template version this product has recorded (the embedded manifest unless a test supplies another). Fixed for the catalogue's lifetime.</summary>
    public TemplateVersionHistory History { get; }

    /// <summary>Registers a calculator. A second registration of the same id@version is refused; a newer version becomes current.</summary>
    public CalcCatalogueEntry Register(ICalcCalculator calculator)
    {
        ArgumentNullException.ThrowIfNull(calculator);
        var template = calculator.Template;
        ValidateTemplate(template);

        // One run on the defaults gives the formulas the content hash covers and the output symbols a port alias must not take.
        var defaultRun = CalcRunner.Run(calculator, CalcTemplateHasher.DefaultInputs(template));
        var contentHash = CalcTemplateHasher.Hash(template, CalcTemplateHasher.Formulas(defaultRun));
        ValidatePortAliases(calculator, defaultRun, contentHash);

        lock (_gate)
        {
            if (_byReference.ContainsKey(template.Reference) || _withheldByReference.ContainsKey(template.Reference))
                throw new InvalidOperationException($"Calculator '{template.Reference}' is already registered.");

            var entry = new CalcCatalogueEntry(calculator, contentHash);

            // A calculator whose feature is off is held aside: no list, no search, no lookup and so no new instance reaches it (ADR-0173).
            var (byReference, current) = Features.IsEnabled(template.FeatureFlag) ? (_byReference, _current) : (_withheldByReference, _withheldCurrent);
            byReference[template.Reference] = entry;

            if (!current.TryGetValue(template.Id, out var latest) || CompareVersions(template.Version, latest.Template.Version) > 0)
                current[template.Id] = entry;

            return entry;
        }
    }

    /// <summary>
    /// Why a calculator that is composed in but switched off cannot be used, or <see langword="null"/> when <paramref name="reference"/>
    /// ("id@version" or id) is not a withheld calculator. The one message every refusal path shows.
    /// </summary>
    public string? FeatureRefusal(string? reference)
    {
        if (string.IsNullOrWhiteSpace(reference))
            return null;

        lock (_gate)
        {
            var withheld = _withheldByReference.GetValueOrDefault(reference) ?? _withheldCurrent.GetValueOrDefault(reference.Split('@')[0]);

            if (withheld is null || _byReference.ContainsKey(reference))
                return null;

            var flag = withheld.Template.FeatureFlag ?? string.Empty;
            return Tempest.Core.Calculations.CalculationFeatureGate.Refusal(withheld.Template.Title, withheld.Template.Id, flag);
        }
    }

    /// <summary>Whether <paramref name="reference"/> names a calculator that is composed in but switched off.</summary>
    public bool IsWithheld(string? reference) => FeatureRefusal(reference) is not null;

    public IReadOnlyList<CalcCatalogueEntry> All()
    {
        lock (_gate) { return _byReference.Values.OrderBy(e => e.Template.Discipline).ThenBy(e => e.Template.Title, StringComparer.Ordinal).ToList(); }
    }

    /// <summary>The current version of every calculator, by discipline then title.</summary>
    public IReadOnlyList<CalcCatalogueEntry> Current()
    {
        lock (_gate) { return _current.Values.OrderBy(e => e.Template.Discipline).ThenBy(e => e.Template.Title, StringComparer.Ordinal).ToList(); }
    }

    public IReadOnlyList<CalcCatalogueEntry> ByDiscipline(CalcDiscipline discipline) => Current().Where(e => e.Template.Discipline == discipline).ToList();

    /// <summary>Finds by "id@version" or by id alone (current version).</summary>
    public CalcCatalogueEntry? Find(string reference)
    {
        if (string.IsNullOrWhiteSpace(reference))
            return null;

        lock (_gate)
        {
            if (_byReference.TryGetValue(reference, out var exact))
                return exact;

            return _current.GetValueOrDefault(reference);
        }
    }

    public CalcCatalogueEntry Get(string reference) =>
        Find(reference) ?? throw (FeatureRefusal(reference) is { } switchedOff ? new CalcFeatureDisabledException(switchedOff)
            : Resolve(reference) is { IsSuperseded: true, Current: { } current } ? new CalcVersionNotInstalledException($"Calculator '{reference}' has been revised to '{current.Reference}'; the formulas of the earlier version are not installed.")
            : new KeyNotFoundException($"No calculator '{reference}' is registered."));

    /// <summary>
    /// What this catalogue knows of <paramref name="reference"/> (<c>id@version</c>, or an id alone, which is its current version): installed as itself,
    /// a superseded version the history records, an older version it has no record of, a newer one, switched off, or gone. Never throws, never runs anything.
    /// </summary>
    public CalcVersionResolution Resolve(string? reference)
    {
        if (string.IsNullOrWhiteSpace(reference))
            return new CalcVersionResolution(reference ?? string.Empty, CalcVersionState.Gone, null, null, null);

        var at = reference.IndexOf('@', StringComparison.Ordinal);
        var id = at < 0 ? reference : reference[..at];

        lock (_gate)
        {
            var current = _current.GetValueOrDefault(id);
            var recorded = History.HashOf(reference);

            if (at < 0)
                return current is not null ? new CalcVersionResolution(reference, CalcVersionState.Installed, current, current, recorded) : Absent(reference, id);

            if (_byReference.TryGetValue(reference, out var exact))
                return new CalcVersionResolution(reference, CalcVersionState.Installed, exact, current, recorded);

            if (current is null)
                return Absent(reference, id);

            var version = reference[(at + 1)..];
            var state = CompareVersions(version, current.Template.Version) > 0 ? CalcVersionState.Newer
                : recorded is not null ? CalcVersionState.Superseded
                : CalcVersionState.Unrecorded;
            return new CalcVersionResolution(reference, state, null, current, recorded);
        }
    }

    private CalcVersionResolution Absent(string reference, string id) =>
        new(reference, _withheldCurrent.ContainsKey(id) ? CalcVersionState.Withheld : CalcVersionState.Gone, null, null, History.HashOf(reference));

    /// <summary>The outputs of a run of every known-answer case of a template, by the template's reference and content: the runs are the same every time a catalogue is built.</summary>
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, IReadOnlySet<string>> CaseOutputSymbols = new(StringComparer.Ordinal);

    /// <summary>
    /// C-2: a port is also known by its public name and its aliases, so none of them may be the symbol of an output that is not that port: a link by that
    /// symbol would then name two outputs. The outputs are those of a run on the defaults, the symbols the known-answer cases expect and the outputs of a run of
    /// every known-answer case, because what a template outputs depends on its mode (a name free on the defaults may be an output of another kind of spring or section).
    /// </summary>
    private static void ValidatePortAliases(ICalcCalculator calculator, CalcRunResult defaultRun, string contentHash)
    {
        var t = calculator.Template;

        if (t.OutputPorts is not { Count: > 0 } ports || !ports.Any(p => p.Name is { Length: > 0 } || p.Aliases is { Count: > 0 }))
            return;

        var portSymbols = ports.Select(p => p.Symbol).ToHashSet(StringComparer.Ordinal);
        var fromCases = CaseOutputSymbols.GetOrAdd(t.Reference + "|" + contentHash, _ =>
            t.TestCases.SelectMany(c => CalcRunner.Run(calculator, new CalcInputSet(c.Inputs)).Outputs.Select(o => o.Symbol)).ToHashSet(StringComparer.Ordinal));
        var others = defaultRun.Outputs.Select(o => o.Symbol).Concat(t.TestCases.SelectMany(c => c.Expected).Select(e => e.Symbol)).Concat(fromCases)
            .Where(s => !portSymbols.Contains(s)).ToHashSet(StringComparer.Ordinal);

        foreach (var port in ports)
        {
            foreach (var name in (port.Name is { Length: > 0 } n ? [n] : Enumerable.Empty<string>()).Concat(port.Aliases ?? []))
            {
                if (others.Contains(name))
                    throw new ArgumentException($"Template '{t.Id}': the name '{name}' of port '{port.Symbol}' is the symbol of another output of the template; a link by that symbol would name two outputs.");
            }
        }
    }

    private static void ValidateTemplate(CalcTemplate t)
    {
        if (string.IsNullOrWhiteSpace(t.Id) || t.Id.Length > 64 || t.Id.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '_')))
            throw new ArgumentException($"Template id '{t.Id}' must be 1–64 characters of letters, digits, '.', '-' or '_'.");

        if (string.IsNullOrWhiteSpace(t.Version) || t.Version.Length > 32 || t.Version.Any(c => !(char.IsAsciiDigit(c) || c == '.')))
            throw new ArgumentException($"Template version '{t.Version}' must be dotted digits.");

        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var i in t.Inputs)
        {
            if (!seen.Add(i.Symbol))
                throw new ArgumentException($"Template '{t.Id}' declares input '{i.Symbol}' twice.");
        }

        // C-2: an alias is the symbol an input had before it was renamed, so it must name no input of the template and no other input's alias.
        foreach (var i in t.Inputs)
        {
            foreach (var alias in i.Aliases ?? [])
            {
                if (!seen.Add(alias))
                    throw new ArgumentException($"Template '{t.Id}': the alias '{alias}' of input '{i.Symbol}' is already an input or the alias of another input.");
            }
        }

        // C-2: a port is known by its output symbol, its public name and its aliases; no two ports may share one of those names.
        var portNames = new HashSet<string>(StringComparer.Ordinal);

        foreach (var port in t.OutputPorts ?? [])
        {
            foreach (var name in new[] { port.Symbol }.Concat(port.Name is { Length: > 0 } n ? [n] : []).Concat(port.Aliases ?? []).Distinct(StringComparer.Ordinal))
            {
                if (!portNames.Add(name))
                    throw new ArgumentException($"Template '{t.Id}': the port name '{name}' of output '{port.Symbol}' is already used by another port.");
            }
        }

        // ADR-0173: the governance a template asks for must be satisfiable, or no instance of it could ever be issued.
        if (t.FeatureFlag is { Length: > 0 } flag && !CalcFeatures.All.Contains(flag, StringComparer.Ordinal))
            throw new ArgumentException($"Template '{t.Id}' names the unknown feature flag '{flag}'.");

        if (t.RequiresAttestation)
        {
            foreach (var symbol in CalcAttestation.Symbols)
            {
                if (t.FindInput(symbol) is not { Kind: CalcInputKind.Text })
                    throw new ArgumentException($"Template '{t.Id}' requires an attestation, so it must declare the text input '{symbol}'.");
            }
        }

        if (t.RequiresSecondCheck && !t.IndependentCheck)
            throw new ArgumentException($"Template '{t.Id}' requires a second check, so its first check must also be independent (IndependentCheck).");
    }

    internal static int CompareVersions(string a, string b)
    {
        var pa = a.Split('.').Select(x => int.TryParse(x, out var n) ? n : 0).ToArray();
        var pb = b.Split('.').Select(x => int.TryParse(x, out var n) ? n : 0).ToArray();

        for (var i = 0; i < Math.Max(pa.Length, pb.Length); i++)
        {
            var x = i < pa.Length ? pa[i] : 0;
            var y = i < pb.Length ? pb[i] : 0;

            if (x != y)
                return x.CompareTo(y);
        }

        return 0;
    }
}
