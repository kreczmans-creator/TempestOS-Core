using Tempest.Core.Commands;
using Tempest.Core.Identity;
using Tempest.Core.Modules;

namespace Tempest.Samples;

/// <summary>
/// A small, self-contained reference module that demonstrates Identity
/// &amp; Permissions: it establishes a default local principal during its
/// own initialisation, then registers a command
/// (<see cref="CheckSamplePermissionCommand"/>) whose handler checks that
/// principal's own permissions.
/// </summary>
/// <remarks>
/// <para>
/// The living reference module <c>WP 6.1</c> validates Identity &amp;
/// Permissions against — mirrors <see cref="DiagnosticsSampleModule"/>'s
/// own role for Diagnostics. Carries <see cref="ModuleMetadataAttribute"/>
/// so Discovery can read its identity without instantiating it
/// (ADR-0027), freeing its constructor to request
/// <see cref="IIdentityService"/>, <see cref="ICurrentPrincipalAccessor"/>,
/// <see cref="IPermissionEvaluator"/>, <see cref="ICommandDispatcher"/>,
/// and <see cref="ICommandRegistry"/> — all DI-public platform services —
/// via ordinary constructor injection.
/// </para>
/// <para>
/// With no <c>Identity:Principals:sample.local-user:Roles</c>
/// configuration supplied, <see cref="SamplePermissionKey"/> is not
/// granted — the fail-closed default this module's own command reports
/// honestly, rather than hiding. A caller supplying that configuration
/// (see this module's own test coverage) observes the command succeed
/// instead, demonstrating both the granted and denied paths against the
/// same, unmodified module.
/// </para>
/// </remarks>
[ModuleMetadata("tempest.samples.identity", "Identity Sample", "1.0.0")]
public sealed class IdentitySampleModule : ModuleLifecycleBase
{
    /// <summary>
    /// The identity id this module establishes as current during its own
    /// initialisation.
    /// </summary>
    public const string SampleIdentityId = "sample.local-user";

    /// <summary>
    /// The permission key <see cref="CheckSamplePermissionCommandHandler"/>
    /// checks for.
    /// </summary>
    public const string SamplePermissionKey = "sample.read";

    /// <summary>
    /// The <see cref="Commands.CommandDescriptor.Id"/> this module registers
    /// for <see cref="CheckSamplePermissionCommand"/>.
    /// </summary>
    public const string CheckSamplePermissionCommandId = "sample.identity-check";

    private readonly IIdentityService _identityService;
    private readonly ICurrentPrincipalAccessor _currentPrincipalAccessor;
    private readonly IPermissionEvaluator _permissionEvaluator;
    private readonly ICommandDispatcher _commandDispatcher;
    private readonly ICommandRegistry _commandRegistry;

    /// <summary>
    /// Initialises a new instance of the <see cref="IdentitySampleModule"/> class.
    /// </summary>
    /// <param name="identityService">
    /// The Identity &amp; Permissions service this module establishes a
    /// principal through, resolved via ordinary constructor injection.
    /// </param>
    /// <param name="currentPrincipalAccessor">
    /// The service this module's registered command reads the current
    /// principal from, resolved via ordinary constructor injection.
    /// </param>
    /// <param name="permissionEvaluator">
    /// The service this module's registered command checks permissions
    /// against, resolved via ordinary constructor injection.
    /// </param>
    /// <param name="commandDispatcher">
    /// The Command Framework's dispatch-side surface this module registers
    /// its handler through, resolved via ordinary constructor injection.
    /// </param>
    /// <param name="commandRegistry">
    /// The Command Framework's discovery-side surface this module
    /// registers its descriptor through, resolved via ordinary constructor
    /// injection.
    /// </param>
    public IdentitySampleModule(
        IIdentityService identityService,
        ICurrentPrincipalAccessor currentPrincipalAccessor,
        IPermissionEvaluator permissionEvaluator,
        ICommandDispatcher commandDispatcher,
        ICommandRegistry commandRegistry)
        : base("tempest.samples.identity", "Identity Sample", "1.0.0")
    {
        ArgumentNullException.ThrowIfNull(identityService);
        ArgumentNullException.ThrowIfNull(currentPrincipalAccessor);
        ArgumentNullException.ThrowIfNull(permissionEvaluator);
        ArgumentNullException.ThrowIfNull(commandDispatcher);
        ArgumentNullException.ThrowIfNull(commandRegistry);

        _identityService = identityService;
        _currentPrincipalAccessor = currentPrincipalAccessor;
        _permissionEvaluator = permissionEvaluator;
        _commandDispatcher = commandDispatcher;
        _commandRegistry = commandRegistry;
    }

    /// <summary>
    /// Gets the principal this module established during its own
    /// <see cref="InitialiseAsync"/>, or <see langword="null"/> before
    /// that has run.
    /// </summary>
    public IPrincipal? EstablishedPrincipal { get; private set; }

    /// <summary>
    /// Gets a value indicating whether <see cref="InitialiseAsync"/> has
    /// registered this module's command.
    /// </summary>
    public bool HasRegistered { get; private set; }

    /// <inheritdoc />
    /// <remarks>
    /// Establishes <see cref="SampleIdentityId"/> as the current principal,
    /// then registers <see cref="CheckSamplePermissionCommand"/>'s handler
    /// and descriptor.
    /// </remarks>
    public override Task InitialiseAsync(CancellationToken cancellationToken)
    {
        EstablishedPrincipal = _identityService.EstablishCurrentPrincipal(SampleIdentityId);

        _commandDispatcher.RegisterHandler<CheckSamplePermissionCommand>(
            new CheckSamplePermissionCommandHandler(_currentPrincipalAccessor, _permissionEvaluator));

        _commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: CheckSamplePermissionCommandId,
            displayName: "Check Sample Permission",
            category: "Sample",
            description: "Reports whether the current principal holds the sample permission.",
            createDefault: () => new CheckSamplePermissionCommand()));

        HasRegistered = true;

        return Task.CompletedTask;
    }
}
