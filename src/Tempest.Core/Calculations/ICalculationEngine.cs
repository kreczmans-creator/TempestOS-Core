namespace Tempest.Core.Calculations;

/// <summary>Registers and dispatches calculations by Id, recording each execution.</summary>
public interface ICalculationEngine
{
    /// <summary>Registers a calculation definition. Expected to be called only during module initialisation, mirroring <c>Commands.ICommandRegistry.RegisterDescriptor</c>.</summary>
    /// <exception cref="DuplicateCalculationException"><paramref name="definition"/>'s own <c>CalculationId</c> is already registered.</exception>
    void RegisterDefinition<TInput, TResult>(ICalculationDefinition<TInput, TResult> definition);

    /// <summary>Executes the named calculation and durably records the result.</summary>
    /// <exception cref="CalculationDefinitionNotFoundException"><paramref name="calculationId"/> is not registered for the requested <typeparamref name="TInput"/>/<typeparamref name="TResult"/> signature.</exception>
    /// <exception cref="CalculationInputInvalidException">The registered definition rejected <paramref name="input"/>.</exception>
    Task<CalculationRecord<TResult>> ExecuteAsync<TInput, TResult>(string calculationId, TInput input, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads back a previously executed calculation by the record id
    /// <see cref="ExecuteAsync{TInput, TResult}"/> returned, or
    /// <see langword="null"/> where no such record exists.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The engine was write-only: it recorded every execution durably and
    /// offered no way to read one back as the typed result it was. A display
    /// reader existed at the application layer, but it flattens the result
    /// to a string, which is enough to show somebody and not enough to
    /// reproduce anything.
    /// </para>
    /// <para>
    /// That matters because a calculation's own record is where its pinned
    /// reference revisions live. Without a typed read-back, an engineer
    /// could not retrieve the calculation and see which revision of which
    /// material it stood on — which is the whole reproducibility claim.
    /// </para>
    /// </remarks>
    /// <typeparam name="TResult">The result type the calculation produced. Must match what was executed.</typeparam>
    /// <param name="recordId">The record id returned by the original execution.</param>
    /// <param name="cancellationToken">A token observed while reading.</param>
    /// <exception cref="CalculationException">The stored record cannot be read as <typeparamref name="TResult"/>.</exception>
    Task<CalculationRecord<TResult>?> FindRecordAsync<TResult>(Guid recordId, CancellationToken cancellationToken = default);
}
