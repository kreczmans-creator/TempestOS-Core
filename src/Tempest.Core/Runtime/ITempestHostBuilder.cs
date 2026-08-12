using Tempest.Core.Configuration;

namespace Tempest.Core.Runtime;

/// <summary>
/// Assembles configuration sources and any other pre-registration inputs,
/// then produces a <see cref="ITempestHost"/>.
/// </summary>
/// <remarks>
/// The builder is the composition root's own entry point: it is the only
/// component permitted to construct a <see cref="ITempestHost"/>. It does not
/// itself build Configuration, Logging, Discovery, Registration, or
/// Dependency Injection — those remain owned and constructed by the host
/// itself, in order, once <see cref="ITempestHost.RunAsync"/> begins (see
/// <c>Host Lifecycle.md</c>'s phase table, where "Host Created" precedes
/// "Configuration Built"). The builder's own job is narrower: collect the
/// inputs the host will need once it starts.
/// </remarks>
public interface ITempestHostBuilder
{
    /// <summary>
    /// Adds a configuration source the host will use to build its
    /// <see cref="IConfigurationProvider"/> once run.
    /// </summary>
    /// <param name="source">The configuration source to add.</param>
    /// <returns>This builder, to allow chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">This builder has already built a host.</exception>
    ITempestHostBuilder AddConfigurationSource(IConfigurationSource source);

    /// <summary>
    /// Opts the resulting host's Module Discovery phase into discovering
    /// <see cref="Modules.IFaultInjectionModule"/> candidates, which are
    /// excluded by default.
    /// </summary>
    /// <returns>This builder, to allow chaining.</returns>
    /// <exception cref="InvalidOperationException">This builder has already built a host.</exception>
    /// <remarks>
    /// The explicit "enabled for validation runs" surface named by
    /// ADR-0102 — a fault-injection module (<c>Tempest.Validation.FaultInjection</c>)
    /// is never discovered unless this method is called before
    /// <see cref="Build"/>. Never called by <c>Tempest.App</c>'s or
    /// <c>Tempest.Desktop</c>'s own composition root, so ordinary application
    /// startup is unaffected.
    /// </remarks>
    ITempestHostBuilder EnableFaultInjectionModules();

    /// <summary>
    /// Builds a <see cref="ITempestHost"/> from the inputs collected so far.
    /// </summary>
    /// <returns>A new <see cref="ITempestHost"/>, in the <see cref="HostState.Created"/> state.</returns>
    /// <exception cref="InvalidOperationException">This builder has already built a host.</exception>
    /// <remarks>
    /// A builder produces at most one host. This mirrors, at the builder
    /// level, the same single-use discipline ADR-0015 establishes for the
    /// host itself.
    /// </remarks>
    ITempestHost Build();
}
