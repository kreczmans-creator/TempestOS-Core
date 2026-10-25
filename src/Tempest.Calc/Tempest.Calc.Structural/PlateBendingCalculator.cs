using Tempest.Calc.Engine;
using Tempest.Calc.Engine.Values;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Structural;

/// <summary>
/// S9 — Bending of a thin plate (Kirchhoff small-deflection theory): circular plates, clamped or simply supported, under a
/// uniform pressure, a central patch or a ring load (closed forms); simply supported rectangles under a uniform pressure or a
/// central patch (Navier double series, summed until it stops changing); clamped rectangles by a Ritz solution (labelled
/// approximate). Gives the maximum deflection, the greatest von Mises stress (from the bending and twisting moments, at the
/// surface), the stress and deflection utilisations and the fundamental frequency. The material properties come from the
/// W3 library; no table of plate coefficients is shipped: the coefficients are computed from the series.
/// </summary>
public sealed class PlateBendingCalculator : ICalcCalculator
{
    public const string Id = "struct.plate-bending";

    public const string Circular = "circular";
    public const string Rectangular = "rectangular";
    public const string Simple = "simply supported";
    public const string Clamped = "clamped";
    public const string Uniform = "uniform";
    public const string Patch = "central patch";
    public const string Ring = "ring (circular only)";
    public const string BasisPressure = "pressure q";
    public const string BasisForce = "total force P";

    /// <summary>The record kind a material from the W3 library fills. Equal to <c>MaterialLibrary.Kind</c> (a Library test pins the two together).</summary>
    public const string MaterialKind = "material.properties";

    /// <summary>The most odd terms per direction the Navier series is summed to (1024 → 1 048 576 terms).</summary>
    public const int NavierTermCap = 1024;

    /// <summary>The relative change over a doubling of the terms below which the Navier series counts as converged.</summary>
    public const double NavierTolerance = 1e-6;

    /// <summary>The relative change between Ritz orders K and K − 2 below which the Ritz solution counts as converged.</summary>
    public const double RitzTolerance = 1e-5;

    public const int RitzStart = 4;
    public const int RitzCap = 30;

    /// <summary>The thin-plate theory is not used for a span/thickness ratio below this (shear deformation).</summary>
    public const double ThinPlateRatio = 10;

    public CalcTemplate Template { get; } = new(
        Id, "1.1.0", CalcDiscipline.Structural, "Plate bending",
        "Bending of a thin, flat, isotropic plate under a lateral load by Kirchhoff small-deflection theory: circular plates (uniform pressure, central patch, ring load; clamped or simply supported) in closed form; rectangular plates simply supported (uniform pressure or central patch, Navier series) or clamped (Ritz, approximate). Maximum deflection, greatest von Mises stress at the surface, stress and deflection utilisations and the fundamental frequency.",
        "Flexural rigidity: D = E·t³/(12(1 − ν²)); surface stress σ = 6M/t²; von Mises σ_vm = √(σ_x² − σ_x·σ_y + σ_y² + 3τ_xy²)\n" +
        "Circular, uniform q, clamped: w = q·a⁴/(64D); σ_edge = 3q·a²/(4t²); simply supported: w = (5 + ν)·q·a⁴/(64(1 + ν)D); σ_centre = 3(3 + ν)·q·a²/(8t²)\n" +
        "Circular, central patch (radius c, pressure q) or ring (radius b, force F): w = A + B·r² (+ q·r⁴/64D) inside, A′ + B′·r² + C′·ln(r/a) + k·r²·ln(r/a) outside, k = q·c²/8D or F/8πD; constants from continuity of w, w′, M_r at the load radius and the edge conditions; M_r = −D(w″ + ν·w′/r), M_θ = −D(w′/r + ν·w″)\n" +
        "Rectangular, simply supported: w = Σ W_mn·sin(mπx/a)·sin(nπy/b) over odd m, n; W_mn = q_mn/(π⁴D(m²/a² + n²/b²)²); q_mn = 16q/(π²mn) (uniform) or 16q/(π²mn)·sin(mπ/2)·sin(nπ/2)·sin(mπu/2a)·sin(nπv/2b) (patch u × v); M_x = Dπ²ΣW_mn(m²/a² + ν·n²/b²)·sin·sin, M_y likewise, M_xy = −D(1 − ν)π²ΣW_mn(mn/ab)·cos·cos; terms added until w, the centre moments and the corner M_xy each change by < 10⁻⁶ over a doubling (cap 1024 × 1024; the corner sum of a uniform load, which converges like 1/N², is Richardson-extrapolated before it is compared)\n" +
        "Rectangular, clamped: Ritz, w = Σ c_ij·X_i(x/a)·Y_j(y/b), X_k(ξ) = ξ²(1 − ξ)²·P₂ₖ(2ξ − 1); c from the minimum of the total potential energy; order K raised from 4 to 30 until w and the stress change by < 10⁻⁵\n" +
        "Fundamental frequency: simply supported rectangle ω₁₁ = π²(1/a² + 1/b²)·√(D/(ρt)), f = ω/2π; circular ω = (λ²/a²)·√(D/(ρt)), λ² from the Bessel characteristic equation; clamped rectangle by Rayleigh–Ritz with the same basis\n" +
        "Checks: σ_vm ≤ f_y/γ_M0; w_max ≤ span/ratio (circular span = diameter, rectangle span = shorter side); WARN w/t > 0.5",
        [
            new CalcInputSpec("shape", "Plate shape", CalcInputKind.Choice, Choices: [Circular, Rectangular], DefaultChoice: Rectangular, Group: "Plate"),
            new CalcInputSpec("support", "Edge support, all round", CalcInputKind.Choice, Choices: [Simple, Clamped], DefaultChoice: Simple, Group: "Plate"),
            new CalcInputSpec("a", "Radius a (circular) or side a (rectangular)", CalcInputKind.Quantity, "mm", Default: 600, Min: 10, Max: 10000, HardMin: 5, HardMax: 20000, Group: "Plate"),
            new CalcInputSpec("b", "Side b (rectangular; not used for a circular plate)", CalcInputKind.Quantity, "mm", Default: 400, Min: 10, Max: 10000, HardMin: 5, HardMax: 20000, Group: "Plate"),
            new CalcInputSpec("t", "Plate thickness", CalcInputKind.Quantity, "mm", Default: 6, Min: 0.5, Max: 100, HardMin: 0.1, HardMax: 300, Group: "Plate"),

            new CalcInputSpec("load_type", "Load", CalcInputKind.Choice, Choices: [Uniform, Patch, Ring], DefaultChoice: Uniform, Group: "Load"),
            new CalcInputSpec("load_basis", "Uniform and patch loads given as", CalcInputKind.Choice, Choices: [BasisPressure, BasisForce], DefaultChoice: BasisPressure, Group: "Load"),
            new CalcInputSpec("q", "Pressure q on the loaded area (uniform and patch loads, pressure basis)", CalcInputKind.Quantity, "kPa", Default: 10, Min: 0.001, Max: 5000, HardMin: 1e-6, HardMax: 1e5, Group: "Load"),
            new CalcInputSpec("P", "Total force P (force basis: uniform, patch and ring loads; a ring load always uses P)", CalcInputKind.Quantity, "kN", Default: 5, Min: 0.001, Max: 10000, HardMin: 1e-6, HardMax: 1e6, Group: "Load"),
            new CalcInputSpec("r0", "Patch radius (circular plate, central patch)", CalcInputKind.Quantity, "mm", Default: 50, Min: 1, Max: 5000, HardMin: 0.1, HardMax: 20000, Group: "Load"),
            new CalcInputSpec("u", "Patch size in x (rectangular plate, central patch)", CalcInputKind.Quantity, "mm", Default: 100, Min: 1, Max: 10000, HardMin: 0.1, HardMax: 20000, Group: "Load"),
            new CalcInputSpec("v", "Patch size in y (rectangular plate, central patch)", CalcInputKind.Quantity, "mm", Default: 100, Min: 1, Max: 10000, HardMin: 0.1, HardMax: 20000, Group: "Load"),
            new CalcInputSpec("r_ring", "Ring radius (circular plate, ring load)", CalcInputKind.Quantity, "mm", Default: 150, Min: 1, Max: 5000, HardMin: 0.1, HardMax: 20000, Group: "Load"),

            new CalcInputSpec("E", "Modulus of elasticity", CalcInputKind.Quantity, "GPa", Default: 210, Min: 0.1, Max: 500, HardMin: 0.01, HardMax: 1500, Group: "Material", MaterialProperty: "E"),
            new CalcInputSpec("nu", "Poisson's ratio", CalcInputKind.Number, Default: 0.3, Min: 0, Max: 0.45, HardMin: 0, HardMax: 0.49, Group: "Material", MaterialProperty: "nu"),
            new CalcInputSpec("rho", "Density (for the frequency)", CalcInputKind.Quantity, "kg/m^3", Default: 7850, Min: 100, Max: 20000, HardMin: 10, HardMax: 30000, Group: "Material", MaterialProperty: "rho"),
            new CalcInputSpec("f_y", "Yield strength", CalcInputKind.Quantity, "MPa", Default: 275, Min: 5, Max: 2000, HardMin: 1, HardMax: 5000, Group: "Material", MaterialProperty: "f_y"),
            new CalcInputSpec("gamma_M0", "Partial factor γ_M0 on the resistance of the cross-section", CalcInputKind.Number, Default: 1.0, Min: 1.0, Max: 1.5, HardMin: 1.0, HardMax: 2.0, Group: "Checks"),
            new CalcInputSpec("defl_ratio", "Deflection limit: span/ratio (span = diameter of a circular plate, shorter side of a rectangle)", CalcInputKind.Number, Default: 250, Min: 50, Max: 2000, HardMin: 10, HardMax: 10000, Group: "Checks"),
        ],
        [
            new CalcAssumption("Thin plate, small-deflection (Kirchhoff) theory: flat, isotropic, linear elastic, constant thickness; the middle surface is unstrained; shear deformation is neglected (span/t of 10 or more).", "Scope of S9; a warning below span/t = 10."),
            new CalcAssumption("The deflection stays small against the thickness: w/t above 0.5 means membrane action stiffens the plate and the small-deflection result is not valid (it overstates the deflection and the bending stress and ignores the membrane stress).", "The sheet warns at w/t > 0.5."),
            new CalcAssumption("The edge support is the same all round and ideal: a simply supported edge is held at zero deflection and free to rotate; a clamped edge allows no deflection or rotation. A simply supported rectangle has its corners held down (the corner reaction 2·M_xy is reported); if the corners can lift, the deflection and the central moments are larger than computed.", "Navier solution; no elastic edges, no rotational springs."),
            new CalcAssumption("The von Mises stress is that of the plane-stress field at the plate surface from the bending and twisting moments (σ = 6M/t²); the through-thickness shear and the local stress under a concentrated load are not included. The stress governing a simply supported rectangle is at the centre or at a corner (twisting); a clamped rectangle is scanned on a 21 × 21 grid over a quarter of the plate.", "Surface stresses; no local effects."),
            new CalcAssumption("The rectangular patch and the circular patch are centred; a ring load is concentric. A load smaller than the thickness is a concentrated load whose local stress thin-plate theory does not give.", "Warning `patch-thin`; patches below 1 % (circular), 2 % (simply supported rectangle) or 25 % (clamped rectangle) of the plate size are refused."),
            new CalcAssumption("The clamped rectangle is a Ritz approximation: the deflection converges quickly, the moments and stress more slowly (the sheet reports the order used and the last change; long plates and small patches converge slowest, and the last change understates the error). Compare a critical result with finite elements.", "Approximate; the sheet states the order used and the last change."),
            new CalcAssumption("Material properties are inputs taken from the W3 library (E, ν, ρ, f_y), each citing the material record, its revision and the page or table; the generic textbook seeds are indicative.", "Record kind material.properties."),
        ],
        [
            new CalcClauseRef("Timoshenko & Woinowsky-Krieger, Theory of Plates and Shells", "Ch. 3", "Symmetrical bending of circular plates"),
            new CalcClauseRef("Timoshenko & Woinowsky-Krieger, Theory of Plates and Shells", "Ch. 5", "Simply supported rectangular plates (Navier solution)"),
            new CalcClauseRef("Timoshenko & Woinowsky-Krieger, Theory of Plates and Shells", "Ch. 6", "Rectangular plates with various edge conditions"),
            new CalcClauseRef("Leissa, Vibration of Plates (NASA SP-160)", "Ch. 2, 4", "Natural frequencies of circular and rectangular plates"),
            new CalcClauseRef("BS EN 1993-1-1:2005+A1:2014", "6.1(1)", "Partial factor γ_M0"),
            new CalcClauseRef("BS EN 1993-1-1:2005+A1:2014", "6.2.1(5)", "Yield criterion for the cross-section"),
        ],
        [
            new CalcApplicabilityRule("ring-rect", "A ring load applies to a circular plate.", CalcNoticeSeverity.Block),
            new CalcApplicabilityRule("aspect", "The long side of a rectangle can be at most 10 times the short side (beyond that the plate is a one-way strip).", CalcNoticeSeverity.Block),
            new CalcApplicabilityRule("patch-size", "The patch cannot be larger than the plate.", CalcNoticeSeverity.Block),
            new CalcApplicabilityRule("patch-min", "The patch must be at least 1 % (circular), 2 % (simply supported rectangle) or 25 % (clamped rectangle) of the plate size for the solution to resolve it.", CalcNoticeSeverity.Block),
            new CalcApplicabilityRule("ring-radius", "The ring radius must lie between 1 % and 99 % of the plate radius.", CalcNoticeSeverity.Block),
            new CalcApplicabilityRule("engine", "The solution could not be evaluated for these inputs.", CalcNoticeSeverity.Block),
            new CalcApplicabilityRule("large-deflection", "w/t above 0.5: the small-deflection theory is not valid.", CalcNoticeSeverity.Warning),
            new CalcApplicabilityRule("thin-plate", "span/t below 10: shear deformation matters and the thin-plate theory over-states the stiffness.", CalcNoticeSeverity.Warning),
            new CalcApplicabilityRule("patch-thin", "A patch smaller than the thickness is a concentrated load: the local stress is not given by thin-plate theory.", CalcNoticeSeverity.Warning),
            new CalcApplicabilityRule("navier-converge", "The Navier series reached its cap without changing by less than the tolerance.", CalcNoticeSeverity.Warning),
            new CalcApplicabilityRule("ritz-converge", "The Ritz solution reached its highest order without changing by less than the tolerance.", CalcNoticeSeverity.Warning),
            new CalcApplicabilityRule("ritz-approx", "The clamped rectangle is a Ritz approximation.", CalcNoticeSeverity.Warning),
        ],
        [
            new CalcTestCase("ka-circular-clamped-uniform", "Circular plate, clamped, uniform pressure: w = q·a⁴/(64D), edge stress 3q·a²/(4t²)",
                new CalcReferenceSource("Analytic closed forms (Kirchhoff theory), recomputed in python", "Timoshenko & Woinowsky-Krieger, Ch. 3",
                    "a = 250 mm, t = 5 mm, q = 20 kPa, E = 200 GPa, ν = 0.3, ρ = 7850: D = 2289.3773 N·m; w = q·a⁴/(64D) = 0.53320 mm; σ_r,edge = 3q·a²/(4t²) = 37.5 MPa; σ_vm = σ_r·√(1 − ν + ν²) = 33.3 MPa; f₁ = (λ²/2πa²)√(D/ρt) with λ² = 10.21583 from J₀(λ)I₁(λ) + I₀(λ)J₁(λ) = 0"),
                [.. Circle(true), CalcInputValue.Choice("load_type", Uniform)],
                [new CalcExpectedValue("D", 2289.37728937729, "N*m", 1e-9), new CalcExpectedValue("w_max", CircularClampedWMax, "mm", 1e-9), new CalcExpectedValue("sigma_principal", 37.5, "MPa", 1e-9), new CalcExpectedValue("sigma_max", 37.5 * Math.Sqrt(1 - 0.3 + 0.09), "MPa", 1e-9), new CalcExpectedValue("f1", CircularClampedF1, "Hz", 1e-8)]),
            new CalcTestCase("ka-circular-simple-uniform", "Circular plate, simply supported, uniform pressure: w = (5 + ν)q·a⁴/(64(1 + ν)D), centre stress 3(3 + ν)q·a²/(8t²)",
                new CalcReferenceSource("Analytic closed forms (Kirchhoff theory), recomputed in python", "Timoshenko & Woinowsky-Krieger, Ch. 3",
                    "Same plate simply supported: w = 5.3/(1.3 × 64) × q·a⁴/D = 2.17383 mm; σ_centre = 3(3 + ν)q·a²/(8t²) = 61.875 MPa (σ_r = σ_θ, so σ_vm is the same); λ² = 4.93515 at ν = 0.3 from J₁/J₀ + I₁/I₀ = 2λ/(1 − ν)"),
                [.. Circle(false), CalcInputValue.Choice("load_type", Uniform)],
                [new CalcExpectedValue("w_max", CircularSimpleWMax, "mm", 1e-9), new CalcExpectedValue("sigma_principal", 61.875, "MPa", 1e-9), new CalcExpectedValue("sigma_max", 61.875, "MPa", 1e-9), new CalcExpectedValue("f1", CircularSimpleF1, "Hz", 1e-8)]),
            new CalcTestCase("ka-circular-simple-patch", "Circular plate, simply supported, central patch of radius 0.4a: closed-form centre deflection and moment",
                new CalcReferenceSource("Analytic (patch solution derived from the Kirchhoff equations; cross-checked in python by integrating the ring solution over the patch and by finite differences)", "Timoshenko & Woinowsky-Krieger, Ch. 3",
                    "a = 250 mm, c = 100 mm (β = 0.4), q = 20 kPa: w₀ = q·a⁴/(8D)·[(3 + ν)/(1 + ν)·(β²/2 − β⁴/4) + β⁴/2·ln β − β⁴/8]; the greatest principal moment is at the centre, M_r = M_θ"),
                [.. Circle(false), CalcInputValue.Choice("load_type", Patch), CalcInputValue.Quantity("r0", 100, "mm")],
                [new CalcExpectedValue("w_max", CircularSimplePatchWMax, "mm", 1e-9), new CalcExpectedValue("sigma_principal", CircularSimplePatchSigma, "MPa", 1e-7), new CalcExpectedValue("sigma_max", CircularSimplePatchSigma, "MPa", 1e-7)]),
            new CalcTestCase("ka-circular-clamped-ring", "Circular plate, clamped, ring load of 4 kN at radius 0.5a: closed-form centre deflection and the edge moment",
                new CalcReferenceSource("Analytic (ring solution derived from the Kirchhoff equations; cross-checked in python by finite differences)", "Timoshenko & Woinowsky-Krieger, Ch. 3",
                    "a = 250 mm, b = 125 mm (β = 0.5), F = 4 kN, clamped: w₀ = F·a²/(16πD)·(1 − β² + 2β²·ln β); the stress is greatest at the clamped edge or the ring, found by scanning M_r, M_θ"),
                [.. Circle(true), CalcInputValue.Choice("load_type", Ring), CalcInputValue.Quantity("r_ring", 125, "mm"), CalcInputValue.Quantity("P", 4, "kN")],
                [new CalcExpectedValue("w_max", CircularClampedRingWMax, "mm", 1e-9), new CalcExpectedValue("sigma_principal", CircularClampedRingSigmaP, "MPa", 1e-6), new CalcExpectedValue("sigma_max", CircularClampedRingSigmaVm, "MPa", 1e-6)]),
            new CalcTestCase("ka-rect-simple-square-series", "Square plate, simply supported, uniform pressure (ν = 0.3): w_max = 0.0040624·q·a⁴/D from the Navier series",
                new CalcReferenceSource("Analytic: the Navier double series summed to the limit (python, numpy, 1024 odd terms per direction with the 1/N² tail removed)", "Timoshenko & Woinowsky-Krieger, Ch. 5",
                    "a = b = 500 mm, t = 8 mm, q = 10 kPa, E = 210 GPa: w_max = 0.00406235266·q·a⁴/D, M_x(centre) = 0.04788638·q·a², corner |M_xy| = 0.0324823·q·a²; σ_vm is the larger of the centre value and √3 times the corner twisting stress"),
                [.. Square(), CalcInputValue.Choice("support", Simple), CalcInputValue.Choice("load_type", Uniform)],
                [new CalcExpectedValue("w_max", SquareWMax, "mm", 2e-6), new CalcExpectedValue("M_x_c", SquareMx, "N*m/m", 2e-6), new CalcExpectedValue("M_xy_k", SquareMxy, "N*m/m", 1e-6), new CalcExpectedValue("sigma_max", SquareSigma, "MPa", 1e-6)]),
            new CalcTestCase("ka-rect-simple-square-book", "Square plate, simply supported, uniform pressure: the classical coefficient w_max = 0.00406·q·a⁴/D",
                new CalcReferenceSource("Timoshenko & Woinowsky-Krieger, Theory of Plates and Shells", "2nd ed.", "Ch. 5, square plate under uniform load, α = 0.00406 (3 s.f.); to be checked against the owner's copy"),
                [.. Square(), CalcInputValue.Choice("support", Simple), CalcInputValue.Choice("load_type", Uniform)],
                [new CalcExpectedValue("w_max", SquareWMaxBook, "mm", 1e-3)],
                CalcReferenceStatus.Unconfirmed),
            new CalcTestCase("ka-rect-simple-natural", "Rectangular plate 600 × 400 × 6 mm, simply supported: f₁₁ = (π/2)(1/a² + 1/b²)·√(D/(ρt))",
                new CalcReferenceSource("Analytic closed form for the (1,1) mode of a simply supported rectangle (ω = π²(1/a² + 1/b²)√(D/ρt), f = ω/2π), recomputed in python", "Timoshenko & Woinowsky-Krieger, Ch. 5 / Leissa Ch. 4",
                    "Steel E = 210 GPa, ν = 0.3, ρ = 7850 kg/m³: D = 4153.846 N·m"),
                [CalcInputValue.Choice("shape", Rectangular), CalcInputValue.Choice("support", Simple), CalcInputValue.Choice("load_type", Uniform)],
                [new CalcExpectedValue("f1", RectSimpleF1, "Hz", 1e-9)]),
            new CalcTestCase("ka-rect-simple-patch", "Rectangular plate 600 × 400 × 6 mm, simply supported, central patch 150 × 100 mm of 50 kPa: Navier series with the patch coefficients",
                new CalcReferenceSource("Analytic: Navier double series with q_mn = 16q/(π²mn)·sin(mπ/2)sin(nπ/2)sin(mπu/2a)sin(nπv/2b), recomputed in python (numpy, 1024 odd terms per direction)", "Timoshenko & Woinowsky-Krieger, Ch. 5",
                    "a = 600, b = 400, t = 6 mm, E = 210 GPa, ν = 0.3, patch u = 150 mm in x, v = 100 mm in y, q = 50 kPa on the patch"),
                [CalcInputValue.Choice("shape", Rectangular), CalcInputValue.Choice("support", Simple), CalcInputValue.Choice("load_type", Patch), CalcInputValue.Quantity("q", 50, "kPa"), CalcInputValue.Quantity("u", 150, "mm"), CalcInputValue.Quantity("v", 100, "mm")],
                [new CalcExpectedValue("w_max", RectPatchWMax, "mm", 2e-6), new CalcExpectedValue("M_x_c", RectPatchMx, "N*m/m", 2e-6), new CalcExpectedValue("M_y_c", RectPatchMy, "N*m/m", 2e-6)]),
            new CalcTestCase("ka-rect-clamped-square", "Square plate, clamped, uniform pressure (ν = 0.3): Ritz solution, w_max about 0.00127·q·a⁴/D",
                new CalcReferenceSource("Ritz solution (the basis of this sheet) recomputed in an independent python implementation (numpy); approximate method", "Timoshenko & Woinowsky-Krieger, Ch. 6",
                    "a = b = 500 mm, t = 8 mm, q = 10 kPa: w_max = 0.001265319·q·a⁴/D, M_x(centre) = 0.022905·q·a², edge middle M = −0.05133·q·a² (converged values); the book gives 0.00126 and 0.0513 (to be checked against the owner's copy)"),
                [.. Square(), CalcInputValue.Choice("support", Clamped), CalcInputValue.Choice("load_type", Uniform)],
                [new CalcExpectedValue("w_max", ClampedSquareWMax, "mm", 1e-6), new CalcExpectedValue("M_x_c", ClampedSquareMx, "N*m/m", 1e-4), new CalcExpectedValue("M_edge", ClampedSquareMedge, "N*m/m", 1e-3)],
                CalcReferenceStatus.Unconfirmed),
            new CalcTestCase("ka-rect-clamped-square-book", "Square plate, clamped, uniform pressure: the classical coefficient w_max = 0.00126·q·a⁴/D",
                new CalcReferenceSource("Timoshenko & Woinowsky-Krieger, Theory of Plates and Shells", "2nd ed.", "Ch. 6, clamped square plate under uniform load, α = 0.00126 (3 s.f.); to be checked against the owner's copy"),
                [.. Square(), CalcInputValue.Choice("support", Clamped), CalcInputValue.Choice("load_type", Uniform)],
                [new CalcExpectedValue("w_max", ClampedSquareWMaxBook, "mm", 5e-3)],
                CalcReferenceStatus.Unconfirmed),
            new CalcTestCase("ka-rect-clamped-natural", "Square plate, clamped: fundamental frequency ω = 35.99/a²·√(D/(ρt))",
                new CalcReferenceSource("Rayleigh–Ritz result of this sheet (python recomputation); published constant λ² = 35.99 (Leissa, Vibration of Plates, Ch. 4), to be checked against the owner's copy", "NASA SP-160",
                    "a = b = 500 mm, t = 8 mm, steel: λ² = 35.9852 from the Ritz eigenvalue"),
                [.. Square(), CalcInputValue.Choice("support", Clamped), CalcInputValue.Choice("load_type", Uniform)],
                [new CalcExpectedValue("f1", ClampedSquareF1, "Hz", 1e-6)],
                CalcReferenceStatus.Unconfirmed),
        ],
        Category: "Design",
        OutputPorts:
        [
            new CalcOutputPort("w_max", "mm", "Maximum plate deflection (→ S6, E2 enclosure checks)"),
            new CalcOutputPort("sigma_max", "MPa", "Greatest von Mises surface stress"),
            new CalcOutputPort("f1", "Hz", "Fundamental natural frequency"),
        ],
        RecordKinds:
        [
            new CalcRecordKind(MaterialKind, "Material properties", ["E", "nu", "rho", "f_y"], "A material from the library: its properties fill these inputs, each citing the material, its revision and the page or table of the value."),
            new CalcRecordKind("ec3.national-annex", "EN 1993 National Annex factors", ["gamma_M0"], "From the National Annex in use, with its edition."),
            new CalcRecordKind("plate.deflection-limit", "Deflection limit", ["defl_ratio"], "The span/ratio limit for the plate from the specification or the product standard in use, with its edition."),
        ]);

    // ------------------------------------------------------------------ known-answer cases (values recomputed in python; see the spec)

    // Circular plate of the first three cases: a = 250 mm, t = 5 mm, q = 20 kPa, E = 200 GPa, ν = 0.3, ρ = 7850 kg/m^3.
    private const double CircularClampedWMax = 0.533203125;
    private const double CircularSimpleWMax = 2.173828125;
    private const double CircularClampedF1 = 198.67922896885142;
    private const double CircularSimpleF1 = 95.97966768057351;
    private const double CircularSimplePatchWMax = 0.733270526039671;
    private const double CircularSimplePatchSigma = 25.958135417236814;
    private const double CircularClampedRingWMax = 0.8764297443823545;
    private const double CircularClampedRingSigmaP = 57.29577951308233;
    private const double CircularClampedRingSigmaVm = 50.92560276039233;
    private const double SquareWMax = 0.2578641825623815;
    private const double SquareMx = 119.71594911499169;
    private const double SquareMxy = 81.20587840546958;
    private const double SquareSigma = 13.186189518664579;
    private const double SquareWMaxBook = 0.25771484375;
    private const double RectSimpleF1 = 133.17278663541322;
    private const double RectPatchWMax = 0.39813227096225886;
    private const double RectPatchMx = 117.0624988680948;
    private const double RectPatchMy = 152.72187253536302;
    private const double ClampedSquareWMax = 0.08031810613887827;
    private const double ClampedSquareMx = 57.262726987305946;
    private const double ClampedSquareMedge = 128.33419152103113;
    private const double ClampedSquareWMaxBook = 0.07998046875;
    private const double ClampedSquareF1 = 286.85169688278467;

    private static CalcInputValue[] Circle(bool clamped) =>
    [
        CalcInputValue.Choice("shape", Circular), CalcInputValue.Choice("support", clamped ? Clamped : Simple), CalcInputValue.Quantity("a", 250, "mm"), CalcInputValue.Quantity("t", 5, "mm"), CalcInputValue.Quantity("q", 20, "kPa"),
        CalcInputValue.Quantity("E", 200, "GPa"), CalcInputValue.Of("nu", 0.3), CalcInputValue.Quantity("rho", 7850, "kg/m^3"),
    ];

    private static CalcInputValue[] Square() =>
    [
        CalcInputValue.Choice("shape", Rectangular), CalcInputValue.Quantity("a", 500, "mm"), CalcInputValue.Quantity("b", 500, "mm"), CalcInputValue.Quantity("t", 8, "mm"), CalcInputValue.Quantity("q", 10, "kPa"),
        CalcInputValue.Quantity("E", 210, "GPa"), CalcInputValue.Of("nu", 0.3), CalcInputValue.Quantity("rho", 7850, "kg/m^3"),
    ];

    private static string F(double v) => double.IsFinite(v) ? CalcValue.FormatNumber(v, 5) : "—";

    public void Run(CalcRunContext c)
    {
        var circular = c.InputText("shape") == Circular;
        var clamped = c.InputText("support") == Clamped;
        var load = c.InputText("load_type");
        var forceBasis = c.InputText("load_basis") == BasisForce;

        if (!circular && load == Ring)
        {
            c.Block("ring-rect", "load_type: a ring load applies to a circular plate; choose a uniform load or a central patch for a rectangle.");
            return;
        }

        foreach (var s in new[] { "E", "nu", "rho", "f_y", "gamma_M0", "defl_ratio", "t", "a" })
            c.Input(s);

        if (!circular)
            c.Input("b");

        var nu = c.InputSi("nu");
        var rho = c.InputSi("rho");
        var t = c.InputSi("t");
        var a = c.InputSi("a");
        var b = circular ? a : c.InputSi("b");

        if (!circular && Math.Max(a, b) / Math.Min(a, b) > 10)
        {
            c.Block("aspect", "b: the long side of the rectangle is more than 10 times the short side; treat the plate as a one-way strip (beam formulas, S2/S3).");
            return;
        }

        var span = circular ? 2 * a : Math.Min(a, b);

        if (span / t < ThinPlateRatio)
            c.Warn("thin-plate", $"t: span/t = {F(span / t)} is below {ThinPlateRatio:0}; shear deformation is not in thin-plate theory and the plate is stiffer and less stressed here than in reality.");

        // The load: its extent (patch or ring) and its pressure and total force.
        double rc = a;
        double? pu = null, pv = null;
        double area;

        switch (load)
        {
            case Uniform:
                area = circular ? Math.PI * a * a : a * b;
                break;
            case Patch when circular:
                c.Input("r0");
                rc = c.InputSi("r0");

                if (rc > a)
                {
                    c.Block("patch-size", "r0: the patch is larger than the plate; enter a patch radius not above the plate radius.");
                    return;
                }

                if (rc < 0.01 * a)
                {
                    c.Block("patch-min", "r0: the patch is below 1 % of the plate radius; enter a larger patch (a smaller load is a concentrated load, outside thin-plate theory).");
                    return;
                }

                area = Math.PI * rc * rc;
                break;
            case Patch:
                c.Input("u");
                c.Input("v");
                pu = c.InputSi("u");
                pv = c.InputSi("v");

                if (pu > a || pv > b)
                {
                    c.Block("patch-size", "u: the patch is larger than the plate; enter patch sizes not above the sides.");
                    return;
                }

                var minFraction = clamped ? 0.25 : 0.02;

                if (pu < minFraction * a || pv < minFraction * b)
                {
                    c.Block("patch-min", $"u: the patch is below {minFraction * 100:0} % of a side, which this {(clamped ? "Ritz" : "Navier")} solution cannot resolve; enter a larger patch.");
                    return;
                }

                area = pu.Value * pv.Value;
                break;
            default:
                c.Input("r_ring");
                rc = c.InputSi("r_ring");

                if (rc < 0.01 * a || rc > 0.99 * a)
                {
                    c.Block("ring-radius", "r_ring: the ring radius must lie between 1 % and 99 % of the plate radius.");
                    return;
                }

                area = 0;
                break;
        }

        var smallest = circular ? (load == Patch ? 2 * rc : 2 * a) : (load == Patch ? Math.Min(pu!.Value, pv!.Value) : span);

        if (load == Patch && smallest < t)
            c.Warn("patch-thin", $"{(circular ? "r0" : "u")}: the patch is smaller than the plate thickness; it is a concentrated load whose local stress thin-plate theory does not give.");

        double q, force;

        if (load == Ring)
        {
            c.Input("P");
            force = c.InputSi("P");
            q = 0;
        }
        else if (!forceBasis)
        {
            c.Input("q");
            q = c.InputSi("q");
            force = q * area;
        }
        else
        {
            c.Input("P");
            force = c.InputSi("P");
            q = force / area;
            c.Record("q", "P/A_loaded", $"{F(force / 1e3)} kN / {F(area * 1e6)} mm²", ScalarValue.Of(q / 1e3, "kPa"), "Pressure on the loaded area", "Kirchhoff theory");
        }

        if (load != Ring)
            c.Record("F_total", "q·A_loaded", $"{F(q / 1e3)} kPa × {F(area * 1e6)} mm²", ScalarValue.Of(force / 1e3, "kN"), "Total load on the plate", "Kirchhoff theory");

        c.Assign("D", "E*t^3/(12*(1 - nu^2))", "N*m", "Flexural rigidity of the plate", "Kirchhoff theory");
        var d = c.Si("D");

        double wMax, mVm, mPrin, f1;
        string where;
        var points = new List<(double X, double Y)>();
        var seriesName = "Deflection along the centre line";

        if (circular)
        {
            var plate = load == Ring
                ? PlateBendingMath.AxisymmetricPlate.Ring(a, rc, nu, d, force, clamped)
                : PlateBendingMath.AxisymmetricPlate.Patch(a, rc, nu, d, q, clamped);

            if (load == Uniform)
            {
                if (clamped)
                {
                    c.Assign("w_max", "q*a^4/(64*D)", "mm", "Maximum deflection (centre), clamped circular plate under uniform pressure", "T&WK Ch. 3");
                    c.Assign("sigma_edge", "3*q*a^2/(4*t^2)", "MPa", "Radial surface stress at the clamped edge", "T&WK Ch. 3");
                }
                else
                {
                    c.Assign("w_max", "(5 + nu)*q*a^4/(64*(1 + nu)*D)", "mm", "Maximum deflection (centre), simply supported circular plate under uniform pressure", "T&WK Ch. 3");
                    c.Assign("sigma_centre", "3*(3 + nu)*q*a^2/(8*t^2)", "MPa", "Surface stress at the centre (σ_r = σ_θ)", "T&WK Ch. 3");
                }
            }
            else
            {
                var beta = rc / a;
                c.Record("beta", "β = load radius / plate radius", $"{F(rc * 1e3)} mm / {F(a * 1e3)} mm", new ScalarValue(beta), "Radius ratio", "T&WK Ch. 3");

                if (load == Patch)
                {
                    var w0 = clamped ? PlateBendingMath.PatchCentreDeflectionClamped(a, rc, d, q) : PlateBendingMath.PatchCentreDeflectionSimple(a, rc, nu, d, q);
                    c.Record("w_max", clamped ? "q·a⁴/(64D)·(4β² − 3β⁴ + 4β⁴·ln β)" : "q·a⁴/(8D)·[(3 + ν)/(1 + ν)·(β²/2 − β⁴/4) + β⁴/2·ln β − β⁴/8]",
                        $"q = {F(q / 1e3)} kPa, a = {F(a * 1e3)} mm, β = {F(beta)}, D = {F(d)} N·m", ScalarValue.Of(w0 * 1e3, "mm"), "Maximum deflection (centre), central patch", "T&WK Ch. 3");
                }
                else
                {
                    var w0 = clamped ? PlateBendingMath.RingCentreDeflectionClamped(a, rc, d, force) : PlateBendingMath.RingCentreDeflectionSimple(a, rc, nu, d, force);
                    c.Record("w_max", clamped ? "F·a²/(16πD)·(1 − β² + 2β²·ln β)" : "F·a²/(16πD)·[(3 + ν)/(1 + ν)·(1 − β²) + 2β²·ln β]",
                        $"F = {F(force / 1e3)} kN, a = {F(a * 1e3)} mm, β = {F(beta)}, D = {F(d)} N·m", ScalarValue.Of(w0 * 1e3, "mm"), "Maximum deflection (centre), ring load", "T&WK Ch. 3");
                }
            }

            wMax = c.Si("w_max");
            var mc = plate.Mr(0);
            var me = plate.Mr(a);

            if (Math.Abs(me) < 1e-9 * Math.Abs(mc))
                me = 0;

            c.Record("M_c", "M_r = M_θ at the centre = −2D(1 + ν)·B", "from the constants of the solution", ScalarValue.Of(mc, "N*m/m"), "Bending moment at the centre", "T&WK Ch. 3");
            c.Record("M_edge", "M_r at the edge r = a", clamped ? "clamped: fixing moment" : "simply supported: 0", ScalarValue.Of(me, "N*m/m"), "Radial bending moment at the edge", "T&WK Ch. 3");
            var (vm, pr) = PlateBendingMath.ScanMoments(plate);
            mVm = vm.Value;
            mPrin = pr.Value;
            where = Where(vm.Radius, a, rc, load == Ring);
            c.Record("M_r_gov", "M_r at the governing point", $"r = {F(vm.Radius * 1e3)} mm", ScalarValue.Of(vm.Mr, "N*m/m"), "Radial moment at the von Mises maximum", "T&WK Ch. 3");
            c.Record("M_t_gov", "M_θ at the governing point", $"r = {F(vm.Radius * 1e3)} mm", ScalarValue.Of(vm.Mt, "N*m/m"), "Circumferential moment at the von Mises maximum", "T&WK Ch. 3");
            var lambda2 = PlateBendingMath.CircularFrequencyParameter(clamped, nu);
            f1 = lambda2 / (2 * Math.PI * a * a) * Math.Sqrt(d / (rho * t));
            c.Record("lambda2", clamped ? "λ² from J₀(λ)·I₁(λ) + I₀(λ)·J₁(λ) = 0" : "λ² from J₁(λ)/J₀(λ) + I₁(λ)/I₀(λ) = 2λ/(1 − ν)", clamped ? "clamped edge" : $"ν = {F(nu)}", new ScalarValue(lambda2), "Frequency parameter of the fundamental axisymmetric mode", "Leissa Ch. 2");
            var n = 20;

            for (var i = 0; i <= n; i++)
            {
                var r = a * i / n;
                points.Add((r * 1e3, plate.W(r) * 1e3));
            }

            seriesName = "Deflection along a radius";
        }
        else if (!clamped)
        {
            var nav = PlateBendingMath.NavierSimplySupported(a, b, nu, d, q, pu, pv, NavierTermCap, NavierTolerance);
            var terms = (double)nav.TermsPerDirection * nav.TermsPerDirection;
            var sums = $"{nav.TermsPerDirection} odd terms per direction ({F(terms)} terms), last change {F(nav.LastChange)}";
            c.Record("terms_used", "odd terms per direction N (m, n = 1, 3, … 2N − 1)", sums, new ScalarValue(nav.TermsPerDirection), "Terms of the Navier series used", "T&WK Ch. 5");

            if (!nav.Converged)
                c.Warn("navier-converge", $"a: the Navier series reached its cap of {NavierTermCap} odd terms per direction with a last change of {F(nav.LastChange)} (tolerance {NavierTolerance:0e0}); the result is accurate to about that change.");

            c.Record("w_max", load == Uniform ? "16q/(π⁶D)·Σ (−1)^((m+n)/2−1)/(m·n·(m²/a² + n²/b²)²), m, n odd" : "16q/(π⁶D)·Σ sin(mπu/2a)·sin(nπv/2b)/(m·n·(m²/a² + n²/b²)²), m, n odd", sums, ScalarValue.Of(nav.Deflection * 1e3, "mm"), "Maximum deflection (centre)", "T&WK Ch. 5");
            c.Record("M_x_c", "D·π²·Σ W_mn(m²/a² + ν·n²/b²)·sin·sin at the centre", sums, ScalarValue.Of(nav.Mx, "N*m/m"), "M_x at the centre", "T&WK Ch. 5");
            c.Record("M_y_c", "D·π²·Σ W_mn(n²/b² + ν·m²/a²)·sin·sin at the centre", sums, ScalarValue.Of(nav.My, "N*m/m"), "M_y at the centre", "T&WK Ch. 5");
            c.Record("M_xy_k", "|D(1 − ν)·π²·Σ W_mn·(mn/ab)| at a corner", sums, ScalarValue.Of(nav.MxyCorner, "N*m/m"), "Twisting moment at a corner", "T&WK Ch. 5");
            c.Record("R_corner", "2·M_xy at a corner", $"2 × {F(nav.MxyCorner)} N·m/m", ScalarValue.Of(2 * nav.MxyCorner / 1e3, "kN"), "Corner reaction holding the corner down", "T&WK Ch. 5");
            c.Record("M_vm_c", "√(M_x² − M_x·M_y + M_y²) at the centre", $"M_x = {F(nav.Mx)}, M_y = {F(nav.My)} N·m/m", ScalarValue.Of(nav.CentreVonMises, "N*m/m"), "Von Mises moment at the centre", "von Mises");
            c.Record("M_vm_k", "√3·|M_xy| at a corner", $"M_xy = {F(nav.MxyCorner)} N·m/m", ScalarValue.Of(nav.CornerVonMises, "N*m/m"), "Von Mises moment at a corner (pure twist)", "von Mises");
            var centreGoverns = nav.CentreVonMises >= nav.CornerVonMises;
            mVm = Math.Max(nav.CentreVonMises, nav.CornerVonMises);
            mPrin = Math.Max(Math.Max(Math.Abs(nav.Mx), Math.Abs(nav.My)), nav.MxyCorner);
            where = centreGoverns ? "centre" : "corner (twisting moment)";
            wMax = nav.Deflection;
            var omega = Math.PI * Math.PI * (1 / (a * a) + 1 / (b * b)) * Math.Sqrt(d / (rho * t));
            f1 = omega / (2 * Math.PI);
            c.Record("omega_11", "π²(1/a² + 1/b²)·√(D/(ρt))", $"a = {F(a * 1e3)} mm, b = {F(b * 1e3)} mm, D = {F(d)} N·m, ρt = {F(rho * t)} kg/m²", ScalarValue.Of(omega, "rad/s"), "Angular frequency of the (1,1) mode", "T&WK Ch. 5");

            for (var i = 0; i <= 40; i++)
            {
                var x = a * i / 40;
                points.Add((x * 1e3, PlateBendingMath.NavierAt(a, b, nu, d, q, pu, pv, x, b / 2, 48).W * 1e3));
            }
        }
        else
        {
            c.Warn("ritz-approx", "support: the clamped rectangle is a Ritz approximation (energy method with polynomial trial functions); the deflection is accurate, the stress converges more slowly (the order and last change are reported below; long plates and small patches are the worst). Compare a critical result with finite elements.");
            PlateBendingMath.ClampedRitz? ritz = null;
            var prevW = double.NaN;
            var prevS = double.NaN;
            var change = double.PositiveInfinity;
            var converged = false;
            (double VonMises, double X, double Y, double Principal) scan = default;

            try
            {
                for (var k = RitzStart; k <= RitzCap; k += 2)
                {
                    ritz = PlateBendingMath.ClampedRitz.Solve(a, b, nu, d, q, pu, pv, k);
                    var wc = ritz.At(a / 2, b / 2).W;
                    scan = ritz.Scan();

                    if (!double.IsNaN(prevW))
                    {
                        change = Math.Max(Math.Abs(wc - prevW) / Math.Abs(wc), Math.Abs(scan.VonMises - prevS) / Math.Max(scan.VonMises, 1e-300));
                        converged = change < RitzTolerance;
                    }

                    prevW = wc;
                    prevS = scan.VonMises;

                    if (converged)
                        break;
                }
            }
            catch (CalcException ex)
            {
                c.Block("engine", $"a: the Ritz solution failed ({ex.Message}).");
                return;
            }

            var sums = $"K = {ritz!.Terms} terms per direction, last change {F(change)}";
            c.Record("terms_used", "Ritz order K (terms per direction)", sums, new ScalarValue(ritz.Terms), "Order of the Ritz solution used", "T&WK Ch. 6");

            if (!converged)
                c.Warn("ritz-converge", $"a: the Ritz solution reached its highest order K = {RitzCap} with a last change of {F(change)} (tolerance {RitzTolerance:0e0}); the stress error can be several times that change (about {F(3 * change)}; an estimate, not a bound) because the plate is long or the patch small for this basis. Compare with finite elements.");

            var centre = ritz.At(a / 2, b / 2);
            wMax = centre.W;
            var edgeX = ritz.At(0, b / 2).Mx;
            var edgeY = ritz.At(a / 2, 0).My;
            c.Record("w_max", "Σ c_ij·X_i(½)·Y_j(½) (Ritz)", sums, ScalarValue.Of(wMax * 1e3, "mm"), "Maximum deflection (centre)", "T&WK Ch. 6");
            c.Record("M_x_c", "−D(w,xx + ν·w,yy) at the centre", sums, ScalarValue.Of(centre.Mx, "N*m/m"), "M_x at the centre", "T&WK Ch. 6");
            c.Record("M_y_c", "−D(w,yy + ν·w,xx) at the centre", sums, ScalarValue.Of(centre.My, "N*m/m"), "M_y at the centre", "T&WK Ch. 6");
            c.Record("M_edge", "the greater of |M_x(0, b/2)| and |M_y(a/2, 0)|", $"M_x = {F(edgeX)}, M_y = {F(edgeY)} N·m/m", ScalarValue.Of(Math.Max(Math.Abs(edgeX), Math.Abs(edgeY)), "N*m/m"), "Normal moment at the middle of the edge", "T&WK Ch. 6");
            mVm = scan.VonMises;
            mPrin = scan.Principal;
            where = $"x = {F(scan.X * 1e3)} mm, y = {F(scan.Y * 1e3)} mm (quarter-plate grid){(scan.X < 1e-9 * a || scan.Y < 1e-9 * b ? ", clamped edge" : string.Empty)}";
            var omega = ritz.FundamentalAngularFrequency(rho * t);
            f1 = omega / (2 * Math.PI);
            c.Record("omega_11", "lowest eigenvalue of K·c = ω²·M·c (Rayleigh–Ritz)", $"order K = {ritz.Terms}, ρt = {F(rho * t)} kg/m²", ScalarValue.Of(omega, "rad/s"), "Angular frequency of the fundamental mode", "Leissa Ch. 4");

            for (var i = 0; i <= 40; i++)
            {
                var x = a * i / 40;
                points.Add((x * 1e3, ritz.At(x, b / 2).W * 1e3));
            }
        }

        if (!double.IsFinite(wMax) || !double.IsFinite(mVm) || !double.IsFinite(mPrin) || !double.IsFinite(f1) || !(wMax > 0) || !(f1 > 0))
        {
            c.Block("engine", "a: the solution could not be evaluated for these inputs.");
            return;
        }

        c.Record("M_vm", "√(M_1² − M_1·M_2 + M_2² + 3M_12²) at the governing point", where, ScalarValue.Of(mVm, "N*m/m"), "Greatest von Mises moment per unit width", "von Mises");
        c.Record("sigma_max", "6·M_vm/t²", $"6 × {F(mVm)} N·m/m / ({F(t * 1e3)} mm)²", ScalarValue.Of(6 * mVm / (t * t) / 1e6, "MPa"), "Greatest von Mises surface stress", "Kirchhoff theory");
        c.Record("sigma_principal", "6·M_principal/t²", $"6 × {F(mPrin)} N·m/m / ({F(t * 1e3)} mm)²", ScalarValue.Of(6 * mPrin / (t * t) / 1e6, "MPa"), "Greatest principal surface stress", "Kirchhoff theory");
        c.Record("f1", "ω/2π", $"D = {F(d)} N·m, ρt = {F(rho * t)} kg/m²", ScalarValue.Of(f1, "Hz"), "Fundamental natural frequency (ω is the angular frequency, rad/s; f is in Hz)", "Leissa");
        c.Assign("w_over_t", "w_max/t", null, "Deflection over thickness", "Kirchhoff theory");

        if (c.Si("w_over_t") > 0.5)
            c.Warn("large-deflection", $"t: w/t = {F(c.Si("w_over_t"))} is above 0.5; membrane action matters and the small-deflection result is not valid (it overstates the deflection and bending stress and omits the membrane stress)." + (c.Si("w_over_t") > 1 ? " Above 1 the result is only an order of magnitude." : string.Empty));

        c.Assign("u_stress", "sigma_max/(f_y/gamma_M0)", null, "Stress utilisation", "BS EN 1993-1-1 6.2.1(5)");
        c.Record("w_lim", "span/ratio", $"{F(span * 1e3)} mm / {F(c.InputSi("defl_ratio"))}", ScalarValue.Of(span / c.InputSi("defl_ratio") * 1e3, "mm"), "Deflection limit", "Serviceability");
        c.Assign("u_defl", "w_max/w_lim", null, "Deflection utilisation", "Serviceability");

        foreach (var (symbol, text) in new[] { ("w_max", "Maximum deflection"), ("sigma_max", "Greatest von Mises stress"), ("sigma_principal", "Greatest principal stress"), ("f1", "Fundamental frequency"), ("u_stress", "Stress utilisation"), ("u_defl", "Deflection utilisation"), ("w_over_t", "Deflection over thickness"), ("D", "Flexural rigidity") })
            c.Output(symbol, text);

        foreach (var (symbol, text) in new[] { ("terms_used", "Series terms used"), ("M_x_c", "M_x at the centre"), ("M_y_c", "M_y at the centre"), ("M_xy_k", "Corner twisting moment"), ("R_corner", "Corner reaction"), ("M_edge", "Edge moment"), ("M_c", "Moment at the centre"), ("lambda2", "Frequency parameter λ²") })
        {
            if (c.Session.Find(symbol) is not null)
                c.Output(symbol, text);
        }

        c.Output("location", "Governing stress point", new TextValue(where));
        c.Series(new CalcSeries(seriesName, "position", "mm", "w", "mm", points));
        c.CheckUtilisation("Stress σ_vm ≤ f_y/γ_M0", "u_stress", "6.2.1(5)");
        c.CheckUtilisation("Deflection w_max ≤ span/ratio", "u_defl", "Serviceability");
    }

    private static string Where(double r, double a, double rc, bool ring)
    {
        if (r <= 1e-9 * a)
            return "centre";

        if (r >= a * (1 - 1e-9))
            return "edge";

        if (Math.Abs(r - rc) <= 1e-6 * a)
            return ring ? $"under the ring (r = {F(r * 1e3)} mm)" : $"edge of the patch (r = {F(r * 1e3)} mm)";

        return $"r = {F(r * 1e3)} mm";
    }
}
