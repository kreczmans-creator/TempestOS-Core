namespace Tempest.Calc.Engine.Numerics;

/// <summary>Dense linear algebra on plain doubles: LU with partial pivoting, determinant, inverse, solve. Sizes are checked by the caller against <see cref="CalcLimits"/>.</summary>
public static class LinearAlgebra
{
    /// <summary>Solves <c>A x = b</c> by Gaussian elimination with partial pivoting. Throws <see cref="CalcEvaluationException"/> on a singular matrix.</summary>
    public static double[] Solve(double[,] a, double[] b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);

        var n = a.GetLength(0);

        if (a.GetLength(1) != n)
            throw new CalcEvaluationException($"solve needs a square matrix, not {n}×{a.GetLength(1)}.");

        if (b.Length != n)
            throw new CalcEvaluationException($"solve needs a right-hand side of length {n}, not {b.Length}.");

        var m = (double[,])a.Clone();
        var x = (double[])b.Clone();

        for (var k = 0; k < n; k++)
        {
            var pivot = k;
            var best = Math.Abs(m[k, k]);

            for (var i = k + 1; i < n; i++)
            {
                var v = Math.Abs(m[i, k]);

                if (v > best)
                {
                    best = v;
                    pivot = i;
                }
            }

            if (best == 0 || !double.IsFinite(best))
                throw new CalcEvaluationException("The matrix is singular; the system has no unique solution.");

            if (pivot != k)
            {
                for (var j = 0; j < n; j++)
                    (m[k, j], m[pivot, j]) = (m[pivot, j], m[k, j]);

                (x[k], x[pivot]) = (x[pivot], x[k]);
            }

            for (var i = k + 1; i < n; i++)
            {
                var f = m[i, k] / m[k, k];

                if (f == 0)
                    continue;

                for (var j = k; j < n; j++)
                    m[i, j] -= f * m[k, j];

                x[i] -= f * x[k];
            }
        }

        for (var i = n - 1; i >= 0; i--)
        {
            var s = x[i];

            for (var j = i + 1; j < n; j++)
                s -= m[i, j] * x[j];

            x[i] = s / m[i, i];
        }

        return x;
    }

    public static double Determinant(double[,] a)
    {
        ArgumentNullException.ThrowIfNull(a);

        var n = a.GetLength(0);

        if (a.GetLength(1) != n)
            throw new CalcEvaluationException($"det needs a square matrix, not {n}×{a.GetLength(1)}.");

        var m = (double[,])a.Clone();
        var det = 1.0;

        for (var k = 0; k < n; k++)
        {
            var pivot = k;

            for (var i = k + 1; i < n; i++)
            {
                if (Math.Abs(m[i, k]) > Math.Abs(m[pivot, k]))
                    pivot = i;
            }

            if (m[pivot, k] == 0)
                return 0;

            if (pivot != k)
            {
                for (var j = 0; j < n; j++)
                    (m[k, j], m[pivot, j]) = (m[pivot, j], m[k, j]);

                det = -det;
            }

            det *= m[k, k];

            for (var i = k + 1; i < n; i++)
            {
                var f = m[i, k] / m[k, k];

                for (var j = k; j < n; j++)
                    m[i, j] -= f * m[k, j];
            }
        }

        return det;
    }

    public static double[,] Inverse(double[,] a)
    {
        ArgumentNullException.ThrowIfNull(a);

        var n = a.GetLength(0);

        if (a.GetLength(1) != n)
            throw new CalcEvaluationException($"inv needs a square matrix, not {n}×{a.GetLength(1)}.");

        var result = new double[n, n];

        for (var c = 0; c < n; c++)
        {
            var e = new double[n];
            e[c] = 1;
            var col = Solve(a, e);

            for (var r = 0; r < n; r++)
                result[r, c] = col[r];
        }

        return result;
    }

    public static double[,] Multiply(double[,] a, double[,] b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);

        var n = a.GetLength(0);
        var k = a.GetLength(1);
        var m = b.GetLength(1);

        if (b.GetLength(0) != k)
            throw new CalcEvaluationException($"Cannot multiply a {n}×{k} matrix by a {b.GetLength(0)}×{m} matrix.");

        var r = new double[n, m];

        for (var i = 0; i < n; i++)
        {
            for (var j = 0; j < m; j++)
            {
                var s = 0.0;

                for (var t = 0; t < k; t++)
                    s += a[i, t] * b[t, j];

                r[i, j] = s;
            }
        }

        return r;
    }

    public static double[] Multiply(double[,] a, double[] v)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(v);

        var n = a.GetLength(0);
        var k = a.GetLength(1);

        if (v.Length != k)
            throw new CalcEvaluationException($"Cannot multiply a {n}×{k} matrix by a vector of length {v.Length}.");

        var r = new double[n];

        for (var i = 0; i < n; i++)
        {
            var s = 0.0;

            for (var t = 0; t < k; t++)
                s += a[i, t] * v[t];

            r[i] = s;
        }

        return r;
    }

    public static double[,] Transpose(double[,] a)
    {
        ArgumentNullException.ThrowIfNull(a);

        var r = new double[a.GetLength(1), a.GetLength(0)];

        for (var i = 0; i < a.GetLength(0); i++)
        {
            for (var j = 0; j < a.GetLength(1); j++)
                r[j, i] = a[i, j];
        }

        return r;
    }
}
