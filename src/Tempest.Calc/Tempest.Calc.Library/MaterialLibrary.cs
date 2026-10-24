using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Tempest.Calc.Engine.Units;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Library;

/// <summary>One property of a material read for a calculation: its value in SI, the unit it is kept in, how it was got, and where it comes from.</summary>
/// <param name="Property">The property symbol (<c>E</c>).</param>
/// <param name="Si">The value in coherent SI (Pa, kg/m^3, 1/K, W/(m*K); a plain number for a dimensionless property). A coefficient whose unit follows another value (the R_m(d) coefficient A) is kept as entered.</param>
/// <param name="SiUnit">The SI unit text; empty for a plain number.</param>
/// <param name="Basis">"stated" (the single value as entered), "tabulated at 100 degC", "interpolated between 20 and 100 degC" or "single value, no temperature table".</param>
/// <param name="Source">The value's own source, edition, page or table, licence class, who entered it and when.</param>
/// <param name="TemperatureKelvin">The temperature it was read at, or <see langword="null"/> when none was asked for.</param>
public sealed record MaterialValue(string Property, double Si, string SiUnit, string Basis, CalcValueSource Source, double? TemperatureKelvin = null)
{
    /// <summary>The value in <paramref name="unit"/>, or why it cannot be (the unit measures something else).</summary>
    public (double? Value, string? Refusal) In(string unit)
    {
        var (value, refusal) = MaterialLibrary.Convert(Si, SiUnit, unit);
        return (value, refusal is null ? null : $"{Property}: {refusal}");
    }
}

/// <summary>What one lookup of a property gave: a value, a table, nothing (the material does not state it) or a refusal.</summary>
internal sealed record MaterialResolution(MaterialValue? Scalar, IReadOnlyList<CalcRow>? Rows, CalcValueSource? Source, bool Absent, string? Refusal)
{
    public static MaterialResolution Missing { get; } = new(null, null, null, true, null);

    public static MaterialResolution Refuse(string reason) => new(null, null, null, false, reason);
}

/// <summary>A temperature asked for: as typed, and in kelvin.</summary>
internal readonly record struct MaterialTemperature(double Value, string Unit, double Kelvin)
{
    public static MaterialTemperature OfKelvin(double kelvin) => new(kelvin, "K", kelvin);

    public override string ToString() => $"{Value.ToString("G6", CultureInfo.InvariantCulture)} {Unit}";
}

/// <summary>
/// Addendum B, W3 — the material property library. A material is a reference record of kind <see cref="Kind"/>
/// (<see cref="CalcDataRecord"/>) whose values are properties: E, G, ν, ρ, α, λ, f_y, f_u, the R_m(d) curve coefficients,
/// the permissible strains, the secant-modulus points, friction coefficients by pair, and tables of any of the first eight
/// against temperature. Each value carries its own source, edition, page or table, licence class, who entered it, when,
/// and the SHA-256 of any evidence attached. It is built on the records, not beside them: a material is revised, never
/// edited; a calculation pins the revision it took values from and goes stale when a later one is saved; a material with
/// a licensed value is a licensed record, whose values never leave through an export (§A5).
/// </summary>
/// <remarks>
/// Properties are kept as typed and read in SI. A template input names the property it takes in
/// <see cref="CalcInputSpec.MaterialProperty"/> and lists its symbol in a record kind of kind <see cref="Kind"/>; applying
/// the material then fills those inputs from the record, each citing the record's pin, the property, any temperature and
/// the value's own page or table.
/// </remarks>
public sealed class MaterialLibrary
{
    /// <summary>The record kind of a material.</summary>
    public const string Kind = "material.properties";

    /// <summary>The format tag of an exported material.</summary>
    public const string ExportFormat = "tempest.material/1";

    /// <summary>The largest material file the library reads.</summary>
    public const int MaxImportBytes = 1_000_000;

    private const double TemperatureSlack = 1e-9;

    private readonly CalcInstanceService _service;
    private bool _seeded;

    internal MaterialLibrary(CalcInstanceService service) => _service = service ?? throw new ArgumentNullException(nameof(service));

    /// <summary>Whether a record kind is the material kind.</summary>
    public static bool IsMaterial(string? kind) => string.Equals(kind, Kind, StringComparison.Ordinal);

    /// <summary>The record kind a template lists to take material properties: <c>new CalcRecordKind(MaterialLibrary.Kind, "Material", [input symbols], guidance)</c>.</summary>
    public static CalcRecordKind RecordKind(IReadOnlyList<string> inputSymbols, string guidance = "A material from the library: its properties fill these inputs, each citing the material, its revision and the page or table of the value.") =>
        new(Kind, "Material properties", inputSymbols, guidance);

    // ------------------------------------------------------------------
    // The library: list, find, save
    // ------------------------------------------------------------------

    /// <summary>Saves the generic seed materials that are not in the store yet (revision 1, origin indicative, unlicensed). Safe to call again.</summary>
    public async Task EnsureSeededAsync(CancellationToken cancellationToken = default)
    {
        if (_seeded)
            return;

        var now = _service.Time.GetUtcNow();

        foreach (var seed in MaterialSeeds.Build(now))
        {
            if (await _service.Store.FindRecordAsync(seed.Id, 1, cancellationToken).ConfigureAwait(false) is not null)
                continue;

            try
            {
                await _service.Store.SaveRecordAsync(seed, cancellationToken).ConfigureAwait(false);
            }
            catch (CalcInstanceImmutableException)
            {
                // Another caller seeded it at the same moment.
            }
        }

        _seeded = true;
    }

    /// <summary>The latest revision of every material, seeds included, optionally those whose title contains <paramref name="search"/> (any case).</summary>
    public async Task<IReadOnlyList<CalcDataRecord>> ListAsync(string? search = null, CancellationToken cancellationToken = default)
    {
        await EnsureSeededAsync(cancellationToken).ConfigureAwait(false);
        var all = await _service.Store.ListRecordsAsync(Kind, cancellationToken).ConfigureAwait(false);

        return string.IsNullOrWhiteSpace(search)
            ? all
            : [.. all.Where(m => m.Title.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase))];
    }

    /// <summary>A material at a revision, or its latest; <see langword="null"/> when there is none (or the record is not a material).</summary>
    public async Task<CalcDataRecord?> FindAsync(Guid id, int? revision = null, CancellationToken cancellationToken = default)
    {
        await EnsureSeededAsync(cancellationToken).ConfigureAwait(false);
        return await _service.Store.FindRecordAsync(id, revision, cancellationToken).ConfigureAwait(false) is { } record && IsMaterial(record.Kind) ? record : null;
    }

    /// <summary>The latest revision of the material with this title (any case), or <see langword="null"/>.</summary>
    public async Task<CalcDataRecord?> FindByTitleAsync(string title, CancellationToken cancellationToken = default) =>
        (await ListAsync(null, cancellationToken).ConfigureAwait(false)).FirstOrDefault(m => string.Equals(m.Title, title?.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Saves a material: a new one at revision 1, or — when <paramref name="draft"/> names an existing one — its next
    /// revision. The kind is always <see cref="Kind"/>. Each value's entered-by and date are stamped when it is new or
    /// changed; a value with no source of its own takes the record's. A licensed value makes the material licensed.
    /// </summary>
    public Task<(CalcDataRecord? Record, string? Refusal)> SaveAsync(CalcDataRecord draft, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);
        return _service.SaveRecordAsync(draft with { Kind = Kind }, cancellationToken);
    }

    // ------------------------------------------------------------------
    // Reading a property
    // ------------------------------------------------------------------

    /// <summary>
    /// A property of a material in SI. With a <paramref name="temperature"/> the material's temperature table for it is
    /// read, linearly between its rows; outside the tabulated range the answer is a refusal that says the range (there is
    /// no extrapolation). A property given only as a table needs a temperature; one given only as a single value has no
    /// temperature to be read at and is refused for one.
    /// </summary>
    public static (MaterialValue? Value, string? Refusal) GetProperty(CalcDataRecord material, string property, double? temperature = null, string temperatureUnit = "K")
    {
        ArgumentNullException.ThrowIfNull(material);
        MaterialTemperature? at = null;

        if (temperature is { } t)
        {
            var (parsed, problem) = MaterialProperties.ParseTemperatureUnit(temperatureUnit, "temperature");

            if (problem is not null)
                return (null, problem);

            if (!double.IsFinite(t))
                return (null, "temperature: give a finite number.");

            at = new MaterialTemperature(t, string.IsNullOrWhiteSpace(temperatureUnit) ? "K" : temperatureUnit.Trim(), parsed!.ToSi(t));
        }

        var resolved = Resolve(material, property, at, singleValue: false);

        if (resolved.Refusal is not null)
            return (null, resolved.Refusal);

        if (resolved.Absent)
            return (null, $"Material '{material.Title}' does not state {property}.");

        return resolved.Scalar is { } value
            ? (value, null)
            : (null, $"{property} of '{material.Title}' is a table; it is applied to a table input, not read as one number.");
    }

    /// <summary>As <see cref="GetProperty"/>, for the material with this id (its latest revision, or <paramref name="revision"/>).</summary>
    public async Task<(MaterialValue? Value, string? Refusal)> GetPropertyAsync(Guid materialId, string property, double? temperature = null, string temperatureUnit = "K", int? revision = null, CancellationToken cancellationToken = default) =>
        await FindAsync(materialId, revision, cancellationToken).ConfigureAwait(false) is { } material
            ? GetProperty(material, property, temperature, temperatureUnit)
            : (null, $"No material '{materialId}' exists.");

    /// <summary>A number from one unit to another of the same dimension; a refusal names what does not match. A temperature on an offset scale (degC, degF) is shifted, not just scaled.</summary>
    public static (double? Value, string? Refusal) Convert(double value, string? fromUnit, string? toUnit)
    {
        if (!double.IsFinite(value))
            return (null, "the number is not finite.");

        if (!UnitCatalog.TryParse(fromUnit ?? string.Empty, out var from, out var fromError))
            return (null, $"unit '{fromUnit}' is not understood ({fromError}).");

        if (!UnitCatalog.TryParse(toUnit ?? string.Empty, out var to, out var toError))
            return (null, $"unit '{toUnit}' is not understood ({toError}).");

        return from.Dimension == to.Dimension
            ? (to.FromSi(from.ToSi(value)), null)
            : (null, $"'{fromUnit}' and '{toUnit}' do not measure the same thing.");
    }

    /// <summary>A number in <paramref name="unit"/> as SI, or why it cannot be.</summary>
    public static (double? Value, string? Refusal) ToSi(double value, string? unit)
    {
        if (!double.IsFinite(value))
            return (null, "the number is not finite.");

        return UnitCatalog.TryParse(unit ?? string.Empty, out var parsed, out var error)
            ? (parsed.ToSi(value), null)
            : (null, $"unit '{unit}' is not understood ({error}).");
    }

    /// <summary>The SHA-256 of an evidence file, as the 64 lower-case hexadecimal digits a value's <see cref="CalcValueSource.EvidenceSha256"/> holds.</summary>
    public static string Sha256Hex(ReadOnlySpan<byte> content) => System.Convert.ToHexStringLower(SHA256.HashData(content));

    // ------------------------------------------------------------------
    // Applying to a calculation
    // ------------------------------------------------------------------

    /// <summary>
    /// Fills a draft calculation's inputs from a material: every input of the template that names a material property
    /// (<see cref="CalcInputSpec.MaterialProperty"/>) and is listed by the template's material record kind takes the
    /// material's value, in the input's own unit, citing the material's pin. Properties the material does not state are
    /// left as they are. With a <paramref name="temperature"/>, temperature-dependent properties are read at it
    /// (a refusal outside a table's range); an input whose property has no temperature table is refused unless
    /// <paramref name="singleValueAtAnyTemperature"/> says its single value stands for it.
    /// </summary>
    public Task<CalcActionResult> ApplyAsync(Guid instanceId, Guid materialId, int? revision = null, double? temperature = null, string temperatureUnit = "degC", bool singleValueAtAnyTemperature = false, CancellationToken cancellationToken = default)
    {
        MaterialTemperature? at = null;

        if (temperature is { } t)
        {
            var (parsed, problem) = MaterialProperties.ParseTemperatureUnit(temperatureUnit, "temperature");

            if (problem is not null)
                return Task.FromResult(CalcActionResult.Refused(problem));

            if (!double.IsFinite(t))
                return Task.FromResult(CalcActionResult.Refused("temperature: give a finite number."));

            at = new MaterialTemperature(t, string.IsNullOrWhiteSpace(temperatureUnit) ? "K" : temperatureUnit.Trim(), parsed!.ToSi(t));
        }

        return _service.ApplyRecordCoreAsync(instanceId, materialId, revision, at, singleValueAtAnyTemperature, cancellationToken);
    }

    // ------------------------------------------------------------------
    // Export and import
    // ------------------------------------------------------------------

    /// <summary>
    /// A material as CSV: one line per value with its unit, source, edition, reference, licence class, who entered it, when
    /// and its evidence hash, then its table rows. Refused for a licensed material (§A5): its values are never exported.
    /// </summary>
    public async Task<(string? Csv, string? Refusal)> ExportCsvAsync(Guid materialId, int? revision = null, CancellationToken cancellationToken = default)
    {
        var (material, refusal) = await ExportableAsync(materialId, revision, cancellationToken).ConfigureAwait(false);

        if (material is null)
            return (null, refusal);

        var sb = new StringBuilder();
        sb.Append(CalcInstanceService.Csv("# " + material.Citation)).Append('\n').Append("property,value,unit,text,source,edition,reference,licence,entered_by,date,evidence_sha256\n");

        foreach (var v in material.Values)
        {
            var s = v.Source;
            var tail = string.Join(',', new[]
            {
                s?.Source ?? string.Empty,
                s?.Edition ?? string.Empty,
                s?.Reference ?? string.Empty,
                s is null ? string.Empty : s.Licence == CalcLicenceClass.LicensedSource ? "licensed_source" : "public",
                s?.EnteredBy ?? string.Empty,
                s?.Date?.ToString("O", CultureInfo.InvariantCulture) ?? string.Empty,
                s?.EvidenceSha256 ?? string.Empty,
            }.Select(CalcInstanceService.Csv));

            sb.Append(CalcInstanceService.Csv(v.Symbol)).Append(',').Append(v.Number?.ToString("R", CultureInfo.InvariantCulture) ?? string.Empty).Append(',').Append(CalcInstanceService.Csv(v.Unit ?? string.Empty)).Append(',').Append(CalcInstanceService.Csv(v.Text ?? string.Empty)).Append(',').Append(tail).Append('\n');

            foreach (var row in v.Rows ?? [])
            {
                var cells = string.Join("; ", row.Cells.OrderBy(c => c.Key, StringComparer.Ordinal).Select(c => $"{c.Key}={c.Value.Number?.ToString("R", CultureInfo.InvariantCulture) ?? c.Value.Text}{(c.Value.Unit is { Length: > 0 } u ? " " + u : string.Empty)}"));
                sb.Append(CalcInstanceService.Csv(v.Symbol + "[]")).Append(",,,").Append(CalcInstanceService.Csv(cells)).Append('\n');
            }
        }

        return (sb.ToString(), null);
    }

    /// <summary>A material as JSON (<see cref="ExportFormat"/>), which <see cref="ImportJsonAsync"/> reads back. Refused for a licensed material (§A5).</summary>
    public async Task<(string? Json, string? Refusal)> ExportJsonAsync(Guid materialId, int? revision = null, CancellationToken cancellationToken = default)
    {
        var (material, refusal) = await ExportableAsync(materialId, revision, cancellationToken).ConfigureAwait(false);

        return material is null
            ? (null, refusal)
            : (CalcInstanceStore.Serialise(new MaterialDocument(ExportFormat, material.Title, material.SourceTitle, material.Edition, material.TableOrClause, material.LicenceHolder, material.Origin, material.Values, material.Notes)), null);
    }

    /// <summary>
    /// Saves the material in a file made by <see cref="ExportJsonAsync"/> as a new material (revision 1, entered by the
    /// signed-in person now). A file that holds a licensed value, or a value withheld on export, is refused: a licensed
    /// material is never exported, so such a file did not come from this application and its values cannot be vouched for.
    /// </summary>
    public async Task<(CalcDataRecord? Record, string? Refusal)> ImportJsonAsync(string json, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(json))
            return (null, "The material file is empty.");

        if (Encoding.UTF8.GetByteCount(json) > MaxImportBytes)
            return (null, $"The material file is larger than {MaxImportBytes / 1_000_000} MB.");

        if (json.Contains(CalcCsv.WithheldMarker, StringComparison.Ordinal))
            return (null, "That file holds values withheld on export (licensed source); enter them from your own copy instead.");

        MaterialDocument? document;

        try
        {
            document = CalcInstanceStore.Deserialise<MaterialDocument>(json);
        }
        catch (System.Text.Json.JsonException ex)
        {
            return (null, $"The material file is not valid: {CalcSheetTextRenderer.Clean(ex.Message)}");
        }

        if (document is null || !string.Equals(document.Format, ExportFormat, StringComparison.Ordinal) || document.Values is null)
            return (null, $"That is not a material file ({ExportFormat}).");

        if (document.Values.Any(v => v?.Source is null || v.Source.Licence == CalcLicenceClass.LicensedSource))
            return (null, "That file holds a value from a licensed source or a value with no source; licensed values are never exported, so it cannot be imported.");

        return await SaveAsync(
            new CalcDataRecord(Guid.Empty, Kind, document.Title ?? string.Empty, 0, document.SourceTitle ?? string.Empty, document.Edition ?? string.Empty, document.TableOrClause ?? string.Empty, document.LicenceHolder ?? string.Empty,
                string.Empty, false, CalcDataOrigin.UserEntered, document.Values, default, document.Notes),
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<(CalcDataRecord? Material, string? Refusal)> ExportableAsync(Guid materialId, int? revision, CancellationToken cancellationToken)
    {
        var material = await FindAsync(materialId, revision, cancellationToken).ConfigureAwait(false);

        if (material is null)
            return (null, $"No material '{materialId}' exists.");

        return material.LicensedSource || material.Values.Any(v => v.Source?.Licence == CalcLicenceClass.LicensedSource)
            ? (null, $"Material '{material.Title}' holds values from a licensed source ({material.SourceTitle}, {material.Edition}); its values are never exported.")
            : (material, null);
    }

    /// <summary>A material as it leaves the application (<see cref="ExportJsonAsync"/>).</summary>
    public sealed record MaterialDocument(string Format, string Title, string SourceTitle, string Edition, string TableOrClause, string LicenceHolder, CalcDataOrigin Origin, IReadOnlyList<CalcDataValue> Values, string? Notes = null);

    // ------------------------------------------------------------------
    // Saving: stamps and licence class (called by CalcInstanceService.SaveRecordAsync)
    // ------------------------------------------------------------------

    /// <summary>
    /// A material draft made ready to save: a value with no source of its own takes the record's; a value that is new or
    /// changed since <paramref name="latest"/> is stamped with who entered it and when (one that is unchanged keeps its earlier
    /// stamp); a material with any licensed value, or itself flagged licensed, is licensed throughout (the lock is per record).
    /// </summary>
    internal static CalcDataRecord Prepare(CalcDataRecord record, CalcDataRecord? latest, string principal, DateTimeOffset now)
    {
        var earlier = latest is null ? [] : latest.Values.ToDictionary(v => v.Symbol, StringComparer.Ordinal);

        var values = record.Values.Select(v =>
        {
            var source = v.Source ?? new CalcValueSource(record.SourceTitle, record.Edition, record.TableOrClause, record.LicensedSource ? CalcLicenceClass.LicensedSource : CalcLicenceClass.Public, principal, now);
            source = source with { Source = CalcSheetTextRenderer.Clean(source.Source), Edition = CalcSheetTextRenderer.Clean(source.Edition), Reference = CalcSheetTextRenderer.Clean(source.Reference) };
            var candidate = v with { Source = source };

            if (earlier.TryGetValue(v.Symbol, out var before) && before.Source is { } beforeSource && Unstamped(before) == Unstamped(candidate))
                return before with { Source = beforeSource };

            return candidate with
            {
                Source = source with
                {
                    EnteredBy = principal,
                    Date = now,
                    EvidenceSha256 = source.EvidenceSha256?.ToLowerInvariant(),
                },
            };
        }).ToList();

        var licensed = record.LicensedSource || values.Any(v => v.Source!.Licence == CalcLicenceClass.LicensedSource);

        if (licensed)
            values = [.. values.Select(v => v with { Source = v.Source! with { Licence = CalcLicenceClass.LicensedSource } })];

        return record with { Values = values, LicensedSource = licensed };
    }

    /// <summary>A value as compared for change: everything but who entered it and when.</summary>
    private static string Unstamped(CalcDataValue value) =>
        CalcInstanceStore.Serialise(value with { Source = value.Source is null ? null : value.Source with { EnteredBy = string.Empty, Date = null, EvidenceSha256 = value.Source.EvidenceSha256?.ToLowerInvariant() } });

    // ------------------------------------------------------------------
    // Resolving a property (temperature, interpolation)
    // ------------------------------------------------------------------

    /// <summary>
    /// A property of a material, at a temperature if one is given. <paramref name="singleValue"/> lets a property that has
    /// no temperature table answer with its single value for a temperature (an application's choice, and staleness's way of re-reading
    /// what was applied); otherwise that is a refusal.
    /// </summary>
    internal static MaterialResolution Resolve(CalcDataRecord material, string property, MaterialTemperature? at, bool singleValue)
    {
        var def = MaterialProperties.Find(property, out var temperatureTable);

        if (def is null)
            return MaterialResolution.Refuse($"'{property}' is not a material property.");

        if (def.Shape == MaterialPropertyShape.Table || temperatureTable)
        {
            var table = Value(material, temperatureTable ? def.TemperatureKey : def.Key);
            return table?.Rows is { } rows ? new MaterialResolution(null, rows, table.Source, false, null) : MaterialResolution.Missing;
        }

        var scalar = Value(material, def.Key);
        var curve = Value(material, def.TemperatureKey);

        if (at is not { } temperature)
        {
            if (scalar is not null)
                return ScalarOf(material, def, scalar, "stated", null);

            return curve is not null
                ? MaterialResolution.Refuse($"{def.Key} of '{material.Title}' is given only against temperature ({def.TemperatureKey}); say at which temperature.")
                : MaterialResolution.Missing;
        }

        if (curve is not null)
            return Interpolate(material, def, curve, temperature);

        if (scalar is null)
            return MaterialResolution.Missing;

        return singleValue
            ? ScalarOf(material, def, scalar, "single value, no temperature table", temperature.Kelvin)
            : MaterialResolution.Refuse($"{def.Key} of '{material.Title}' is a single value with no temperature table, so it cannot be read at {temperature}.");
    }

    private static CalcDataValue? Value(CalcDataRecord material, string symbol) =>
        material.Values.FirstOrDefault(v => string.Equals(v.Symbol, symbol, StringComparison.Ordinal));

    private static MaterialResolution ScalarOf(CalcDataRecord material, MaterialPropertyDef def, CalcDataValue value, string basis, double? kelvin)
    {
        if (value.Number is not { } number || value.Source is not { } source)
            return MaterialResolution.Refuse($"{def.Key} of '{material.Title}' has no number or no source.");

        var (unit, problem) = MaterialProperties.ParseUnit(def, value.Unit, def.Key);

        return problem is not null
            ? MaterialResolution.Refuse(problem)
            : new MaterialResolution(new MaterialValue(def.Key, def.SiUnit is null ? number : unit!.ToSi(number), def.SiUnit ?? (value.Unit ?? string.Empty), basis, source, kelvin), null, source, false, null);
    }

    private static MaterialResolution Interpolate(CalcDataRecord material, MaterialPropertyDef def, CalcDataValue curve, MaterialTemperature at)
    {
        if (curve.Rows is not { Count: >= 2 } rows || curve.Source is not { } source)
            return MaterialResolution.Refuse($"{def.TemperatureKey} of '{material.Title}' is not a table of at least two rows.");

        var points = new List<(double T, double V)>();

        foreach (var row in rows)
        {
            var t = row.Get(MaterialProperties.TemperatureColumn);
            var v = row.Get(MaterialProperties.ValueColumn);
            var (tUnit, tProblem) = MaterialProperties.ParseTemperatureUnit(t?.Unit, def.TemperatureKey);
            var (vUnit, vProblem) = MaterialProperties.ParseUnit(def, v?.Unit, def.TemperatureKey);

            if (t?.Number is not { } tn || v?.Number is not { } vn || tProblem is not null || vProblem is not null)
                return MaterialResolution.Refuse($"{def.TemperatureKey} of '{material.Title}' has a row without a temperature or a value in a unit of the right kind.");

            points.Add((tUnit!.ToSi(tn), vUnit!.ToSi(vn)));
        }

        points.Sort((a, b) => a.T.CompareTo(b.T));
        var (lo, hi) = (points[0].T, points[^1].T);
        var slack = TemperatureSlack * Math.Max(1.0, hi);
        var unit = UnitCatalog.Parse(at.Unit);

        if (at.Kelvin < lo - slack || at.Kelvin > hi + slack)
        {
            return MaterialResolution.Refuse(
                $"{def.Key} of '{material.Title}' is tabulated from {unit.FromSi(lo).ToString("G6", CultureInfo.InvariantCulture)} to {unit.FromSi(hi).ToString("G6", CultureInfo.InvariantCulture)} {at.Unit}; {at} is outside that range and is not extrapolated.");
        }

        var t0 = Math.Clamp(at.Kelvin, lo, hi);

        for (var i = 0; i < points.Count; i++)
        {
            if (Math.Abs(points[i].T - t0) <= slack)
                return Resolved(points[i].V, $"tabulated at {unit.FromSi(points[i].T).ToString("G6", CultureInfo.InvariantCulture)} {at.Unit}");

            if (i + 1 < points.Count && t0 > points[i].T && t0 < points[i + 1].T)
            {
                var f = (t0 - points[i].T) / (points[i + 1].T - points[i].T);
                var value = points[i].V + f * (points[i + 1].V - points[i].V);
                return Resolved(value, $"interpolated between {unit.FromSi(points[i].T).ToString("G6", CultureInfo.InvariantCulture)} and {unit.FromSi(points[i + 1].T).ToString("G6", CultureInfo.InvariantCulture)} {at.Unit}");
            }
        }

        return MaterialResolution.Refuse($"{def.TemperatureKey} of '{material.Title}' could not be read at {at}.");

        MaterialResolution Resolved(double si, string basis) =>
            new(new MaterialValue(def.Key, si, def.SiUnit ?? string.Empty, basis, source, at.Kelvin), null, source, false, null);
    }

    /// <summary>The temperature (kelvin) a provenance reference says its value was read at, or <see langword="null"/>.</summary>
    internal static MaterialTemperature? TemperatureOf(CalcProvenance? provenance)
    {
        foreach (var part in (provenance?.Reference ?? string.Empty).Split(" · "))
        {
            if (part.StartsWith("T=", StringComparison.Ordinal) && part.EndsWith(" K", StringComparison.Ordinal)
                && double.TryParse(part[2..^2], NumberStyles.Float, CultureInfo.InvariantCulture, out var kelvin) && double.IsFinite(kelvin))
                return MaterialTemperature.OfKelvin(kelvin);
        }

        return null;
    }

    /// <summary>The provenance an input takes from a material: its pin, the property, any temperature and the value's own page or table, with the value's edition as revision.</summary>
    internal static CalcProvenance ProvenanceFor(CalcDataRecord material, string property, CalcValueSource source, MaterialTemperature? at, string basis)
    {
        var head = $"{material.Pin} · {property}";

        if (at is { } temperature)
            head += $" · T={temperature.Kelvin.ToString("R", CultureInfo.InvariantCulture)} K";

        if (basis.StartsWith("single value", StringComparison.Ordinal))
            head += " · single value";

        var room = CalcProvenance.MaxReferenceLength - head.Length - 3;
        var reference = room > 0 && source.Reference.Length > 0 ? $"{head} · {(source.Reference.Length > room ? source.Reference[..room] : source.Reference)}" : head;
        var edition = source.Edition.Length > CalcProvenance.MaxRevisionLength ? source.Edition[..CalcProvenance.MaxRevisionLength] : source.Edition;

        return new CalcProvenance(CalcProvenanceKind.Reference, reference.Length > CalcProvenance.MaxReferenceLength ? reference[..CalcProvenance.MaxReferenceLength] : reference, edition);
    }
}
