using Tempest.Calc.Engine;
using Tempest.Calc.Engine.Units;
using Tempest.Calc.Engine.Values;

namespace Tempest.Calc.Sheets;

/// <summary>Turns one entered input into an engine value according to its spec. Assumes the input already passed <see cref="CalcInputValidator"/>.</summary>
public static class CalcInputBinder
{
    public static CalcValue Bind(CalcInputSpec spec, CalcInputValue? value)
    {
        ArgumentNullException.ThrowIfNull(spec);

        switch (spec.Kind)
        {
            case CalcInputKind.Quantity:
            {
                var unit = UnitCatalog.Parse(value?.Unit ?? spec.DefaultUnit);
                var number = value?.Number ?? spec.Default ?? throw new CalcEvaluationException($"{spec.Symbol}: no value was given.");
                return ScalarValue.Of(number, unit);
            }

            case CalcInputKind.Number:
                return new ScalarValue(value?.Number ?? spec.Default ?? throw new CalcEvaluationException($"{spec.Symbol}: no value was given."));

            case CalcInputKind.Choice:
                return new TextValue(value?.Text ?? spec.DefaultChoice ?? throw new CalcEvaluationException($"{spec.Symbol}: no choice was made."));

            case CalcInputKind.Text:
                return new TextValue(value?.Text ?? string.Empty);

            case CalcInputKind.Boolean:
                return new BoolValue(value?.Flag ?? (spec.Default is { } d && d != 0));

            case CalcInputKind.Vector:
            {
                var unit = UnitCatalog.Parse(value?.Unit ?? spec.DefaultUnit);
                return VectorValue.Of(value?.Vector ?? [], unit);
            }

            case CalcInputKind.Table:
                return new ScalarValue(value?.Rows?.Count ?? 0);

            default:
                throw new CalcEvaluationException($"{spec.Symbol}: unknown input kind.");
        }
    }
}
