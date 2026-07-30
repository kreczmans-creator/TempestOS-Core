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
}
