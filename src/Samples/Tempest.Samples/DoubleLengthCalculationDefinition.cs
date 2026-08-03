using Tempest.Core.Calculations;
using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Samples;

/// <summary>
/// A deliberately trivial, illustrative calculation — doubles a length —
/// used to demonstrate the Calculation Framework's own dispatch,
/// metadata, assumption, constraint, intermediate-result, and
/// material-reference capabilities without inventing any real
/// engineering formula.
/// </summary>
public sealed class DoubleLengthCalculationDefinition : ICalculationDefinition<Quantity<Length>, Quantity<Length>>
{
    /// <summary>The Id this calculation is registered under.</summary>
    public const string Id = "sample.double-length";

    /// <inheritdoc />
    public string CalculationId => Id;

    /// <inheritdoc />
    public CalculationMetadata Metadata { get; } = new(
        Name: "Double Length (Sample)",
        Description: "A trivial, illustrative calculation that doubles a length — not a real engineering formula.",
        Category: "Sample",
        Assumptions: [new CalculationAssumption("The input represents a valid physical length.", "Demonstration only.")],
        Constraints: [new CalculationConstraint("Input length must be positive.")]);

    /// <inheritdoc />
    /// <exception cref="CalculationInputInvalidException"><paramref name="input"/>'s own value is not positive.</exception>
    public Quantity<Length> Calculate(Quantity<Length> input, CalculationContext context)
    {
        var isPositive = input.Value > 0;
        context.RecordConstraintCheck("Input length must be positive.", isPositive, $"Input value was {input.Value}.");

        if (!isPositive)
            throw new CalculationInputInvalidException($"Input length must be positive; received {input.Value}.");

        var doubled = input * 2.0;
        context.RecordIntermediate("Doubled value", doubled);
        context.ReferenceMaterial(MaterialsSampleModule.SampleMaterialId);

        return doubled;
    }
}
