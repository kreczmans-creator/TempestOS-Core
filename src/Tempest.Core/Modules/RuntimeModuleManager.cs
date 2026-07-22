using System.Collections.ObjectModel;
using Tempest.Core.Logging;

namespace Tempest.Core.Modules;

/// <summary>
/// The concrete, thread-safe <see cref="IRuntimeModuleManager"/> implementation.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="RuntimeModuleManager"/> is the single authoritative runtime catalogue of
/// every module known to TempestOS. It registers already-discovered modules (typically
/// produced by <see cref="ReflectionFrameworkDiscoveryService"/>) and exposes lookup
/// operations over them. It performs no reflection, no assembly scanning, no module
/// instantiation, no dependency injection, and no lifecycle execution — those
/// responsibilities belong to later stages of the module pipeline.
/// </para>
/// <para>
/// Registration order is preserved so that <see cref="Modules"/> and
/// <see cref="GetAll"/> are deterministic, unlike
/// <see cref="ReflectionFrameworkDiscoveryService"/>, which returns modules in
/// alphabetical order. A single lock guards all internal state; this is an
/// intentionally simple approach appropriate to the manager's current, lightweight
/// in-memory bookkeeping responsibilities, and avoids the complexity of lock-free or
/// per-field concurrency primitives that this work package does not need.
/// </para>
/// </remarks>
public sealed class RuntimeModuleManager : IRuntimeModuleManager
{
    private readonly object _gate = new();
    private readonly Dictionary<string, RuntimeModule> _modulesById = new(StringComparer.Ordinal);
    private readonly List<RuntimeModule> _registrationOrder = new();
    private readonly ILogger? _logger;

    /// <summary>
    /// Initialises a new instance of the <see cref="RuntimeModuleManager"/> class.
    /// </summary>
    /// <param name="logger">
    /// An optional logger used to record registration activity via the logging
    /// abstraction. May be <see langword="null"/> if logging is not required.
    /// </param>
    public RuntimeModuleManager(ILogger? logger = null)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public IReadOnlyCollection<RuntimeModule> Modules => GetAll();

    /// <inheritdoc />
    public RuntimeModule Register(ModuleDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        if (string.IsNullOrWhiteSpace(descriptor.Id))
        {
            throw new ArgumentException(
                "Descriptor Id must not be null, empty, or whitespace.",
                nameof(descriptor));
        }

        lock (_gate)
        {
            if (_modulesById.ContainsKey(descriptor.Id))
            {
                _logger?.Information(
                    $"Duplicate module registration rejected for '{descriptor.Id}'.");

                throw new DuplicateModuleRegistrationException(descriptor.Id);
            }

            var runtimeModule = new RuntimeModule(
                descriptor,
                ModuleState.Registered,
                DateTimeOffset.UtcNow);

            _modulesById.Add(descriptor.Id, runtimeModule);
            _registrationOrder.Add(runtimeModule);

            _logger?.Information(
                $"Module '{descriptor.Id}' registered ({descriptor.Name} v{descriptor.Version}).");

            return runtimeModule;
        }
    }

    /// <inheritdoc />
    public RuntimeModule Get(string moduleId)
    {
        if (TryGet(moduleId, out var module))
            return module;

        throw new ModuleNotRegisteredException(moduleId);
    }

    /// <inheritdoc />
    public bool TryGet(string moduleId, out RuntimeModule module)
    {
        lock (_gate)
        {
            if (_modulesById.TryGetValue(moduleId, out var found))
            {
                module = found;
                return true;
            }

            module = null!;
            return false;
        }
    }

    /// <inheritdoc />
    public bool IsRegistered(string moduleId)
    {
        lock (_gate)
        {
            return _modulesById.ContainsKey(moduleId);
        }
    }

    /// <inheritdoc />
    public IReadOnlyCollection<RuntimeModule> GetAll()
    {
        lock (_gate)
        {
            return new ReadOnlyCollection<RuntimeModule>(new List<RuntimeModule>(_registrationOrder));
        }
    }
}
