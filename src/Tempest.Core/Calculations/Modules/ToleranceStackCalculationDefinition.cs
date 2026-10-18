using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.Calculations.Modules;

/// <summary>Which way one contributor's dimension acts on the stack's result.</summary>
public enum ToleranceDirection
{
    /// <summary>The dimension adds to the result: a bigger part makes a bigger gap.</summary>
    Adds,

    /// <summary>The dimension subtracts from the result: a bigger part makes a smaller gap.</summary>
    Subtracts,
}

/// <summary>One dimension in a one-dimensional tolerance stack, typed as one row: "Housing bore, Adds, 40 mm, 0.1 mm, 0 mm".</summary>
/// <param name="Name">What the dimension is, in the engineer's own words.</param>
/// <param name="Direction">Whether it adds to or subtracts from the stack.</param>
/// <param name="Nominal">The basic (nominal) dimension.</param>
/// <param name="UpperDeviation">The upper limit less the nominal, signed: +0.1 mm for "40 +0.1/0".</param>
/// <param name="LowerDeviation">The lower limit less the nominal, signed: −0.12 mm for "20 0/−0.12". Never above the upper deviation.</param>
public sealed record ToleranceStackContributor(
    string Name,
    ToleranceDirection Direction,
    Quantity<Length> Nominal,
    Quantity<Length> UpperDeviation,
    Quantity<Length> LowerDeviation);

/// <summary>The inputs to one linear (one-dimensional) tolerance stack-up.</summary>
/// <param name="Contributors">Every dimension in the loop, in any order.</param>
/// <param name="MinimumResult">The smallest acceptable result (for a gap, the minimum clearance), or <see langword="null"/> where none is stated.</param>
/// <param name="MaximumResult">The largest acceptable result, or <see langword="null"/> where none is stated.</param>
/// <param name="SigmaPerTolerance">How many standard deviations each half-tolerance band represents (3 for a capable, centred process).</param>
/// <param name="StatisticalBasis">
/// The characterisation that makes a statistical (root-sum-square) result
/// meaningful — for example "every process capable, centred and independent,
/// ±3σ within tolerance". Left empty, no statistical result is produced at
/// all: the worst case stands alone, and the result says why.
/// </param>
public sealed record ToleranceStackInput(
    IReadOnlyList<ToleranceStackContributor> Contributors,
    Quantity<Length>? MinimumResult,
    Quantity<Length>? MaximumResult,
    double SigmaPerTolerance,
    string? StatisticalBasis);

/// <summary>The result of one tolerance stack-up. The statistical figures are <see langword="null"/> when no statistical basis was stated; the requirement figures when no requirement was.</summary>
/// <param name="Outcome">Whether the worst-case limits lie inside the stated requirement; <see langword="null"/> when no requirement was stated.</param>
/// <param name="ContributorCount">How many dimensions are in the stack.</param>
/// <param name="NominalResult">The sum of the signed nominals.</param>
/// <param name="MeanResult">The sum of the signed mid-limit dimensions: the centre the tolerances are reckoned about.</param>
/// <param name="WorstCaseTolerance">The arithmetic sum of every half-tolerance band: ± about the mean.</param>
/// <param name="WorstCaseMinimum">The smallest result any conforming set of parts can produce.</param>
/// <param name="WorstCaseMaximum">The largest result any conforming set of parts can produce.</param>
/// <param name="WorstCaseMinimumMargin">How far the worst-case minimum sits above the stated minimum; negative when below.</param>
/// <param name="WorstCaseMaximumMargin">How far the worst-case maximum sits below the stated maximum; negative when above.</param>
/// <param name="StatisticalNote">Why there is no statistical result, or the basis it rests on.</param>
/// <param name="RootSumSquareTolerance">The root-sum-square of the half-tolerance bands: ± about the mean.</param>
/// <param name="RootSumSquareMinimum">The mean less the root-sum-square tolerance.</param>
/// <param name="RootSumSquareMaximum">The mean plus the root-sum-square tolerance.</param>
/// <param name="ResultStandardDeviation">The predicted standard deviation of the result.</param>
/// <param name="ProcessCapabilityIndex">C_pk of the result against the stated requirement: the nearer limit's distance from the mean over three standard deviations.</param>
/// <param name="PredictedPartsPerMillionOutside">The predicted fraction of assemblies outside the stated requirement, in parts per million, for a normal result.</param>
public sealed record ToleranceStackResult(
    EngineeringCheckOutcome? Outcome,
    int ContributorCount,
    Quantity<Length> NominalResult,
    Quantity<Length> MeanResult,
    Quantity<Length> WorstCaseTolerance,
    Quantity<Length> WorstCaseMinimum,
    Quantity<Length> WorstCaseMaximum,
    Quantity<Length>? WorstCaseMinimumMargin,
    Quantity<Length>? WorstCaseMaximumMargin,
    string StatisticalNote,
    Quantity<Length>? RootSumSquareTolerance,
    Quantity<Length>? RootSumSquareMinimum,
    Quantity<Length>? RootSumSquareMaximum,
    Quantity<Length>? ResultStandardDeviation,
    double? ProcessCapabilityIndex,
    double? PredictedPartsPerMillionOutside);

/// <summary>
/// A linear one-dimensional tolerance stack-up: the worst case always, and
/// the root-sum-square (statistical) result where the engineer states the
/// basis for it. Specified in
/// <c>docs/engineering/calculations/calc.tolerance-stack.md</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Recovered from the v0.16.0 release-candidate suite.</b> The
/// original (<c>Tempest.Core.Calculations.Suite.ToleranceStackCalculationDefinition</c>,
/// lost with its integration branch) took symmetric tolerances and a
/// per-contributor statistical flag. This port keeps its two load-bearing
/// decisions — the verdict comes from the worst case alone, and no
/// statistical figure is produced without a stated basis — and adds
/// asymmetric (signed) deviations, the mean shift they cause, the result's
/// standard deviation, C_pk and the predicted fraction outside.
/// </para>
/// <para>
/// <b>The worst case is a guarantee; the statistical result is not.</b>
/// Root-sum-square is a prediction about a population, true only where every
/// contributor varies independently and roughly normally within its band.
/// Combining the tolerances that way without saying so reports a tighter
/// stack than the design has — so the basis is an input, recorded with the
/// result, and when it is empty the statistical figures are simply absent.
/// </para>
/// </remarks>
public sealed class ToleranceStackCalculationDefinition : ICalculationDefinition<ToleranceStackInput, ToleranceStackResult>
{
    /// <summary>The Id this calculation is registered under.</summary>
    public const string Id = "calc.tolerance-stack";

    /// <inheritdoc />
    public string CalculationId => Id;

    /// <inheritdoc />
    public CalculationMetadata Metadata { get; } = new(
        Name: "Linear Tolerance Stack-Up (Worst Case and RSS)",
        Description:
            "A one-dimensional tolerance loop: the nominal and mean result, the worst-case limits any conforming set of parts can "
            + "produce, and — where a statistical basis is stated — the root-sum-square limits, the result's standard deviation, "
            + "C_pk and the predicted fraction outside the requirement. The verdict is taken from the worst case.",
        Category: "Tolerancing",
        Assumptions:
        [
            new CalculationAssumption("Every contributor acts along the one axis of the stack.", "A linear stack: no angles, datum shift, form controls or bonus tolerance."),
            new CalculationAssumption("Statistical figures assume each contributor varies independently and normally about its mid-limit, its half-band being the stated number of standard deviations.", "Only computed when the engineer states the basis for that assumption; never assumed by the calculation."),
            new CalculationAssumption("The requirement comparison allows one part in a billion of the stack's own magnitude as slack.", "So a stack exactly on its limit is not failed by unit-conversion rounding."),
        ],
        Constraints:
        [
            new CalculationConstraint("At least one contributor; every contributor named; no lower deviation above its upper deviation; the sigma level positive; a stated minimum not above a stated maximum."),
            new CalculationConstraint("The worst-case limits must lie inside the stated requirement, where one is stated."),
        ]);

    /// <inheritdoc />
    /// <exception cref="CalculationInputInvalidException">Any constraint above is not met.</exception>
    public ToleranceStackResult Calculate(ToleranceStackInput input, CalculationContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(context);

        var contributors = input.Contributors ?? [];
        var minimumM = input.MinimumResult?.BaseValue;
        var maximumM = input.MaximumResult?.BaseValue;
        var sigmaLevel = input.SigmaPerTolerance;

        ModuleGuards.Require(context, "At least one contributor is required.", contributors.Count >= 1, $"{contributors.Count} contributor(s)");
        foreach (var c in contributors)
        {
            ModuleGuards.Require(context, "Every contributor must be named.", !string.IsNullOrWhiteSpace(c.Name), "an unnamed contributor");
            ModuleGuards.Require(
                context,
                "A contributor's lower deviation must not be above its upper deviation.",
                c.LowerDeviation.BaseValue <= c.UpperDeviation.BaseValue,
                $"{c.Name}: upper {Mm(c.UpperDeviation.BaseValue)} mm, lower {Mm(c.LowerDeviation.BaseValue)} mm");
        }

        ModuleGuards.Require(context, "Sigma per tolerance must be positive.", sigmaLevel > 0 && double.IsFinite(sigmaLevel), input, nameof(input.SigmaPerTolerance));
        ModuleGuards.Require(
            context,
            "The stated minimum result must not be above the stated maximum result.",
            minimumM is null || maximumM is null || minimumM <= maximumM,
            $"minimum {ModuleGuards.Describe(input, nameof(input.MinimumResult))}, maximum {ModuleGuards.Describe(input, nameof(input.MaximumResult))}");

        var nominalM = 0.0;
        var meanM = 0.0;
        var worstM = 0.0;
        var sumSquaresM2 = 0.0;

        foreach (var c in contributors)
        {
            var sign = c.Direction == ToleranceDirection.Adds ? 1.0 : -1.0;
            var upper = c.UpperDeviation.BaseValue;
            var lower = c.LowerDeviation.BaseValue;
            var mid = c.Nominal.BaseValue + (upper + lower) / 2.0;
            var half = (upper - lower) / 2.0;

            nominalM += sign * c.Nominal.BaseValue;
            meanM += sign * mid;
            worstM += half;
            sumSquaresM2 += half * half;

            context.RecordIntermediate(
                $"Contributor {c.Name}",
                $"{(sign > 0 ? "+" : "−")} {Mm(c.Nominal.BaseValue)} mm (+{Mm(upper)} / {Mm(lower)}); mid-limit {Mm(mid)} mm ± {Mm(half)} mm");
        }

        var worstMinM = meanM - worstM;
        var worstMaxM = meanM + worstM;

        context.RecordIntermediate("Nominal result", L(nominalM));
        context.RecordIntermediate("Mean result", L(meanM));
        context.RecordIntermediate("Worst-case tolerance", L(worstM));
        context.RecordIntermediate("Worst-case limits", $"{Mm(worstMinM)} mm to {Mm(worstMaxM)} mm");

        // Statistical figures only on a stated basis: see the class remarks.
        var basis = input.StatisticalBasis?.Trim();
        double? rssM = null;
        double? sigmaM = null;
        double? cpk = null;
        double? ppm = null;
        string note;

        if (string.IsNullOrEmpty(basis))
        {
            note = "No statistical result: no statistical basis was stated, and assuming one would report a tighter stack than the design has. The worst case is unaffected.";
            context.RecordIntermediate("Root-sum-square tolerance", "not computed — no statistical basis stated");
        }
        else
        {
            note = $"Statistical basis: {basis}";
            rssM = Math.Sqrt(sumSquaresM2);
            sigmaM = rssM / sigmaLevel;

            context.RecordIntermediate("Statistical basis", basis);
            context.RecordIntermediate("Root-sum-square tolerance", L(rssM.Value));
            context.RecordIntermediate("Result standard deviation", L(sigmaM.Value));

            if ((minimumM is not null || maximumM is not null) && sigmaM > 0)
            {
                var zs = new List<double>();
                var fraction = 0.0;

                if (minimumM is { } lsl)
                {
                    var z = (meanM - lsl) / sigmaM.Value;
                    zs.Add(z);
                    fraction += NormalUpperTail(z);
                    context.RecordIntermediate("Z to the minimum", z);
                }

                if (maximumM is { } usl)
                {
                    var z = (usl - meanM) / sigmaM.Value;
                    zs.Add(z);
                    fraction += NormalUpperTail(z);
                    context.RecordIntermediate("Z to the maximum", z);
                }

                cpk = zs.Min() / 3.0;
                ppm = fraction * 1e6;
                context.RecordIntermediate("C_pk", cpk.Value);
                context.RecordIntermediate("Predicted parts per million outside", ppm.Value);
            }
        }

        EngineeringCheckOutcome? outcome = null;
        double? minMarginM = null;
        double? maxMarginM = null;

        if (minimumM is not null || maximumM is not null)
        {
            // Slack scaled off the stack's own magnitude, not the limit's: a
            // symmetric stack's limits may legitimately be zero.
            var slack = Math.Max(Math.Max(Math.Abs(nominalM), Math.Abs(meanM)), worstM) * ModuleGuards.AcceptanceRelativeTolerance;
            var lowerOk = minimumM is null || worstMinM >= minimumM - slack;
            var upperOk = maximumM is null || worstMaxM <= maximumM + slack;

            minMarginM = minimumM is { } lo ? worstMinM - lo : null;
            maxMarginM = maximumM is { } hi ? hi - worstMaxM : null;
            outcome = ModuleGuards.OutcomeOf(lowerOk && upperOk);

            context.RecordConstraintCheck(
                "The worst-case limits must lie inside the stated requirement, where one is stated.",
                lowerOk && upperOk,
                $"Worst case {Mm(worstMinM)} mm to {Mm(worstMaxM)} mm against requirement "
                + $"{(minimumM is { } a ? Mm(a) + " mm" : "open")} to {(maximumM is { } b ? Mm(b) + " mm" : "open")}.");
        }

        return new ToleranceStackResult(
            outcome,
            contributors.Count,
            L(nominalM),
            L(meanM),
            L(worstM),
            L(worstMinM),
            L(worstMaxM),
            minMarginM is { } m1 ? L(m1) : null,
            maxMarginM is { } m2 ? L(m2) : null,
            note,
            rssM is { } r ? L(r) : null,
            rssM is { } r1 ? L(meanM - r1) : null,
            rssM is { } r2 ? L(meanM + r2) : null,
            sigmaM is { } s ? L(s) : null,
            cpk,
            ppm);
    }

    /// <summary>The probability a standard normal variable exceeds <paramref name="z"/>: ½ erfc(z / √2).</summary>
    internal static double NormalUpperTail(double z) => 0.5 * Erfc(z / Math.Sqrt(2.0));

    /// <summary>
    /// The complementary error function, by the Chebyshev fit of Press et al.,
    /// <i>Numerical Recipes</i> (2nd ed., §6.2, <c>erfcc</c>): fractional error
    /// below 1.2 × 10⁻⁷ everywhere — far tighter than any tolerance stack's inputs.
    /// </summary>
    internal static double Erfc(double x)
    {
        var z = Math.Abs(x);
        var t = 1.0 / (1.0 + 0.5 * z);
        var ans = t * Math.Exp(-z * z - 1.26551223 + t * (1.00002368 + t * (0.37409196 + t * (0.09678418 + t * (-0.18628806
            + t * (0.27886807 + t * (-1.13520398 + t * (1.48851587 + t * (-0.82215223 + t * 0.17087277)))))))));
        return x >= 0 ? ans : 2.0 - ans;
    }

    private static Quantity<Length> L(double metres) => new Quantity<Length>(metres, LengthUnits.Metre).ConvertTo(LengthUnits.Millimetre);

    private static string Mm(double metres) => EngineeringNumber.Format(metres * 1000.0);
}
