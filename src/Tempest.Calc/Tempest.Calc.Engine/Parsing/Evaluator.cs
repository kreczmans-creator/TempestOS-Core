using System.Globalization;
using Tempest.Calc.Engine.Numerics;
using Tempest.Calc.Engine.Units;
using Tempest.Calc.Engine.Values;

namespace Tempest.Calc.Engine.Parsing;

/// <summary>Evaluates a parsed tree against a variable scope. Pure: no I/O, no clock, no randomness, no reflection.</summary>
internal sealed class Evaluator(Func<string, CalcValue?> lookup, CalcLimits limits)
{
    private readonly Func<string, CalcValue?> _lookup = lookup;
    private readonly CalcLimits _limits = limits;

    public CalcValue Evaluate(Node node)
    {
        switch (node)
        {
            case NumberNode n:
            {
                if (n.Unit is null)
                    return new ScalarValue(n.Value);

                if (!UnitCatalog.TryParse(n.Unit, out var unit, out var error))
                    throw new CalcUnitException($"{error} (after '{n.Literal}')");

                return ScalarValue.Of(n.Value, unit);
            }

            case TextNode t:
                return new TextValue(t.Value);

            case BoolNode b:
                return b.Value ? BoolValue.True : BoolValue.False;

            case IdentifierNode id:
            {
                if (Constants.TryGetValue(id.Name, out var constant))
                    return constant;

                return _lookup(id.Name) ?? throw new CalcEvaluationException($"Unknown symbol '{id.Name}'.");
            }

            case UnaryNode u:
            {
                var v = Evaluate(u.Operand);

                return u.Operator switch
                {
                    "-" => Negate(v),
                    "not" => v is BoolValue bv ? new BoolValue(!bv.Value) : throw new CalcEvaluationException("'not' needs a truth value."),
                    _ => throw new CalcEvaluationException($"Unknown operator '{u.Operator}'."),
                };
            }

            case BinaryNode bin:
                return Binary(bin);

            case ConvertNode c:
            {
                var v = Evaluate(c.Operand);

                if (!UnitCatalog.TryParse(c.Unit, out var unit, out var error))
                    throw new CalcUnitException(error);

                return v.In(unit);
            }

            case CallNode call:
                return Call(call);

            case IndexNode ix:
                return Index(ix);

            case ArrayNode arr:
                return Array(arr);

            default:
                throw new CalcEvaluationException("Unsupported expression node.");
        }
    }

    private static readonly Dictionary<string, CalcValue> Constants = new(StringComparer.Ordinal)
    {
        ["pi"] = new ScalarValue(Math.PI),
        ["π"] = new ScalarValue(Math.PI),
        ["e"] = new ScalarValue(Math.E),
        ["g_n"] = ScalarValue.Of(9.80665, "m/s^2"),
    };

    private static CalcValue Negate(CalcValue v) => v switch
    {
        ScalarValue s => new ScalarValue(-s.Si, s.Dimension, s.DisplayUnit),
        VectorValue vec => new VectorValue(vec.Si.Select(x => -x).ToArray(), vec.Dimension, vec.DisplayUnit),
        MatrixValue m => MapMatrix(m, x => -x, m.Dimension, m.DisplayUnit),
        _ => throw new CalcEvaluationException("Only a number, vector or matrix can be negated."),
    };

    private CalcValue Binary(BinaryNode bin)
    {
        if (bin.Operator is "and" or "or")
        {
            var l = Evaluate(bin.Left) as BoolValue ?? throw new CalcEvaluationException($"'{bin.Operator}' needs truth values.");

            if (bin.Operator == "and" && !l.Value)
                return BoolValue.False;

            if (bin.Operator == "or" && l.Value)
                return BoolValue.True;

            var r = Evaluate(bin.Right) as BoolValue ?? throw new CalcEvaluationException($"'{bin.Operator}' needs truth values.");
            return r;
        }

        var left = Evaluate(bin.Left);
        var right = Evaluate(bin.Right);

        switch (bin.Operator)
        {
            case "+": return Add(left, right, 1.0, "+");
            case "-": return Add(left, right, -1.0, "−");
            case "*": return Multiply(left, right);
            case "/": return Divide(left, right);
            case "^": return Power(left, right);
            case "<" or "<=" or ">" or ">=" or "==" or "!=":
            {
                if (left is TextValue lt && right is TextValue rt)
                {
                    return bin.Operator switch
                    {
                        "==" => new BoolValue(string.Equals(lt.Value, rt.Value, StringComparison.Ordinal)),
                        "!=" => new BoolValue(!string.Equals(lt.Value, rt.Value, StringComparison.Ordinal)),
                        _ => throw new CalcEvaluationException("Texts compare only with == and !=."),
                    };
                }

                if (left is BoolValue lb && right is BoolValue rb)
                {
                    return bin.Operator switch
                    {
                        "==" => new BoolValue(lb.Value == rb.Value),
                        "!=" => new BoolValue(lb.Value != rb.Value),
                        _ => throw new CalcEvaluationException("Truth values compare only with == and !=."),
                    };
                }

                var (a, b) = Scalars(left, right, bin.Operator);
                RequireSame(a, b, bin.Operator);

                return new BoolValue(bin.Operator switch
                {
                    "<" => a.Si < b.Si,
                    "<=" => a.Si <= b.Si,
                    ">" => a.Si > b.Si,
                    ">=" => a.Si >= b.Si,
                    "==" => a.Si == b.Si,
                    _ => a.Si != b.Si,
                });
            }

            default:
                throw new CalcEvaluationException($"Unknown operator '{bin.Operator}'.");
        }
    }

    private static (ScalarValue, ScalarValue) Scalars(CalcValue l, CalcValue r, string op)
    {
        if (l is ScalarValue a && r is ScalarValue b)
            return (a, b);

        throw new CalcEvaluationException($"'{op}' needs two numbers.");
    }

    private static void RequireSame(CalcValue a, CalcValue b, string op)
    {
        if (a.Dimension != b.Dimension)
            throw new CalcDimensionException($"Cannot apply '{op}' to [{Describe(a)}] and [{Describe(b)}]: the dimensions differ.");
    }

    private static string Describe(CalcValue v) => v.DisplayUnit.Symbol.Length > 0 ? v.DisplayUnit.Symbol : (v.Dimension.IsDimensionless ? "dimensionless" : v.Dimension.ToString());

    private static Unit ChooseUnit(CalcValue a, CalcValue b) => a.DisplayUnit.Symbol.Length > 0 || b.DisplayUnit.Symbol.Length == 0 ? a.DisplayUnit : b.DisplayUnit;

    private CalcValue Add(CalcValue l, CalcValue r, double sign, string op)
    {
        RequireSame(l, r, op);
        var unit = ChooseUnit(l, r);

        switch (l, r)
        {
            case (ScalarValue a, ScalarValue b):
                return new ScalarValue(a.Si + sign * b.Si, a.Dimension, unit);

            case (VectorValue a, VectorValue b):
            {
                if (a.Length != b.Length)
                    throw new CalcEvaluationException($"Cannot add vectors of length {a.Length} and {b.Length}.");

                var result = new double[a.Length];

                for (var i = 0; i < result.Length; i++)
                    result[i] = a.Si[i] + sign * b.Si[i];

                return new VectorValue(result, a.Dimension, unit);
            }

            case (VectorValue a, ScalarValue b):
                return new VectorValue(a.Si.Select(x => x + sign * b.Si).ToArray(), a.Dimension, unit);

            case (ScalarValue a, VectorValue b):
                return new VectorValue(b.Si.Select(x => a.Si + sign * x).ToArray(), a.Dimension, unit);

            case (MatrixValue a, MatrixValue b):
            {
                if (a.Rows != b.Rows || a.Columns != b.Columns)
                    throw new CalcEvaluationException($"Cannot add a {a.Rows}×{a.Columns} matrix and a {b.Rows}×{b.Columns} matrix.");

                var result = new double[a.Rows, a.Columns];

                for (var i = 0; i < a.Rows; i++)
                {
                    for (var j = 0; j < a.Columns; j++)
                        result[i, j] = a.Si(i, j) + sign * b.Si(i, j);
                }

                return new MatrixValue(result, a.Dimension, unit);
            }

            default:
                throw new CalcEvaluationException($"Cannot apply '{op}' to these values.");
        }
    }

    private CalcValue Multiply(CalcValue l, CalcValue r)
    {
        var dimension = l.Dimension * r.Dimension;

        switch (l, r)
        {
            case (ScalarValue a, ScalarValue b):
                return new ScalarValue(a.Si * b.Si, dimension, ProductUnit(a, b));

            case (ScalarValue a, VectorValue b):
                return new VectorValue(b.Si.Select(x => a.Si * x).ToArray(), dimension);

            case (VectorValue a, ScalarValue b):
                return new VectorValue(a.Si.Select(x => x * b.Si).ToArray(), dimension);

            case (ScalarValue a, MatrixValue b):
                return MapMatrix(b, x => a.Si * x, dimension, null);

            case (MatrixValue a, ScalarValue b):
                return MapMatrix(a, x => x * b.Si, dimension, null);

            case (VectorValue a, VectorValue b):
            {
                if (a.Length != b.Length)
                    throw new CalcEvaluationException($"Cannot multiply vectors of length {a.Length} and {b.Length} element-wise.");

                var result = new double[a.Length];

                for (var i = 0; i < result.Length; i++)
                    result[i] = a.Si[i] * b.Si[i];

                return new VectorValue(result, dimension);
            }

            case (MatrixValue a, MatrixValue b):
                CheckOrder(a.Rows, a.Columns);
                CheckOrder(b.Rows, b.Columns);
                return new MatrixValue(LinearAlgebra.Multiply(a.ToSiArray(), b.ToSiArray()), dimension);

            case (MatrixValue a, VectorValue b):
                CheckOrder(a.Rows, a.Columns);
                return new VectorValue(LinearAlgebra.Multiply(a.ToSiArray(), [.. b.Si]), dimension);

            default:
                throw new CalcEvaluationException("Cannot multiply these values.");
        }
    }

    private static Unit? ProductUnit(ScalarValue a, ScalarValue b)
    {
        // A dimensionless factor keeps the other operand's display unit: 2 * 40 mm is 80 mm.
        if (a.Dimension.IsDimensionless && a.DisplayUnit.Symbol.Length == 0)
            return b.DisplayUnit;

        if (b.Dimension.IsDimensionless && b.DisplayUnit.Symbol.Length == 0)
            return a.DisplayUnit;

        return null;
    }

    private CalcValue Divide(CalcValue l, CalcValue r)
    {
        var dimension = l.Dimension / r.Dimension;

        switch (l, r)
        {
            case (ScalarValue a, ScalarValue b):
            {
                if (b.Si == 0)
                    throw new CalcEvaluationException("Division by zero.");

                var unit = b.Dimension.IsDimensionless && b.DisplayUnit.Symbol.Length == 0 ? a.DisplayUnit : null;
                return new ScalarValue(a.Si / b.Si, dimension, unit);
            }

            case (VectorValue a, ScalarValue b):
            {
                if (b.Si == 0)
                    throw new CalcEvaluationException("Division by zero.");

                return new VectorValue(a.Si.Select(x => x / b.Si).ToArray(), dimension);
            }

            case (ScalarValue a, VectorValue b):
            {
                if (b.Si.Any(x => x == 0))
                    throw new CalcEvaluationException("Division by zero in a vector.");

                return new VectorValue(b.Si.Select(x => a.Si / x).ToArray(), dimension);
            }

            case (VectorValue a, VectorValue b):
            {
                if (a.Length != b.Length)
                    throw new CalcEvaluationException($"Cannot divide vectors of length {a.Length} and {b.Length} element-wise.");

                var result = new double[a.Length];

                for (var i = 0; i < result.Length; i++)
                {
                    if (b.Si[i] == 0)
                        throw new CalcEvaluationException("Division by zero in a vector.");

                    result[i] = a.Si[i] / b.Si[i];
                }

                return new VectorValue(result, dimension);
            }

            case (MatrixValue a, ScalarValue b):
            {
                if (b.Si == 0)
                    throw new CalcEvaluationException("Division by zero.");

                return MapMatrix(a, x => x / b.Si, dimension, null);
            }

            default:
                throw new CalcEvaluationException("Cannot divide these values.");
        }
    }

    private CalcValue Power(CalcValue l, CalcValue r)
    {
        if (r is not ScalarValue exponent || !exponent.Dimension.IsDimensionless)
            throw new CalcDimensionException("An exponent must be a dimensionless number.");

        var n = exponent.Si;

        switch (l)
        {
            case ScalarValue a:
            {
                if (a.Dimension.IsDimensionless)
                    return new ScalarValue(Math.Pow(a.Si, n));

                if (Math.Abs(n - Math.Round(n)) < 1e-12)
                {
                    var k = (int)Math.Round(n);

                    if (Math.Abs(k) > 8)
                        throw new CalcDimensionException("A dimensioned value can be raised to an integer power between −8 and 8 only.");

                    return new ScalarValue(Math.Pow(a.Si, k), a.Dimension.Pow(k), PowerUnit(a.DisplayUnit, k));
                }

                // Fractional power of a dimensioned value: allowed only when the dimension root exists (x^0.5 of an area).
                var inverse = 1.0 / n;

                if (Math.Abs(inverse - Math.Round(inverse)) < 1e-9 && a.Dimension.Root((int)Math.Round(inverse)) is { } root)
                    return new ScalarValue(Math.Pow(a.Si, n), root);

                throw new CalcDimensionException($"Cannot raise a value of dimension [{a.Dimension}] to the power {CalcValue.FormatNumber(n)}.");
            }

            case VectorValue v:
            {
                if (!v.Dimension.IsDimensionless)
                    throw new CalcDimensionException("Only a dimensionless vector can be raised to a power element-wise.");

                return new VectorValue(v.Si.Select(x => Math.Pow(x, n)).ToArray(), Dimension.None);
            }

            default:
                throw new CalcEvaluationException("Only a number can be raised to a power.");
        }
    }

    private static Unit? PowerUnit(Unit unit, int k)
    {
        if (unit.Symbol.Length == 0 || unit.IsAffine || k == 0)
            return null;

        return new Unit(k == 1 ? unit.Symbol : $"{Wrap(unit.Symbol)}{Dimension.Superscript(k)}", unit.Dimension.Pow(k), Math.Pow(unit.Factor, k));
    }

    private static string Wrap(string s) => s.Contains('/', StringComparison.Ordinal) || s.Contains('·', StringComparison.Ordinal) ? $"({s})" : s;

    private static MatrixValue MapMatrix(MatrixValue m, Func<double, double> f, Dimension dimension, Unit? unit)
    {
        var result = new double[m.Rows, m.Columns];

        for (var i = 0; i < m.Rows; i++)
        {
            for (var j = 0; j < m.Columns; j++)
                result[i, j] = f(m.Si(i, j));
        }

        return new MatrixValue(result, dimension, unit);
    }

    private void CheckOrder(int rows, int columns)
    {
        if (rows > _limits.MaxMatrixOrder || columns > _limits.MaxMatrixOrder)
            throw new CalcLimitException(nameof(CalcLimits.MaxMatrixOrder), $"A matrix is at most {_limits.MaxMatrixOrder} on a side.");

        if ((long)rows * columns > _limits.MaxArrayElements)
            throw new CalcLimitException(nameof(CalcLimits.MaxArrayElements), $"An array holds at most {_limits.MaxArrayElements} elements.");
    }

    private CalcValue Index(IndexNode ix)
    {
        var target = Evaluate(ix.Target);
        var first = IndexValue(Evaluate(ix.Indices[0]));

        if (target is VectorValue v)
        {
            if (ix.Indices.Count != 1)
                throw new CalcEvaluationException("A vector takes one index.");

            if (first < 1 || first > v.Length)
                throw new CalcEvaluationException($"Index {first} is outside the vector's 1…{v.Length}.");

            return v[first - 1];
        }

        if (target is MatrixValue m)
        {
            if (ix.Indices.Count != 2)
                throw new CalcEvaluationException("A matrix takes two indices: M[row, column].");

            var second = IndexValue(Evaluate(ix.Indices[1]));

            if (first < 1 || first > m.Rows || second < 1 || second > m.Columns)
                throw new CalcEvaluationException($"Index [{first}, {second}] is outside the matrix's {m.Rows}×{m.Columns}.");

            return m[first - 1, second - 1];
        }

        throw new CalcEvaluationException("Only a vector or a matrix can be indexed.");
    }

    private static int IndexValue(CalcValue v)
    {
        if (v is not ScalarValue s || !s.Dimension.IsDimensionless || Math.Abs(s.Si - Math.Round(s.Si)) > 1e-9)
            throw new CalcEvaluationException("An index must be a whole dimensionless number.");

        return (int)Math.Round(s.Si);
    }

    private CalcValue Array(ArrayNode arr)
    {
        var rows = arr.Rows;

        if (rows.Count == 1)
        {
            var items = rows[0].Select(Evaluate).ToList();

            if (items.Count == 0)
                return new VectorValue([], Dimension.None);

            if (items.Any(i => i is not ScalarValue))
                throw new CalcEvaluationException("An array literal holds numbers only.");

            var scalars = items.Cast<ScalarValue>().ToList();
            var dimension = scalars[0].Dimension;

            if (scalars.Any(s => s.Dimension != dimension))
                throw new CalcDimensionException("Every element of an array must share one dimension.");

            return new VectorValue(scalars.Select(s => s.Si).ToArray(), dimension, scalars[0].DisplayUnit);
        }

        var columns = rows[0].Count;

        if (columns == 0 || rows.Any(r => r.Count != columns))
            throw new CalcEvaluationException("Every row of a matrix literal must have the same, non-zero, number of elements.");

        CheckOrder(rows.Count, columns);
        var matrix = new double[rows.Count, columns];
        Dimension? dim = null;
        Unit? unit = null;

        for (var i = 0; i < rows.Count; i++)
        {
            for (var j = 0; j < columns; j++)
            {
                if (Evaluate(rows[i][j]) is not ScalarValue s)
                    throw new CalcEvaluationException("A matrix literal holds numbers only.");

                dim ??= s.Dimension;
                unit ??= s.DisplayUnit;

                if (s.Dimension != dim)
                    throw new CalcDimensionException("Every element of a matrix must share one dimension.");

                matrix[i, j] = s.Si;
            }
        }

        return new MatrixValue(matrix, dim ?? Dimension.None, unit);
    }

    private CalcValue Call(CallNode call)
    {
        var name = call.Name;

        if (name == "if")
        {
            if (call.Arguments.Count != 3)
                throw new CalcEvaluationException("if(condition, then, else) takes three arguments.");

            var cond = Evaluate(call.Arguments[0]) as BoolValue ?? throw new CalcEvaluationException("if needs a truth value as its condition.");
            return Evaluate(call.Arguments[cond.Value ? 1 : 2]);
        }

        var args = call.Arguments.Select(Evaluate).ToList();

        return name switch
        {
            "sin" => Trig(args, Math.Sin, name),
            "cos" => Trig(args, Math.Cos, name),
            "tan" => Trig(args, Math.Tan, name),
            "asin" => Angle(args, Math.Asin, name),
            "acos" => Angle(args, Math.Acos, name),
            "atan" => Angle(args, Math.Atan, name),
            "sinh" => Trig(args, Math.Sinh, name),
            "cosh" => Trig(args, Math.Cosh, name),
            "tanh" => Trig(args, Math.Tanh, name),
            "atan2" => Atan2(args),
            "exp" => Dimensionless(args, Math.Exp, name),
            "ln" => Dimensionless(args, Log, name),
            "log" => Dimensionless(args, Log, name),
            "log10" => Dimensionless(args, Log10, name),
            "log2" => Dimensionless(args, Math.Log2, name),
            "sqrt" => Root(args, 2),
            "cbrt" => Root(args, 3),
            "abs" => Map(args, Math.Abs, name),
            "sign" => Map(args, x => Math.Sign(x), name, dimensionless: true),
            "floor" => RoundLike(args, Math.Floor, name),
            "ceil" => RoundLike(args, Math.Ceiling, name),
            "round" => Round(args),
            "min" => Extremum(args, false),
            "max" => Extremum(args, true),
            "clamp" => Clamp(args),
            "sum" => Sum(args),
            "avg" => Average(args),
            "len" => Length(args),
            "mod" => Mod(args),
            "pow" => args.Count == 2 ? Power(args[0], args[1]) : throw new CalcEvaluationException("pow(x, n) takes two arguments."),
            "hypot" => Hypot(args),
            "interp" => Interp(args),
            "dot" => Dot(args),
            "norm" => Norm(args),
            "transpose" => Transpose(args),
            "det" => Det(args),
            "inv" => Inv(args),
            "solve" => SolveCall(args),
            "vec" => VecCall(args),
            "isfinite" => args.Count == 1 && args[0] is ScalarValue s ? new BoolValue(double.IsFinite(s.Si)) : throw new CalcEvaluationException("isfinite(x) takes one number."),
            _ => throw new CalcEvaluationException($"Unknown function '{name}'."),
        };
    }

    private static double Log(double x) => x > 0 ? Math.Log(x) : throw new CalcEvaluationException("ln needs a positive argument.");

    private static double Log10(double x) => x > 0 ? Math.Log10(x) : throw new CalcEvaluationException("log10 needs a positive argument.");

    private static ScalarValue One(IReadOnlyList<CalcValue> args, string name)
    {
        if (args.Count != 1 || args[0] is not ScalarValue s)
            throw new CalcEvaluationException($"{name}(x) takes one number.");

        return s;
    }

    private static CalcValue Trig(IReadOnlyList<CalcValue> args, Func<double, double> f, string name)
    {
        var s = One(args, name);

        if (!s.Dimension.IsDimensionless)
            throw new CalcDimensionException($"{name} needs an angle (dimensionless, rad or deg), not [{s.Dimension}].");

        return new ScalarValue(f(s.Si));
    }

    private static CalcValue Angle(IReadOnlyList<CalcValue> args, Func<double, double> f, string name)
    {
        var s = One(args, name);

        if (!s.Dimension.IsDimensionless)
            throw new CalcDimensionException($"{name} needs a dimensionless argument.");

        var r = f(s.Si);

        if (double.IsNaN(r))
            throw new CalcEvaluationException($"{name}({CalcValue.FormatNumber(s.Si)}) is undefined.");

        return new ScalarValue(r, Dimension.None, UnitCatalog.Parse("rad"));
    }

    private static CalcValue Atan2(IReadOnlyList<CalcValue> args)
    {
        if (args.Count != 2 || args[0] is not ScalarValue y || args[1] is not ScalarValue x)
            throw new CalcEvaluationException("atan2(y, x) takes two numbers.");

        RequireSame(y, x, "atan2");
        return new ScalarValue(Math.Atan2(y.Si, x.Si), Dimension.None, UnitCatalog.Parse("rad"));
    }

    private static CalcValue Dimensionless(IReadOnlyList<CalcValue> args, Func<double, double> f, string name)
    {
        var s = One(args, name);

        if (!s.Dimension.IsDimensionless)
            throw new CalcDimensionException($"{name} needs a dimensionless argument, not [{s.Dimension}].");

        return new ScalarValue(f(s.Si));
    }

    private static CalcValue Root(IReadOnlyList<CalcValue> args, int n)
    {
        var name = n == 2 ? "sqrt" : "cbrt";
        var s = One(args, name);
        var root = s.Dimension.Root(n) ?? throw new CalcDimensionException($"{name} of a value of dimension [{s.Dimension}] has no whole dimension.");

        if (n == 2 && s.Si < 0)
            throw new CalcEvaluationException("sqrt of a negative number is undefined.");

        var value = n == 2 ? Math.Sqrt(s.Si) : Math.Cbrt(s.Si);
        return new ScalarValue(value, root);
    }

    private static CalcValue Map(IReadOnlyList<CalcValue> args, Func<double, double> f, string name, bool dimensionless = false)
    {
        if (args.Count != 1)
            throw new CalcEvaluationException($"{name}(x) takes one argument.");

        return args[0] switch
        {
            ScalarValue s => dimensionless ? new ScalarValue(f(s.Si)) : new ScalarValue(f(s.Si), s.Dimension, s.DisplayUnit),
            VectorValue v => dimensionless ? new VectorValue(v.Si.Select(f).ToArray(), Dimension.None) : new VectorValue(v.Si.Select(f).ToArray(), v.Dimension, v.DisplayUnit),
            _ => throw new CalcEvaluationException($"{name} takes a number or a vector."),
        };
    }

    private static CalcValue RoundLike(IReadOnlyList<CalcValue> args, Func<double, double> f, string name)
    {
        // Rounds in the display unit, which is what an engineer means by floor(40.6 mm).
        var s = One(args, name);
        return new ScalarValue(s.DisplayUnit.ToSi(f(s.Displayed)), s.Dimension, s.DisplayUnit);
    }

    private static CalcValue Round(IReadOnlyList<CalcValue> args)
    {
        if (args.Count is < 1 or > 2 || args[0] is not ScalarValue s)
            throw new CalcEvaluationException("round(x[, decimals]) takes a number and an optional whole number of decimals.");

        var decimals = 0;

        if (args.Count == 2)
            decimals = Math.Clamp(IndexValue(args[1]), 0, 15);

        return new ScalarValue(s.DisplayUnit.ToSi(Math.Round(s.Displayed, decimals, MidpointRounding.AwayFromZero)), s.Dimension, s.DisplayUnit);
    }

    /// <summary>The SI values of every argument (numbers and vectors) of one dimension, as a span — never one boxed value per element (R-10); the total is capped before anything is copied.</summary>
    private (double[] Si, Dimension Dimension, Unit DisplayUnit) Flatten(IReadOnlyList<CalcValue> args, string name)
    {
        long total = 0;
        Dimension? dim = null;
        Unit? unit = null;

        foreach (var a in args)
        {
            switch (a)
            {
                case ScalarValue s:
                    total += 1;
                    dim ??= s.Dimension;
                    unit ??= s.DisplayUnit;
                    if (s.Dimension != dim) throw new CalcDimensionException($"{name} needs values of one dimension.");
                    break;
                case VectorValue v:
                    total += v.Length;
                    dim ??= v.Dimension;
                    unit ??= v.DisplayUnit;
                    if (v.Dimension != dim) throw new CalcDimensionException($"{name} needs values of one dimension.");
                    break;
                default: throw new CalcEvaluationException($"{name} takes numbers or vectors.");
            }

            if (total > _limits.MaxArrayElements)
                throw new CalcLimitException(nameof(CalcLimits.MaxArrayElements), $"{name} takes at most {_limits.MaxArrayElements} values in all.");
        }

        if (total == 0)
            throw new CalcEvaluationException($"{name} needs at least one value.");

        var si = new double[total];
        var k = 0;

        foreach (var a in args)
        {
            switch (a)
            {
                case ScalarValue s: si[k++] = s.Si; break;
                case VectorValue v:
                    for (var i = 0; i < v.Length; i++) si[k++] = v.Si[i];
                    break;
            }
        }

        return (si, dim!.Value, unit!);
    }

    private CalcValue Extremum(IReadOnlyList<CalcValue> args, bool max)
    {
        var (si, dim, unit) = Flatten(args, max ? "max" : "min");
        var best = si[0];

        foreach (var v in si)
        {
            if (max ? v > best : v < best)
                best = v;
        }

        return new ScalarValue(best, dim, unit);
    }

    private static CalcValue Clamp(IReadOnlyList<CalcValue> args)
    {
        if (args.Count != 3 || args[0] is not ScalarValue x || args[1] is not ScalarValue lo || args[2] is not ScalarValue hi)
            throw new CalcEvaluationException("clamp(x, lo, hi) takes three numbers.");

        RequireSame(x, lo, "clamp");
        RequireSame(x, hi, "clamp");

        if (lo.Si > hi.Si)
            throw new CalcEvaluationException("clamp needs lo <= hi.");

        return new ScalarValue(Math.Clamp(x.Si, lo.Si, hi.Si), x.Dimension, x.DisplayUnit);
    }

    private CalcValue Sum(IReadOnlyList<CalcValue> args)
    {
        var (si, dim, unit) = Flatten(args, "sum");
        double sum = 0;
        foreach (var v in si) sum += v;
        return new ScalarValue(sum, dim, unit);
    }

    private CalcValue Average(IReadOnlyList<CalcValue> args)
    {
        var (si, dim, unit) = Flatten(args, "avg");
        double sum = 0;
        foreach (var v in si) sum += v;
        return new ScalarValue(sum / si.Length, dim, unit);
    }

    private static CalcValue Length(IReadOnlyList<CalcValue> args)
    {
        if (args.Count != 1)
            throw new CalcEvaluationException("len(v) takes one vector or matrix.");

        return args[0] switch
        {
            VectorValue v => new ScalarValue(v.Length),
            MatrixValue m => new ScalarValue(m.Rows),
            _ => throw new CalcEvaluationException("len(v) takes one vector or matrix."),
        };
    }

    private static CalcValue Mod(IReadOnlyList<CalcValue> args)
    {
        if (args.Count != 2 || args[0] is not ScalarValue a || args[1] is not ScalarValue b)
            throw new CalcEvaluationException("mod(a, b) takes two numbers.");

        RequireSame(a, b, "mod");

        if (b.Si == 0)
            throw new CalcEvaluationException("Modulo by zero.");

        return new ScalarValue(a.Si - b.Si * Math.Floor(a.Si / b.Si), a.Dimension, a.DisplayUnit);
    }

    private CalcValue Hypot(IReadOnlyList<CalcValue> args)
    {
        var (si, dim, unit) = Flatten(args, "hypot");
        double sum = 0;
        foreach (var v in si) sum += v * v;
        return new ScalarValue(Math.Sqrt(sum), dim, unit);
    }

    private static CalcValue Interp(IReadOnlyList<CalcValue> args)
    {
        if (args.Count is < 3 or > 4 || args[0] is not ScalarValue x || args[1] is not VectorValue xs || args[2] is not VectorValue ys)
            throw new CalcEvaluationException("interp(x, xs, ys[, extrapolate]) takes a number and two vectors.");

        RequireSame(x, xs, "interp");
        var extrapolate = args.Count == 4 && args[3] is BoolValue { Value: true };
        var y = Interpolation.Linear(x.Si, xs.Si, ys.Si, extrapolate);
        return new ScalarValue(y, ys.Dimension, ys.DisplayUnit);
    }

    private static CalcValue Dot(IReadOnlyList<CalcValue> args)
    {
        if (args.Count != 2 || args[0] is not VectorValue a || args[1] is not VectorValue b)
            throw new CalcEvaluationException("dot(a, b) takes two vectors.");

        if (a.Length != b.Length)
            throw new CalcEvaluationException($"dot needs vectors of one length, not {a.Length} and {b.Length}.");

        var s = 0.0;

        for (var i = 0; i < a.Length; i++)
            s += a.Si[i] * b.Si[i];

        return new ScalarValue(s, a.Dimension * b.Dimension);
    }

    private static CalcValue Norm(IReadOnlyList<CalcValue> args)
    {
        if (args.Count != 1 || args[0] is not VectorValue a)
            throw new CalcEvaluationException("norm(v) takes one vector.");

        return new ScalarValue(Math.Sqrt(a.Si.Sum(x => x * x)), a.Dimension, a.DisplayUnit);
    }

    private static CalcValue Transpose(IReadOnlyList<CalcValue> args)
    {
        if (args.Count != 1 || args[0] is not MatrixValue m)
            throw new CalcEvaluationException("transpose(M) takes one matrix.");

        return new MatrixValue(LinearAlgebra.Transpose(m.ToSiArray()), m.Dimension, m.DisplayUnit);
    }

    private CalcValue Det(IReadOnlyList<CalcValue> args)
    {
        if (args.Count != 1 || args[0] is not MatrixValue m)
            throw new CalcEvaluationException("det(M) takes one matrix.");

        CheckOrder(m.Rows, m.Columns);
        return new ScalarValue(LinearAlgebra.Determinant(m.ToSiArray()), m.Dimension.Pow(m.Rows));
    }

    private CalcValue Inv(IReadOnlyList<CalcValue> args)
    {
        if (args.Count != 1 || args[0] is not MatrixValue m)
            throw new CalcEvaluationException("inv(M) takes one matrix.");

        CheckOrder(m.Rows, m.Columns);
        return new MatrixValue(LinearAlgebra.Inverse(m.ToSiArray()), Dimension.None / m.Dimension);
    }

    private CalcValue SolveCall(IReadOnlyList<CalcValue> args)
    {
        if (args.Count != 2 || args[0] is not MatrixValue a || args[1] is not VectorValue b)
            throw new CalcEvaluationException("solve(A, b) takes a matrix and a vector.");

        CheckOrder(a.Rows, a.Columns);
        return new VectorValue(LinearAlgebra.Solve(a.ToSiArray(), [.. b.Si]), b.Dimension / a.Dimension);
    }

    private CalcValue VecCall(IReadOnlyList<CalcValue> args)
    {
        var (si, dim, unit) = Flatten(args, "vec");
        return new VectorValue(si, dim, unit);
    }

    /// <summary>Renders <paramref name="node"/> with every symbol replaced by its current formatted value: the substitution line of a step.</summary>
    public string Substitute(Node node, int significantFigures)
    {
        return node switch
        {
            NumberNode n => n.Unit is null ? n.Literal : $"{n.Literal} {n.Unit}",
            TextNode t => $"\"{t.Value}\"",
            BoolNode b => b.Value ? "true" : "false",
            IdentifierNode id => Constants.ContainsKey(id.Name) ? (id.Name == "pi" ? "π" : id.Name) : (_lookup(id.Name)?.Format(significantFigures) ?? id.Name),
            UnaryNode u => u.Operator == "not" ? $"not {Substitute(u.Operand, significantFigures)}" : $"−{WrapIfCompound(u.Operand, significantFigures)}",
            BinaryNode b => b.Operator == "^"
                ? $"{WrapIf(b, b.Left, significantFigures, false)}^{WrapIf(b, b.Right, significantFigures, true)}"
                : $"{WrapIf(b, b.Left, significantFigures, false)} {Symbol(b.Operator)} {WrapIf(b, b.Right, significantFigures, true)}",
            ConvertNode c => $"{Substitute(c.Operand, significantFigures)} in {c.Unit}",
            CallNode c => $"{c.Name}({string.Join(", ", c.Arguments.Select(a => Substitute(a, significantFigures)))})",
            IndexNode ix => $"{Substitute(ix.Target, significantFigures)}[{string.Join(", ", ix.Indices.Select(a => Substitute(a, significantFigures)))}]",
            ArrayNode a => "[" + string.Join("; ", a.Rows.Select(r => string.Join(", ", r.Select(x => Substitute(x, significantFigures))))) + "]",
            _ => "?",
        };
    }

    private static string Symbol(string op) => op switch
    {
        "*" => "×",
        "-" => "−",
        "<=" => "≤",
        ">=" => "≥",
        "!=" => "≠",
        _ => op,
    };

    private static int Precedence(string op) => op switch
    {
        "or" => 1,
        "and" => 2,
        "<" or "<=" or ">" or ">=" or "==" or "!=" => 3,
        "+" or "-" => 4,
        "*" or "/" => 5,
        "^" => 6,
        _ => 7,
    };

    private string WrapIf(BinaryNode parent, Node child, int sig, bool right)
    {
        var text = Substitute(child, sig);

        if (child is BinaryNode b)
        {
            var pp = Precedence(parent.Operator);
            var cp = Precedence(b.Operator);

            if (cp < pp || (cp == pp && right && parent.Operator is "-" or "/" or "^"))
                return $"({text})";
        }
        else if (child is UnaryNode && parent.Operator == "^")
        {
            return $"({text})";
        }
        else if (child is IdentifierNode or NumberNode && text.StartsWith('-') && parent.Operator is not "+" )
        {
            return $"({text})";
        }
        else if (child is IdentifierNode && text.Contains(' ', StringComparison.Ordinal) && parent.Operator is "^")
        {
            return $"({text})";
        }

        return text;
    }

    private string WrapIfCompound(Node child, int sig)
    {
        var text = Substitute(child, sig);
        return child is BinaryNode ? $"({text})" : text;
    }
}
