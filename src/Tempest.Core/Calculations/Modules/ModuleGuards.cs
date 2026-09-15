using System.Globalization;
using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.Calculations.Modules;

/// <summary>
/// The three things every `WP 21.7A` module does the same way: reject a
/// malformed input, refuse an input outside the method's limits, and
/// compare a utilisation against one with the framework's slack.
/// </summary>
/// <remarks>
/// Internal and static: these are conventions shared by the modules in
/// this folder, not a framework contract. The slack is
/// <see cref="BracketSectionCheckCalculationDefinition.AcceptanceRelativeTolerance"/>,
/// reused rather than restated, for the reason set out on that constant.
/// </remarks>
internal static class ModuleGuards
{
    /// <summary>The relative slack every module's acceptance comparison allows — one part in a billion.</summary>
    public const double AcceptanceRelativeTolerance = BracketSectionCheckCalculationDefinition.AcceptanceRelativeTolerance;

    /// <summary>
    /// Records <paramref name="constraint"/> as a constraint check and, when
    /// it does not hold, rejects the input with
    /// <see cref="CalculationInputInvalidException"/>.
    /// </summary>
    /// <exception cref="CalculationInputInvalidException"><paramref name="satisfied"/> is <see langword="false"/>.</exception>
    public static void Require(CalculationContext context, string constraint, bool satisfied, string actual)
    {
        context.RecordConstraintCheck(constraint, satisfied, $"Received {actual}.");

        if (!satisfied)
            throw new CalculationInputInvalidException($"{constraint} Received {actual}.");
    }

    /// <summary>
    /// Records <paramref name="constraint"/> as a constraint check on the
    /// input named <paramref name="inputName"/> and, when it does not hold,
    /// rejects the input with <see cref="CalculationInputInvalidException"/>.
    /// The offending value is shown as the calculation's form shows it —
    /// a quantity in the descriptor's default unit for that input, never the
    /// SI base value (see <see cref="Describe{TInput}"/>).
    /// </summary>
    /// <exception cref="CalculationInputInvalidException"><paramref name="satisfied"/> is <see langword="false"/>.</exception>
    public static void Require<TInput>(CalculationContext context, string constraint, bool satisfied, TInput input, string inputName)
        where TInput : class =>
        Require(context, constraint, satisfied, Describe(input, inputName));

    /// <summary>
    /// The value of <paramref name="inputName"/> on <paramref name="input"/>
    /// as the form, the results and the working show it: a quantity in the
    /// descriptor's default unit for that input to six figures ("210 GPa",
    /// "1500 r/min", "3 mm"), a number to six figures, text quoted, and
    /// nothing as "nothing". A quantity whose input has no descriptor is
    /// shown in its own unit.
    /// </summary>
    /// <exception cref="ArgumentException"><typeparamref name="TInput"/> has no readable property named <paramref name="inputName"/>.</exception>
    public static string Describe<TInput>(TInput input, string inputName)
        where TInput : class
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentException.ThrowIfNullOrWhiteSpace(inputName);

        var property = typeof(TInput).GetProperty(inputName)
            ?? throw new ArgumentException($"{typeof(TInput).Name} has no input '{inputName}'.", nameof(inputName));
        var value = property.GetValue(input);

        if (value is null)
            return "nothing";

        var type = value.GetType();

        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Quantity<>))
        {
            var dimension = type.GetGenericArguments()[0].Name;
            var descriptor = CalculationModuleDescriptors.All
                .FirstOrDefault(d => d.InputType == typeof(TInput))?.Inputs
                .FirstOrDefault(i => i.Name == inputName && i.DimensionName == dimension);

            if (descriptor?.DefaultUnitSymbol is { } symbol && CalculationInputUnits.IsKnown(dimension))
                return $"{Six(CalculationInputUnits.ValueIn(dimension, value, symbol))} {symbol}";

            var magnitude = (double)type.GetProperty(nameof(Quantity<Length>.Value))!.GetValue(value)!;
            var unit = type.GetProperty(nameof(Quantity<Length>.Unit))!.GetValue(value)!;
            return $"{Six(magnitude)} {unit.GetType().GetProperty(nameof(Unit<Length>.Symbol))!.GetValue(unit)}";
        }

        return value switch
        {
            double number => Six(number),
            int count => count.ToString(CultureInfo.InvariantCulture),
            string text => $"'{text}'",
            _ => value.ToString() ?? "nothing",
        };
    }

    private static string Six(double value) => EngineeringNumber.Format(value);

    /// <summary>
    /// Records a method limit as an unsatisfied constraint check and returns
    /// the refusal reason for the result to carry. Never throws: see
    /// <see cref="EngineeringCheckOutcome.OutsideMethodLimits"/>.
    /// </summary>
    public static string Refuse(CalculationContext context, string limit, string reason)
    {
        context.RecordConstraintCheck(limit, false, reason);
        return reason;
    }

    /// <summary>Whether a utilisation (demand over capacity) is at or below one, with the framework's slack.</summary>
    public static bool IsMet(double utilisation) => utilisation <= 1.0 + AcceptanceRelativeTolerance;

    /// <summary>The outcome for a set of criteria that were all met or not.</summary>
    public static EngineeringCheckOutcome OutcomeOf(bool everyCriterionMet) =>
        everyCriterionMet ? EngineeringCheckOutcome.MeetsCriteria : EngineeringCheckOutcome.DoesNotMeetCriteria;
}
