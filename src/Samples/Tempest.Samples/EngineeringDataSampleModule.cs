using Tempest.Core.Commands;
using Tempest.Core.EngineeringData;
using Tempest.Core.Modules;

namespace Tempest.Samples;

/// <summary>
/// A small, self-contained reference module that demonstrates the
/// Engineering Data Model: it creates a document during its own
/// initialisation, revises it once, links it to a second document, and
/// registers two commands (create/revise) demonstrating both the
/// creation and revision paths for manual invocation.
/// </summary>
/// <remarks>
/// The living reference module <c>WP 7.1A</c> validates the Engineering
/// Data Model against — mirrors <see cref="AuditSampleModule"/>'s and
/// <see cref="SettingsSampleModule"/>'s own role for their own respective
/// frameworks. Carries <see cref="ModuleMetadataAttribute"/> so Discovery
/// can read its identity without instantiating it (ADR-0027), freeing its
/// constructor to request <see cref="IEngineeringDocumentStore"/>,
/// <see cref="ICommandDispatcher"/>, and <see cref="ICommandRegistry"/> —
/// all DI-public — via ordinary constructor injection.
/// </remarks>
[ModuleMetadata("tempest.samples.engineeringdata", "Engineering Data Sample", "1.0.0")]
public sealed class EngineeringDataSampleModule : ModuleLifecycleBase
{
    /// <summary>The <c>Kind</c> this module creates its own sample documents under.</summary>
    public const string SampleDocumentKind = "SampleEngineeringDocument";

    /// <summary>The relationship kind this module records between its two sample documents.</summary>
    public const string SampleRelationshipKind = "relatesTo";

    /// <summary>
    /// The <see cref="Commands.CommandDescriptor.Id"/> this module registers
    /// for <see cref="CreateSampleDocumentCommand"/>.
    /// </summary>
    public const string CreateSampleDocumentCommandId = "sample.engineeringdata-create";

    /// <summary>
    /// The <see cref="Commands.CommandDescriptor.Id"/> this module registers
    /// for <see cref="ReviseSampleDocumentCommand"/>.
    /// </summary>
    public const string ReviseSampleDocumentCommandId = "sample.engineeringdata-revise";

    private readonly IEngineeringDocumentStore _documentStore;
    private readonly ICommandDispatcher _commandDispatcher;
    private readonly ICommandRegistry _commandRegistry;

    /// <summary>
    /// Initialises a new instance of the <see cref="EngineeringDataSampleModule"/> class.
    /// </summary>
    /// <param name="documentStore">
    /// The Engineering Data Model service this module creates and revises
    /// documents through, resolved via ordinary constructor injection.
    /// </param>
    /// <param name="commandDispatcher">
    /// The Command Framework's dispatch-side surface this module registers
    /// its handlers through, resolved via ordinary constructor injection.
    /// </param>
    /// <param name="commandRegistry">
    /// The Command Framework's discovery-side surface this module
    /// registers its descriptors through, resolved via ordinary constructor
    /// injection.
    /// </param>
    public EngineeringDataSampleModule(
        IEngineeringDocumentStore documentStore,
        ICommandDispatcher commandDispatcher,
        ICommandRegistry commandRegistry)
        : base("tempest.samples.engineeringdata", "Engineering Data Sample", "1.0.0")
    {
        ArgumentNullException.ThrowIfNull(documentStore);
        ArgumentNullException.ThrowIfNull(commandDispatcher);
        ArgumentNullException.ThrowIfNull(commandRegistry);

        _documentStore = documentStore;
        _commandDispatcher = commandDispatcher;
        _commandRegistry = commandRegistry;
    }

    /// <summary>
    /// Gets the Id of the document this module created during its own
    /// <see cref="InitialiseAsync"/>, once initialisation has completed.
    /// </summary>
    public Guid? SampleDocumentId { get; private set; }

    /// <summary>
    /// Gets a value indicating whether <see cref="InitialiseAsync"/> has
    /// registered this module's commands.
    /// </summary>
    public bool HasRegistered { get; private set; }

    /// <inheritdoc />
    /// <remarks>
    /// Creates a document, revises it once, creates a second document, and
    /// links the first to the second — proving create/revise/link/read
    /// all work end to end against the real store — then registers
    /// <see cref="CreateSampleDocumentCommand"/> and
    /// <see cref="ReviseSampleDocumentCommand"/>'s handlers and
    /// descriptors.
    /// </remarks>
    public override async Task InitialiseAsync(CancellationToken cancellationToken)
    {
        var document = await _documentStore.CreateAsync(SampleDocumentKind, "Initial sample content.", cancellationToken)
            .ConfigureAwait(false);
        SampleDocumentId = document.Id;

        await _documentStore.ReviseAsync(document.Id, "Revised sample content.", "Sample revision.", cancellationToken)
            .ConfigureAwait(false);

        var relatedDocument = await _documentStore.CreateAsync(SampleDocumentKind, "Related sample content.", cancellationToken)
            .ConfigureAwait(false);
        await _documentStore.LinkAsync(document.Id, relatedDocument.Id, SampleRelationshipKind, cancellationToken)
            .ConfigureAwait(false);

        _commandDispatcher.RegisterHandler<CreateSampleDocumentCommand>(
            new CreateSampleDocumentCommandHandler(_documentStore));
        _commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: CreateSampleDocumentCommandId,
            displayName: "Create Sample Engineering Document",
            category: "Sample",
            description: "Creates a new sample engineering document.",
            createDefault: () => new CreateSampleDocumentCommand()));

        _commandDispatcher.RegisterHandler<ReviseSampleDocumentCommand>(
            new ReviseSampleDocumentCommandHandler(_documentStore, this));
        _commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: ReviseSampleDocumentCommandId,
            displayName: "Revise Sample Engineering Document",
            category: "Sample",
            description: "Revises this module's own sample engineering document.",
            createDefault: () => new ReviseSampleDocumentCommand()));

        HasRegistered = true;
    }
}
