using Tempest.Core.Commands;
using Tempest.Core.EngineeringData;

namespace Tempest.Samples;

/// <summary>
/// Handles <see cref="CreateSampleDocumentCommand"/> by creating a new
/// document of kind <see cref="EngineeringDataSampleModule.SampleDocumentKind"/>
/// through <see cref="IEngineeringDocumentStore"/>.
/// </summary>
public sealed class CreateSampleDocumentCommandHandler : ICommandHandler<CreateSampleDocumentCommand>
{
    private readonly IEngineeringDocumentStore _documentStore;

    /// <summary>
    /// Initialises a new instance of the <see cref="CreateSampleDocumentCommandHandler"/> class.
    /// </summary>
    /// <param name="documentStore">The Engineering Data Model service this handler creates through.</param>
    public CreateSampleDocumentCommandHandler(IEngineeringDocumentStore documentStore)
    {
        ArgumentNullException.ThrowIfNull(documentStore);

        _documentStore = documentStore;
    }

    /// <inheritdoc />
    public async Task<CommandResult> HandleAsync(CreateSampleDocumentCommand command, CancellationToken cancellationToken)
    {
        var document = await _documentStore.CreateAsync(
            EngineeringDataSampleModule.SampleDocumentKind, "Created via command.", cancellationToken)
            .ConfigureAwait(false);

        return CommandResult.Success($"Created document '{document.Id}'.");
    }
}
