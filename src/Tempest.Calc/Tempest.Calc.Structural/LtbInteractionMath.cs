namespace Tempest.Calc.Structural;

/// <summary>
/// The arithmetic of S7 (EN 1993-1-1 6.3.1, 6.3.2 and 6.3.3) as pure functions on doubles in N, mm and MPa, so the
/// worksheet, the tests and a hand check all use one set of formulas. Every function is a published closed-form
/// relation, written here in our own notation; nothing is read from a table. Factors that depend on the moment
/// diagram beyond the linear (ψ) case are entered by the user, not derived.
/// </summary>
public static class LtbInteractionMath
{
    /// <summary>C₁ for end-moment loading with k = 1 from the ψ ratio: 1.88 − 1.40ψ + 0.52ψ², not above 2.70 (ENV 1993-1-1 Annex F curve fit).</summary>
    public static double C1FromPsi(double psi) => Math.Min(2.70, 1.88 - 1.40 * psi + 0.52 * psi * psi);

    /// <summary>k_c for a linear moment diagram, 1/(1.33 − 0.33ψ) (EN 1993-1-1 Table 6.6).</summary>
    public static double KcFromPsi(double psi) => 1.0 / (1.33 - 0.33 * psi);

    /// <summary>C_m for a linear moment diagram, 0.6 + 0.4ψ, not below 0.4 (EN 1993-1-1 Table B.3).</summary>
    public static double CmFromPsi(double psi) => Math.Max(0.4, 0.6 + 0.4 * psi);

    /// <summary>
    /// The elastic critical moment of a doubly-symmetric member on fork supports, the general (NCCI SN003) expression:
    /// M_cr = C₁·π²EI_z/(kL)²·[√((k/k_w)²·I_w/I_z + (kL)²·GI_t/(π²EI_z) + (C₂z_g)²) − C₂z_g]. E, G in MPa, I in mm⁴, I_w in mm⁶, L and z_g in mm; N·mm.
    /// </summary>
    public static double Mcr(double e, double g, double iz, double it, double iw, double length, double k, double kw, double c1, double c2, double zg)
    {
        var lk = k * length;
        var a = k / kw * (k / kw) * iw / iz;
        var b = lk * lk * g * it / (Math.PI * Math.PI * e * iz);
        var c = c2 * zg;
        var sum = a + b;
        var root = Math.Sqrt(sum + c * c);

        // For a positive C₂z_g the difference √(·) − C₂z_g cancels; the equal form (a + b)/(√(·) + C₂z_g) does not.
        var bracket = c > 0 ? sum / (root + c) : root - c;
        return c1 * Math.PI * Math.PI * e * iz / (lk * lk) * bracket;
    }

    /// <summary>χ_LT by 6.3.2.2 (the general case): 1/(Φ + √(Φ² − λ̄²)) ≤ 1 with Φ = 0.5[1 + α(λ̄ − 0.2) + λ̄²]; 1 up to λ̄ = 0.2.</summary>
    public static double ChiLtGeneral(double lambda, double alpha)
    {
        if (lambda <= 0.2)
            return 1.0;

        var phi = 0.5 * (1 + alpha * (lambda - 0.2) + lambda * lambda);
        return Math.Min(1.0, 1 / (phi + Math.Sqrt(Math.Max(0, phi * phi - lambda * lambda))));
    }

    /// <summary>
    /// χ_LT by 6.3.2.3 (rolled and welded equivalent sections): 1/(Φ + √(Φ² − βλ̄²)) with Φ = 0.5[1 + α(λ̄ − λ̄₀) + βλ̄²], not above 1 or 1/λ̄²;
    /// 1 up to λ̄ = λ̄₀.
    /// </summary>
    public static double ChiLtRolled(double lambda, double alpha, double lambda0, double beta)
    {
        if (lambda <= lambda0)
            return 1.0;

        var phi = 0.5 * (1 + alpha * (lambda - lambda0) + beta * lambda * lambda);
        var chi = 1 / (phi + Math.Sqrt(Math.Max(0, phi * phi - beta * lambda * lambda)));
        return Math.Min(Math.Min(1.0, chi), 1 / (lambda * lambda));
    }

    /// <summary>The modification factor f = 1 − 0.5(1 − k_c)[1 − 2(λ̄_LT − 0.8)²], not above 1 (6.3.2.3(2)).</summary>
    public static double FModification(double kc, double lambda) => Math.Min(1.0, 1 - 0.5 * (1 - kc) * (1 - 2.0 * (lambda - 0.8) * (lambda - 0.8)));

    /// <summary>χ_LT,mod = χ_LT/f (6.3.2.3(2)), not above 1 and, as for χ_LT itself (eq. 6.58), not above 1/λ̄_LT².</summary>
    public static double ChiLtModified(double chiLt, double f, double lambda) => Math.Min(Math.Min(1.0, chiLt / f), lambda > 0 ? 1 / (lambda * lambda) : 1.0);

    /// <summary>χ for flexural buckling (6.3.1.2): 1/(Φ + √(Φ² − λ̄²)) ≤ 1.</summary>
    public static double ChiFlexural(double lambda, double alpha)
    {
        var phi = 0.5 * (1 + alpha * (lambda - 0.2) + lambda * lambda);
        return Math.Min(1.0, 1 / (phi + Math.Sqrt(Math.Max(0, phi * phi - lambda * lambda))));
    }

    /// <summary>The interaction factors k_ij with the equivalent-uniform-moment factors that produced them.</summary>
    public readonly record struct Factors(double Kyy, double Kyz, double Kzy, double Kzz, double Cmy, double Cmz, double CmLT);

    /// <summary>
    /// Annex B (method 2) interaction factors, Table B.1 (members not susceptible to torsional deformation) or Table B.2 (susceptible).
    /// <paramref name="ny"/> and <paramref name="nz"/> are N_Ed/(χ·N_Rk/γ_M1) about y and z; <paramref name="plastic"/> is class 1 or 2.
    /// </summary>
    public static Factors AnnexB(double ny, double nz, double lambdaY, double lambdaZ, double cmy, double cmz, double cmLt, bool plastic, bool torsionSusceptible)
    {
        double kyy, kzz, kyz, kzy;

        if (plastic)
        {
            kyy = Math.Min(cmy * (1 + (lambdaY - 0.2) * ny), cmy * (1 + 0.8 * ny));
            kzz = Math.Min(cmz * (1 + (2 * lambdaZ - 0.6) * nz), cmz * (1 + 1.4 * nz));
            kyz = 0.6 * kzz;

            if (!torsionSusceptible)
            {
                kzy = 0.6 * kyy;
            }
            else
            {
                var d = cmLt - 0.25;
                var atSlenderness = 1 - 0.1 * lambdaZ / d * nz;
                kzy = lambdaZ < 0.4 ? Math.Min(0.6 + lambdaZ, atSlenderness) : Math.Max(atSlenderness, 1 - 0.1 / d * nz);
            }
        }
        else
        {
            kyy = Math.Min(cmy * (1 + 0.6 * lambdaY * ny), cmy * (1 + 0.6 * ny));
            kzz = Math.Min(cmz * (1 + 0.6 * lambdaZ * nz), cmz * (1 + 0.6 * nz));
            kyz = kzz;

            if (!torsionSusceptible)
            {
                kzy = 0.8 * kyy;
            }
            else
            {
                var d = cmLt - 0.25;
                kzy = Math.Max(1 - 0.05 * lambdaZ / d * nz, 1 - 0.05 / d * nz);
            }
        }

        return new Factors(kyy, kyz, kzy, kzz, cmy, cmz, cmLt);
    }

    /// <summary>What Annex A (method 1) needs, in N, mm and MPa.</summary>
    /// <param name="N">Design compression, N (≥ 0).</param>
    /// <param name="NcrY">Elastic flexural buckling force about y, N.</param>
    /// <param name="NcrZ">Elastic flexural buckling force about z, N.</param>
    /// <param name="NcrT">Elastic torsional buckling force, N.</param>
    /// <param name="ChiY">Flexural buckling reduction about y.</param>
    /// <param name="ChiZ">Flexural buckling reduction about z.</param>
    /// <param name="ChiLt">Lateral-torsional reduction (modified, as used in the check).</param>
    /// <param name="LambdaY">Slenderness about y.</param>
    /// <param name="LambdaZ">Slenderness about z.</param>
    /// <param name="Lambda0">Lateral-torsional slenderness for a uniform moment (ψ = 1).</param>
    /// <param name="C1">The C₁ of the moment diagram, for the Table A.2 threshold.</param>
    /// <param name="PsiY">ψ of the M_y diagram.</param>
    /// <param name="PsiZ">ψ of the M_z diagram.</param>
    /// <param name="My">Design M_y, N·mm.</param>
    /// <param name="Mz">Design M_z, N·mm.</param>
    /// <param name="MplY">M_pl,y,Rd = W_pl,y·f_y/γ_M1, N·mm (W_el for class 3).</param>
    /// <param name="MplZ">M_pl,z,Rd, N·mm (W_el for class 3).</param>
    /// <param name="NRkOverGamma">N_Rk/γ_M1, N.</param>
    /// <param name="Area">A, mm².</param>
    /// <param name="WelY">W_el,y, mm³.</param>
    /// <param name="WplOverWelY">W_pl,y/W_el,y (1 for class 3).</param>
    /// <param name="WplOverWelZ">W_pl,z/W_el,z (1 for class 3).</param>
    /// <param name="ALt">a_LT = 1 − I_t/I_y, not below 0.</param>
    /// <param name="Plastic">Class 1 or 2 (Table A.1, plastic column, with the 0.6√(w_i/w_j) factors of k_yz and k_zy); false for class 3 (elastic column, without them).</param>
    public readonly record struct AnnexAInput(
        double N, double NcrY, double NcrZ, double NcrT, double ChiY, double ChiZ, double ChiLt, double LambdaY, double LambdaZ, double Lambda0, double C1,
        double PsiY, double PsiZ, double My, double Mz, double MplY, double MplZ, double NRkOverGamma, double Area, double WelY, double WplOverWelY, double WplOverWelZ, double ALt, bool Plastic = true);

    /// <summary>Annex A (method 1) interaction factors, Tables A.1 and A.2.</summary>
    public static Factors AnnexA(AnnexAInput a)
    {
        var nY = 1 - a.N / a.NcrY;
        var nZ = 1 - a.N / a.NcrZ;
        var nT = 1 - a.N / a.NcrT;
        var muY = nY / (1 - a.ChiY * a.N / a.NcrY);
        var muZ = nZ / (1 - a.ChiZ * a.N / a.NcrZ);
        var wy = Math.Min(1.5, a.WplOverWelY);
        var wz = Math.Min(1.5, a.WplOverWelZ);
        var nPl = a.N / a.NRkOverGamma;
        var lambdaMax = Math.Max(a.LambdaY, a.LambdaZ);

        var cmy0 = 0.79 + 0.21 * a.PsiY + 0.36 * (a.PsiY - 0.33) * a.N / a.NcrY;
        var cmz0 = 0.79 + 0.21 * a.PsiZ + 0.36 * (a.PsiZ - 0.33) * a.N / a.NcrZ;
        var threshold = 0.2 * Math.Sqrt(a.C1) * Math.Pow(nZ * nT, 0.25);
        var lateral = a.Lambda0 > threshold;

        double cmy, cmz = cmz0, cmLt, bLt = 0, cLt = 0, dLt = 0, eLt = 0;

        if (!lateral)
        {
            cmy = cmy0;
            cmLt = 1.0;
        }
        else
        {
            var s = a.N <= 0 ? (a.ALt > 0 ? double.PositiveInfinity : 0) : Math.Sqrt(Math.Abs(a.My) / a.N * a.Area / a.WelY) * a.ALt;
            var share = double.IsPositiveInfinity(s) ? 1.0 : s / (1 + s);
            cmy = cmy0 + (1 - cmy0) * share;
            cmLt = Math.Max(1.0, cmy * cmy * a.ALt / Math.Sqrt(nZ * nT));

            var myTerm = a.My / (a.ChiLt * a.MplY);
            bLt = 0.5 * a.ALt * a.Lambda0 * a.Lambda0 * myTerm * (a.Mz / a.MplZ);
            cLt = 10 * a.ALt * a.Lambda0 * a.Lambda0 / (5 + Math.Pow(a.LambdaZ, 4)) * (a.My / (cmy * a.ChiLt * a.MplY));
            dLt = 2 * a.ALt * a.Lambda0 / (0.1 + Math.Pow(a.LambdaZ, 4)) * (a.My / (cmy * a.ChiLt * a.MplY)) * (a.Mz / (cmz * a.MplZ));
            eLt = 1.7 * a.ALt * a.Lambda0 / (0.1 + Math.Pow(a.LambdaZ, 4)) * (a.My / (cmy * a.ChiLt * a.MplY));
        }

        var elOverPlY = 1 / a.WplOverWelY;
        var elOverPlZ = 1 / a.WplOverWelZ;
        var cyy = Math.Max(elOverPlY, 1 + (wy - 1) * ((2 - 1.6 / wy * cmy * cmy * lambdaMax - 1.6 / wy * cmy * cmy * lambdaMax * lambdaMax) * nPl - bLt));
        var czz = Math.Max(elOverPlZ, 1 + (wz - 1) * ((2 - 1.6 / wz * cmz * cmz * lambdaMax - 1.6 / wz * cmz * cmz * lambdaMax * lambdaMax) * nPl - eLt));
        var cyz = Math.Max(0.6 * Math.Sqrt(wz / wy) * elOverPlZ, 1 + (wz - 1) * ((2 - 14 * cmz * cmz * lambdaMax * lambdaMax / Math.Pow(wz, 5)) * nPl - cLt));
        var czy = Math.Max(0.6 * Math.Sqrt(wy / wz) * elOverPlY, 1 + (wy - 1) * ((2 - 14 * cmy * cmy * lambdaMax * lambdaMax / Math.Pow(wy, 5)) * nPl - dLt));

        var kyy = cmy * cmLt * muY / nY / cyy;

        // Table A.1: the plastic column carries 0.6√(w_z/w_y) and 0.6√(w_y/w_z); the elastic column (class 3, and 4) does not.
        var kyz = cmz * muY / nZ / cyz * (a.Plastic ? 0.6 * Math.Sqrt(wz / wy) : 1.0);
        var kzy = cmy * cmLt * muZ / nY / czy * (a.Plastic ? 0.6 * Math.Sqrt(wy / wz) : 1.0);
        var kzz = cmz * muZ / nZ / czz;
        return new Factors(kyy, kyz, kzy, kzz, cmy, cmz, cmLt);
    }
}
