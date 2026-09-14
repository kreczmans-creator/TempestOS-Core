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
    /// <param name="input">The calculation's own input.</param>
    /// <param name="context">The fresh recorder this execution's intermediate results and referenced material Ids are written to.</param>
    /// <param name="cancellationToken">
    /// Observed between steps by a definition whose own work is iterative
    /// or long-running (`TD-21`) — most definitions are a handful of closed-form
    /// expressions and have nothing worth checking it against, so ignoring it
    /// is a legitimate implementation. <see cref="ICalculationEngine.ExecuteAsync{TInput, TResult}"/>'s
    /// own token reaches here unchanged.
    /// </param>
    /// <exception cref="CalculationInputInvalidException"><paramref name="input"/> fails this calculation's own validation.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled and the definition observed it.</exception>
    TResult Calculate(TInput input, CalculationContext context, CancellationToken cancellationToken = default);
}
