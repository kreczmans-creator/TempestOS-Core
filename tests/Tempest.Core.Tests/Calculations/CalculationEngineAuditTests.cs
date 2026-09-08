using Tempest.Core.Audit;
using Tempest.Core.Calculations;
using Tempest.Core.EngineeringData;
using Tempest.Core.Identity;
using Tempest.Core.Persistence;

namespace Tempest.Core.Tests.Calculations;

/// <summary>
/// Proves <see cref="CalculationEngine.ExecuteAsync{TInput, TResult}"/>
/// records a real <c>calculation.executed</c> audit row, attributed to the
/// session principal's own <see cref="IIdentity.Id"/> (`WP 17.2A`,
/// ADR-0146).
/// </summary>
public class CalculationEngineAuditTests
{
    private sealed class AddOneCalculation : ICalculationDefinition<double, double>
    {
        public const string Id = "test.audit.add-one";
        public string CalculationId => Id;
        public CalculationMetadata Metadata { get; } = new("Add One", null, null, [], []);
        public double Calculate(double input, CalculationContext context) => input + 1;
    }

    [Fact]
    public async Task ExecuteAsync_WithAuditRecorder_RecordsARowAttributedToTheSessionPrincipalsIdentityId()
    {
        const string identityId = "engineer.ada";

        var persistenceStore = new Tempest.Core.Tests.Persistence.InMemoryQueryablePersistenceStore();
        var accessor = new CurrentPrincipalAccessor();
        var permissionEvaluator = new PermissionEvaluator();

        accessor.SetCurrent(new PlatformPrincipal(
            new PlatformIdentity(identityId, "Ada"),
            [AuditQuery.QueryPermission]));

        var documentStore = new EngineeringDocumentStore(persistenceStore, accessor);
        var auditRecorder = new AuditRecorder(persistenceStore, accessor);
        var auditQuery = new AuditQuery(persistenceStore, accessor, permissionEvaluator);

        var engine = new CalculationEngine(documentStore, accessor, auditRecorder: auditRecorder);
        engine.RegisterDefinition(new AddOneCalculation());

        var record = await engine.ExecuteAsync<double, double>(AddOneCalculation.Id, 1.0);

        var rows = await auditQuery.QueryAsync(new AuditQueryCriteria(actorId: identityId));

        var row = Assert.Single(rows, r => r.Action == CalculationEngine.CalculationExecutedActionName);
        Assert.Equal(identityId, row.ActorId);
        Assert.Equal(record.Id.ToString(), row.Detail["Subject"]);
        Assert.Equal(AddOneCalculation.Id, row.Detail["CalculationId"]);
    }

    [Fact]
    public async Task ExecuteAsync_WithNoAuditRecorderSupplied_BehavesExactlyAsBefore_AndWritesNoRow()
    {
        var persistenceStore = new Tempest.Core.Tests.Persistence.InMemoryQueryablePersistenceStore();
        var accessor = new CurrentPrincipalAccessor();

        var documentStore = new EngineeringDocumentStore(persistenceStore, accessor);
        var engine = new CalculationEngine(documentStore, accessor);
        engine.RegisterDefinition(new AddOneCalculation());

        var exception = await Record.ExceptionAsync(() => engine.ExecuteAsync<double, double>(AddOneCalculation.Id, 1.0));

        Assert.Null(exception);

        var keys = await persistenceStore.ListKeysAsync(AuditRecorder.AuditCollectionName);
        Assert.Empty(keys);
    }
}
