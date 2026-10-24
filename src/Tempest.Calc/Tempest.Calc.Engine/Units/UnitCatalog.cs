using System.Globalization;

namespace Tempest.Calc.Engine.Units;

/// <summary>
/// The engine's unit table and the parser for compound unit expressions such
/// as <c>kN*m</c>, <c>N/mm^2</c>, <c>W/(m*K)</c> or <c>m/s^2</c>. Built in,
/// immutable, SI plus the imperial units an engineer meets on a drawing.
/// </summary>
public static class UnitCatalog
{
    private const double Inch = 0.0254;
    private const double Pound = 0.45359237;
    private const double PoundForce = Pound * 9.80665;

    private static readonly Dictionary<string, Unit> Units = new(StringComparer.Ordinal);
    private static readonly HashSet<string> Prefixable = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, double> Prefixes = new(StringComparer.Ordinal)
    {
        ["T"] = 1e12, ["G"] = 1e9, ["M"] = 1e6, ["k"] = 1e3, ["h"] = 1e2, ["da"] = 1e1,
        ["d"] = 1e-1, ["c"] = 1e-2, ["m"] = 1e-3, ["µ"] = 1e-6, ["u"] = 1e-6, ["n"] = 1e-9, ["p"] = 1e-12,
    };

    /// <summary>The symbol the engine prefers to display a dimension in when the author names none.</summary>
    private static readonly Dictionary<Dimension, string> Preferred = new();

    static UnitCatalog()
    {
        // Base and coherent derived SI, prefixable.
        Add("m", Dimension.L, 1, prefixable: true);
        Add("g", Dimension.M, 1e-3, prefixable: true);
        Add("s", Dimension.T, 1, prefixable: true);
        Add("A", Dimension.I, 1, prefixable: true);
        Add("K", Dimension.Theta, 1, prefixable: true);
        Add("mol", Dimension.N, 1, prefixable: true);
        Add("cd", Dimension.J, 1);
        Add("N", Dimension.Force, 1, prefixable: true);
        Add("Pa", Dimension.Pressure, 1, prefixable: true);
        Add("J", Dimension.Energy, 1, prefixable: true);
        Add("W", Dimension.Power, 1, prefixable: true);
        Add("V", Dimension.Voltage, 1, prefixable: true);
        Add("Ω", Dimension.Resistance, 1, prefixable: true);
        Add("ohm", Dimension.Resistance, 1, prefixable: true);
        Add("C", Dimension.Charge, 1, prefixable: true);
        Add("Hz", Dimension.Frequency, 1, prefixable: true);
        Add("L", Dimension.Volume, 1e-3, prefixable: true);
        Add("l", Dimension.Volume, 1e-3, prefixable: true);
        Add("t", Dimension.M, 1000);
        Add("bar", Dimension.Pressure, 1e5, prefixable: true);
        Add("Wh", Dimension.Energy, 3600, prefixable: true);
        Add("VA", Dimension.Power, 1, prefixable: true);
        Add("var", Dimension.Power, 1, prefixable: true);

        // Non-prefixed SI-accepted and engineering units.
        Add("kg", Dimension.M, 1);
        Add("min", Dimension.T, 60);
        Add("h", Dimension.T, 3600);
        Add("d", Dimension.T, 86400);
        Add("rad", Dimension.None, 1);
        Add("deg", Dimension.None, Math.PI / 180);
        Add("°", Dimension.None, Math.PI / 180);
        Add("rev", Dimension.None, 2 * Math.PI);
        Add("rpm", Dimension.AngularVelocity, 2 * Math.PI / 60);
        Add("%", Dimension.None, 0.01);
        Add("ppm", Dimension.None, 1e-6);
        Add("degC", Dimension.Theta, 1, 273.15);
        Add("°C", Dimension.Theta, 1, 273.15);
        Add("degF", Dimension.Theta, 5.0 / 9.0, 459.67 * 5.0 / 9.0);
        Add("°F", Dimension.Theta, 5.0 / 9.0, 459.67 * 5.0 / 9.0);
        Add("ha", Dimension.Area, 1e4);
        Add("Nm", Dimension.Torque, 1);
        Add("kNm", Dimension.Torque, 1e3);
        Add("atm", Dimension.Pressure, 101325);
        Add("mmHg", Dimension.Pressure, 133.322387415);
        Add("mmH2O", Dimension.Pressure, 9.80665);
        Add("mWG", Dimension.Pressure, 9806.65);
        Add("P", Dimension.DynamicViscosity, 0.1, prefixable: true);
        Add("St", Dimension.KinematicViscosity, 1e-4, prefixable: true);
        Add("gf", Dimension.Force, 9.80665e-3, prefixable: true);

        // Imperial.
        Add("in", Dimension.L, Inch);
        Add("ft", Dimension.L, 12 * Inch);
        Add("yd", Dimension.L, 36 * Inch);
        Add("mi", Dimension.L, 63360 * Inch);
        Add("thou", Dimension.L, Inch / 1000);
        Add("mil", Dimension.L, Inch / 1000);
        Add("lb", Dimension.M, Pound);
        Add("lbm", Dimension.M, Pound);
        Add("oz", Dimension.M, Pound / 16);
        Add("lbf", Dimension.Force, PoundForce);
        Add("kip", Dimension.Force, 1000 * PoundForce);
        Add("psi", Dimension.Pressure, PoundForce / (Inch * Inch), prefixable: true);
        Add("ksi", Dimension.Pressure, 1000 * PoundForce / (Inch * Inch));
        Add("psf", Dimension.Pressure, PoundForce / (144 * Inch * Inch));
        Add("hp", Dimension.Power, 745.69987158227022);
        Add("Btu", Dimension.Energy, 1055.05585262);
        Add("gal", Dimension.Volume, 231 * Inch * Inch * Inch);
        Add("galUK", Dimension.Volume, 4.54609e-3);
        Add("cfm", Dimension.VolumeFlow, 12 * 12 * 12 * Inch * Inch * Inch / 60);
        Add("gpm", Dimension.VolumeFlow, 231 * Inch * Inch * Inch / 60);
        Add("inH2O", Dimension.Pressure, 249.08891);
        Add("inHg", Dimension.Pressure, 3386.389);

        Preferred[Dimension.None] = "";
        Preferred[Dimension.L] = "m";
        Preferred[Dimension.M] = "kg";
        Preferred[Dimension.T] = "s";
        Preferred[Dimension.I] = "A";
        Preferred[Dimension.Theta] = "K";
        Preferred[Dimension.Area] = "m^2";
        Preferred[Dimension.Volume] = "m^3";
        Preferred[Dimension.Force] = "N";
        Preferred[Dimension.Pressure] = "Pa";
        Preferred[Dimension.Energy] = "J";
        Preferred[Dimension.Power] = "W";
        Preferred[Dimension.Voltage] = "V";
        Preferred[Dimension.Resistance] = "Ω";
        Preferred[Dimension.Frequency] = "Hz";
        Preferred[Dimension.Velocity] = "m/s";
        Preferred[Dimension.Acceleration] = "m/s^2";
        Preferred[Dimension.Density] = "kg/m^3";
        Preferred[Dimension.SecondMomentOfArea] = "m^4";
        Preferred[Dimension.VolumeFlow] = "m^3/s";
        Preferred[Dimension.MassFlow] = "kg/s";
        Preferred[Dimension.DynamicViscosity] = "Pa*s";
        Preferred[Dimension.KinematicViscosity] = "m^2/s";
        Preferred[Dimension.Stiffness] = "N/m";
        Preferred[Dimension.SpecificHeat] = "J/(kg*K)";
        Preferred[Dimension.ThermalConductivity] = "W/(m*K)";
        Preferred[Dimension.HeatTransferCoefficient] = "W/(m^2*K)";
        Preferred[Dimension.MomentOfInertia] = "kg*m^2";
        Preferred[Dimension.Charge] = "C";
    }

    private static void Add(string symbol, Dimension dimension, double factor, double offset = 0.0, bool prefixable = false)
    {
        Units[symbol] = new Unit(symbol, dimension, factor, offset);

        if (prefixable)
            Prefixable.Add(symbol);
    }

    /// <summary>Every unit symbol the catalogue knows outright (prefixed forms are derived on parse).</summary>
    public static IReadOnlyCollection<string> Symbols => Units.Keys;

    /// <summary>Resolves one atomic symbol, with an SI prefix where the base symbol allows one.</summary>
    public static bool TryGetAtomic(string symbol, out Unit unit)
    {
        if (Units.TryGetValue(symbol, out var direct))
        {
            unit = direct;
            return true;
        }

        foreach (var (prefix, scale) in Prefixes)
        {
            if (symbol.Length > prefix.Length && symbol.StartsWith(prefix, StringComparison.Ordinal))
            {
                var rest = symbol[prefix.Length..];

                if (Prefixable.Contains(rest) && Units.TryGetValue(rest, out var baseUnit))
                {
                    unit = new Unit(symbol, baseUnit.Dimension, baseUnit.Factor * scale, baseUnit.Offset);
                    return true;
                }
            }
        }

        unit = Unit.One;
        return false;
    }

    /// <summary>
    /// Parses a compound unit: atoms joined by <c>*</c>, <c>·</c> or <c>/</c>,
    /// each with an optional integer power (<c>^2</c>, <c>²</c>, <c>2</c> as
    /// in <c>m2</c>), with parentheses. A unit expression is at most 64
    /// characters. An affine unit (°C, °F) may only stand alone.
    /// </summary>
    public static Unit Parse(string text)
    {
        if (!TryParse(text, out var unit, out var error))
            throw new CalcUnitException(error);

        return unit;
    }

    public static bool TryParse(string text, out Unit unit, out string error)
    {
        unit = Unit.One;
        error = string.Empty;

        if (text is null)
        {
            error = "No unit given.";
            return false;
        }

        var s = text.Trim();

        if (s.Length == 0 || s == "1")
            return true;

        if (s.Length > 64)
        {
            error = "A unit expression is at most 64 characters.";
            return false;
        }

        var parser = new UnitExpressionParser(s);

        try
        {
            unit = parser.ParseAll();
            return true;
        }
        catch (CalcUnitException e)
        {
            error = e.Message;
            return false;
        }
    }

    /// <summary>The unit the engine shows a dimension in when the author named none: a coherent SI symbol.</summary>
    public static Unit PreferredFor(Dimension dimension)
    {
        if (Preferred.TryGetValue(dimension, out var symbol))
            return symbol.Length == 0 ? Unit.One : Parse(symbol);

        return new Unit(dimension.BaseSymbol(), dimension, 1.0);
    }

    private sealed class UnitExpressionParser(string text)
    {
        private readonly string _text = text;
        private int _pos;

        public Unit ParseAll()
        {
            var unit = ParseProduct();
            SkipSpaces();

            if (_pos != _text.Length)
                throw new CalcUnitException($"Unexpected '{_text[_pos]}' in unit '{_text}'.");

            if (unit.IsAffine && !string.Equals(unit.Symbol, _text.Trim(), StringComparison.Ordinal))
                throw new CalcUnitException($"The affine unit '{unit.Symbol}' can only stand alone, not in '{_text}'.");

            return unit;
        }

        private Unit ParseProduct()
        {
            var left = ParsePower();

            while (true)
            {
                SkipSpaces();

                if (_pos >= _text.Length)
                    return left;

                var c = _text[_pos];

                if (c is '*' or '·' or '.')
                {
                    _pos++;
                    var right = ParsePower();
                    left = Combine(left, right, 1);
                }
                else if (c == '/')
                {
                    _pos++;
                    var right = ParsePower();
                    left = Combine(left, right, -1);
                }
                else
                {
                    return left;
                }
            }
        }

        private static Unit Combine(Unit a, Unit b, int sign)
        {
            if (a.IsAffine || b.IsAffine)
                throw new CalcUnitException("An affine unit (°C, °F) cannot be combined with another unit; use K for a temperature difference.");

            var dimension = sign > 0 ? a.Dimension * b.Dimension : a.Dimension / b.Dimension;
            var factor = sign > 0 ? a.Factor * b.Factor : a.Factor / b.Factor;
            var symbol = a.Symbol.Length == 0 ? (sign > 0 ? b.Symbol : $"1/{Wrap(b.Symbol)}") : $"{a.Symbol}{(sign > 0 ? "·" : "/")}{Wrap(b.Symbol)}";
            return new Unit(symbol, dimension, factor);
        }

        private static string Wrap(string symbol) => symbol.Contains('/', StringComparison.Ordinal) || symbol.Contains('·', StringComparison.Ordinal) ? $"({symbol})" : symbol;

        private Unit ParsePower()
        {
            var atom = ParseAtom();
            SkipSpaces();

            var exponent = TryReadExponent();

            if (exponent is null)
                return atom;

            if (atom.IsAffine)
                throw new CalcUnitException($"The affine unit '{atom.Symbol}' cannot be raised to a power.");

            var n = exponent.Value;

            if (n == 0 || Math.Abs(n) > 8)
                throw new CalcUnitException($"A unit power must be between -8 and 8 and not zero, not {n}.");

            return new Unit($"{atom.Symbol}{Dimension.Superscript(n)}", atom.Dimension.Pow(n), Math.Pow(atom.Factor, n));
        }

        private int? TryReadExponent()
        {
            if (_pos >= _text.Length)
                return null;

            var c = _text[_pos];

            if (c == '^')
            {
                _pos++;
                var negative = false;

                if (_pos < _text.Length && (_text[_pos] == '-' || _text[_pos] == '+'))
                {
                    negative = _text[_pos] == '-';
                    _pos++;
                }

                var start = _pos;

                while (_pos < _text.Length && char.IsAsciiDigit(_text[_pos]))
                    _pos++;

                if (start == _pos)
                    throw new CalcUnitException("A unit power needs an integer after '^'.");

                var n = int.Parse(_text.AsSpan(start, _pos - start), CultureInfo.InvariantCulture);
                return negative ? -n : n;
            }

            const string superscripts = "⁰¹²³⁴⁵⁶⁷⁸⁹";

            if (superscripts.Contains(c, StringComparison.Ordinal) || c == '⁻')
            {
                var negative = c == '⁻';

                if (negative)
                    _pos++;

                var value = 0;
                var any = false;

                while (_pos < _text.Length && superscripts.IndexOf(_text[_pos], StringComparison.Ordinal) is var d and >= 0)
                {
                    value = value * 10 + d;
                    _pos++;
                    any = true;
                }

                if (!any)
                    throw new CalcUnitException("A superscript sign needs digits after it.");

                return negative ? -value : value;
            }

            if (char.IsAsciiDigit(c))
            {
                // "m2", "mm3": a trailing digit directly after an atom.
                var start = _pos;

                while (_pos < _text.Length && char.IsAsciiDigit(_text[_pos]))
                    _pos++;

                return int.Parse(_text.AsSpan(start, _pos - start), CultureInfo.InvariantCulture);
            }

            return null;
        }

        private Unit ParseAtom()
        {
            SkipSpaces();

            if (_pos >= _text.Length)
                throw new CalcUnitException($"Unit '{_text}' ends unexpectedly.");

            if (_text[_pos] == '(')
            {
                _pos++;
                var inner = ParseProduct();
                SkipSpaces();

                if (_pos >= _text.Length || _text[_pos] != ')')
                    throw new CalcUnitException($"Missing ')' in unit '{_text}'.");

                _pos++;
                return inner;
            }

            if (_text[_pos] == '1')
            {
                _pos++;
                return Unit.One;
            }

            var start = _pos;

            while (_pos < _text.Length && (IsAtomChar(_text[_pos]) || char.IsAsciiDigit(_text[_pos])))
                _pos++;

            if (start == _pos)
                throw new CalcUnitException($"Unexpected '{_text[_pos]}' in unit '{_text}'.");

            // Longest known symbol wins ("mmH2O" before "mm" + exponent "2"); a trailing digit run is an exponent.
            for (var end = _pos; end > start; end--)
            {
                var candidate = _text[start..end];

                if (TryGetAtomic(candidate, out var unit))
                {
                    _pos = end;
                    return unit;
                }

                if (!char.IsAsciiDigit(_text[end - 1]) && end < _pos)
                    break;
            }

            throw new CalcUnitException($"Unknown unit '{_text[start.._pos]}'.");
        }

        private static bool IsAtomChar(char c) => char.IsLetter(c) || c is '°' or 'µ' or 'Ω' or '%' or '‰';

        private void SkipSpaces()
        {
            while (_pos < _text.Length && _text[_pos] == ' ')
                _pos++;
        }
    }
}
