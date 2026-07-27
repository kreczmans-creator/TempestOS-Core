namespace Tempest.Core.Modules;

/// <summary>
/// A minimal, convenient base implementation of <see cref="IModule"/> for
/// modules that have no lifecycle behaviour.
/// </summary>
/// <remarks>
/// <para>
/// Reduces every module's most repeated boilerplate — three near-identical
/// property getters for <see cref="Id"/>, <see cref="Name"/>, and
/// <see cref="Version"/> — to a single base-constructor call, without
/// introducing reflection, attributes, or code generation of any kind.
/// </para>
/// <para>
/// A concrete module deriving from this class still requires its own public
/// parameterless constructor for <c>IFrameworkDiscoveryService</c> to
/// discover it (discovery instantiates the concrete type directly via
/// <see cref="Activator.CreateInstance(Type)"/>) — this base class does not,
/// and cannot, change that requirement. A typical derived constructor calls
/// <c>: base("my.module.id", "My Module", "1.0.0")</c> with literal values.
/// </para>
/// <para>
/// For modules that also participate in initialisation, startup, shutdown,
/// or disposal, derive from <see cref="ModuleLifecycleBase"/> instead, which
/// extends this class with <see cref="Modules.IModuleLifecycle"/>.
/// </para>
/// </remarks>
public abstract class ModuleBase : IModule
{
    /// <summary>
    /// Initialises a new instance of the <see cref="ModuleBase"/> class.
    /// </summary>
    /// <param name="id">The module's unique, stable identifier.</param>
    /// <param name="name">The module's human-readable display name.</param>
    /// <param name="version">The module's version string.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="id"/>, <paramref name="name"/>, or
    /// <paramref name="version"/> is <see langword="null"/>, empty, or
    /// whitespace.
    /// </exception>
    protected ModuleBase(string id, string name, string version)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Module Id must not be null, empty, or whitespace.", nameof(id));

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Module Name must not be null, empty, or whitespace.", nameof(name));

        if (string.IsNullOrWhiteSpace(version))
            throw new ArgumentException("Module Version must not be null, empty, or whitespace.", nameof(version));

        Id = id;
        Name = name;
        Version = version;
    }

    /// <inheritdoc />
    public string Id { get; }

    /// <inheritdoc />
    public string Name { get; }

    /// <inheritdoc />
    public string Version { get; }
}
