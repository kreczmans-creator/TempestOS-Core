using System.Collections.Immutable;

namespace Tempest.Core.Identity;

/// <summary>
/// The concrete <see cref="ICurrentComponentAccessor"/> implementation.
/// </summary>
/// <remarks>
/// <para>
/// Backed by an <see cref="AsyncLocal{T}"/>-flowed <see cref="ImmutableStack{T}"/>,
/// not the ambient, <see langword="lock"/>-protected single-field pattern
/// <see cref="CurrentPrincipalAccessor"/> deliberately uses for its own,
/// genuinely different question. ADR-0044 itself explains exactly why
/// <see cref="AsyncLocal{T}"/> is wrong for <see cref="CurrentPrincipalAccessor"/>'s
/// own question (a user, once established, must remain visible to a wholly
/// separate, later, unrelated caller) — and exactly why it would be right
/// for a genuinely call-chain-scoped, nesting-sensitive question instead
/// (ADR-0111). "Which component's own code is currently executing" is
/// exactly that different question: enforcement always happens within the
/// same logical call chain that entered the component's code, and must
/// correctly revert when that chain returns — precisely what
/// <see cref="AsyncLocal{T}"/> provides and the ambient pattern does not.
/// </para>
/// <para>
/// <see cref="AsyncLocal{T}"/>'s own copy-on-write flow semantics require
/// replacing <c>_stack.Value</c> wholesale on every push/pop (an
/// <see cref="ImmutableStack{T}"/>, never a mutated <see cref="Stack{T}"/>)
/// — this is deliberate, not an over-engineering choice: a shared mutable
/// <see cref="Stack{T}"/> stored once in <c>_stack.Value</c> would not
/// correctly isolate concurrent, unrelated async call chains from each
/// other's own push/pop activity, since every flowed copy of an
/// <see cref="AsyncLocal{T}"/> value would then reference the very same
/// mutable instance.
/// </para>
/// <para>
/// <see cref="BeginScope"/> is deliberately not part of
/// <see cref="ICurrentComponentAccessor"/> itself — mirrors
/// <see cref="CurrentPrincipalAccessor.SetCurrent"/> being concrete-type-only
/// exactly: only a caller holding a direct reference to this concrete type
/// (the Host) can establish a component scope; every ordinary consumer
/// resolves the read-only interface.
/// </para>
/// </remarks>
public sealed class CurrentComponentAccessor : ICurrentComponentAccessor
{
    private readonly AsyncLocal<ImmutableStack<IPrincipal>> _stack = new();

    /// <inheritdoc />
    public IPrincipal? Current => (_stack.Value is { IsEmpty: false } stack) ? stack.Peek() : null;

    /// <summary>
    /// Pushes <paramref name="componentPrincipal"/> as current for the
    /// remainder of the calling async context, until the returned token is
    /// disposed, at which point the immediately-prior value (correctly,
    /// whatever it was — <see langword="null"/>, or a different component,
    /// supporting correct nesting for a cross-component call) is restored.
    /// </summary>
    /// <param name="componentPrincipal">The component principal to push as current.</param>
    /// <returns>
    /// A disposable token that restores the immediately-prior value when
    /// disposed. Must be disposed by the caller (typically via a
    /// <see langword="using"/> statement) once the component's code has
    /// finished executing.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="componentPrincipal"/> is <see langword="null"/>.
    /// </exception>
    public IDisposable BeginScope(IPrincipal componentPrincipal)
    {
        ArgumentNullException.ThrowIfNull(componentPrincipal);
        var previous = _stack.Value ?? ImmutableStack<IPrincipal>.Empty;
        _stack.Value = previous.Push(componentPrincipal);
        return new Scope(this, previous);
    }

    private sealed class Scope : IDisposable
    {
        private readonly CurrentComponentAccessor _owner;
        private readonly ImmutableStack<IPrincipal> _previous;
        private bool _disposed;

        public Scope(CurrentComponentAccessor owner, ImmutableStack<IPrincipal> previous)
        {
            _owner = owner;
            _previous = previous;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _owner._stack.Value = _previous;
        }
    }
}
