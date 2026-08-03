namespace Tempest.Core.Calculations;

/// <summary>A single, registrable calculation, taking <typeparamref name="TInput"/> and producing <typeparamref name="TResult"/>.</summary>
public interface ICalculationDefinition<TInput, TResult>
{
    /// <summary>The identity this calculation is registered and dispatched under.</summary>
    string CalculationId { get; }

    /// <summary>Fixed, declarative metadata about this calculation — name, description, category, assumptions, and constraints.</summary>
    CalculationMetadata Metadata { get; }

    /// <summary>
    /// Performs the calculation. Must be a pure function of
    /// <paramref name="input"/> — no I/O, no shared mutable state.
    /// <paramref name="context"/> is the only side channel available, and
    /// is itself pure in effect: a fresh, non-shared recorder the engine
    /// discards after reading it back, never observable by any other
    /// execution.
    /// </summary>
    /// <exception cref="CalculationInputInvalidException"><paramref name="input"/> fails this calculation's own validation.</exception>
    TResult Calculate(TInput input, CalculationContext context);
}
