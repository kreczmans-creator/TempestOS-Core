using System.Globalization;
using Tempest.Core.BusinessGovernance;
using Tempest.Core.Commands;
using Tempest.Core.Projects;
using Tempest.Workspace.Mechanical;

namespace Tempest.Workspace.Projects;

/// <summary>
/// Wires the project commercial core's own six acts into a running
/// Workspace (`WP 19.0A`, `ADR-0150`) — mirrors
/// <see cref="Evidence.EvidenceWorkspaceRegistration.Register"/>'s own
/// shape, narrowed: the Project Kind already has a node provider and
/// facet provider (Mechanical's own), so this registers only the six
/// commands, none of the discovery machinery Evidence's own new Kind
/// needed.
/// </summary>
public static class ProjectCommercialWorkspaceRegistration
{
    private static readonly IReadOnlyList<string> BoundKinds = [MechanicalObjectFactoryRegistry.Project];

    /// <summary>Registers every project commercial command against <paramref name="commandDispatcher"/>/<paramref name="commandRegistry"/>.</summary>
    public static void Register(IProjectCommercialService projectCommercialService, ICommandDispatcher commandDispatcher, ICommandRegistry commandRegistry)
    {
        ArgumentNullException.ThrowIfNull(projectCommercialService);
        ArgumentNullException.ThrowIfNull(commandDispatcher);
        ArgumentNullException.ThrowIfNull(commandRegistry);

        commandDispatcher.RegisterHandler<SetProjectClientCommand>(new SetProjectClientCommandHandler(projectCommercialService));
        commandDispatcher.RegisterHandler<SetProjectPurchaseOrderCommand>(new SetProjectPurchaseOrderCommandHandler(projectCommercialService));
        commandDispatcher.RegisterHandler<SetProjectBudgetCommand>(new SetProjectBudgetCommandHandler(projectCommercialService));
        commandDispatcher.RegisterHandler<PinProjectRateCardCommand>(new PinProjectRateCardCommandHandler(projectCommercialService));
        commandDispatcher.RegisterHandler<SetProjectDatesCommand>(new SetProjectDatesCommandHandler(projectCommercialService));
        commandDispatcher.RegisterHandler<SetProjectManagerCommand>(new SetProjectManagerCommandHandler(projectCommercialService));

        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "project.set-client", displayName: "Set Client", category: "Projects",
            description: "Sets, or clears, the selected project's own client — a record id in the Organisation catalogue, never validated as a structure.")
        {
            Binding = new CommandBinding(
                CommandContextRequirement.SelectedObject,
                (context, values) => new SetProjectClientCommand(
                    WorkspaceCommandBindings.Target(context).ObjectId, WorkspaceCommandBindings.Target(context).Kind,
                    WorkspaceCommandBindings.OrNull(values["organisationId"])),
                [WorkspaceCommandBindings.Text("organisationId", "Client organisation id (blank clears it)", defaultValue: string.Empty)],
                BoundKinds),
        });

        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "project.set-purchase-order", displayName: "Set Purchase Order", category: "Projects",
            description: "Sets, or clears, the selected project's own client purchase-order reference.")
        {
            Binding = new CommandBinding(
                CommandContextRequirement.SelectedObject,
                (context, values) => new SetProjectPurchaseOrderCommand(
                    WorkspaceCommandBindings.Target(context).ObjectId, WorkspaceCommandBindings.Target(context).Kind,
                    WorkspaceCommandBindings.OrNull(values["reference"])),
                [WorkspaceCommandBindings.Text("reference", "Purchase order reference (blank clears it)", defaultValue: string.Empty)],
                BoundKinds),
        });

        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "project.set-budget", displayName: "Set Budget", category: "Projects",
            description: "Sets, or clears, the selected project's own budget, as \"amount currency\" (e.g. \"50000 GBP\").")
        {
            Binding = new CommandBinding(
                CommandContextRequirement.SelectedObject,
                (context, values) => new SetProjectBudgetCommand(
                    WorkspaceCommandBindings.Target(context).ObjectId, WorkspaceCommandBindings.Target(context).Kind,
                    ParseMoneyOrNull(values["budget"])),
                [new CommandParameter("budget", "Budget (\"amount currency\", blank clears)", DefaultValue: string.Empty, Validate: ValidateBudget)],
                BoundKinds),
        });

        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "project.pin-rate-card", displayName: "Pin Rate Card", category: "Projects",
            description: "Pins the Released rate card the selected project bills against, to the revision read. An unreleased card is refused, named.")
        {
            Binding = new CommandBinding(
                CommandContextRequirement.SelectedObject,
                (context, values) => new PinProjectRateCardCommand(
                    WorkspaceCommandBindings.Target(context).ObjectId, WorkspaceCommandBindings.Target(context).Kind,
                    values["rateCardId"]),
                [WorkspaceCommandBindings.Required("rateCardId", "Rate card")],
                BoundKinds),
        });

        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "project.set-dates", displayName: "Set Dates", category: "Projects",
            description: "Sets, or clears, the selected project's own start and target dates.")
        {
            Binding = new CommandBinding(
                CommandContextRequirement.SelectedObject,
                (context, values) => new SetProjectDatesCommand(
                    WorkspaceCommandBindings.Target(context).ObjectId, WorkspaceCommandBindings.Target(context).Kind,
                    ParseDateOrNull(values["startDate"]), ParseDateOrNull(values["targetDate"])),
                [
                    new CommandParameter("startDate", "Start date (yyyy-MM-dd, blank clears)", DefaultValue: string.Empty, Validate: ValidateOptionalDate),
                    new CommandParameter("targetDate", "Target date (yyyy-MM-dd, blank clears)", DefaultValue: string.Empty, Validate: ValidateOptionalDate),
                ],
                BoundKinds),
        });

        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "project.set-project-manager", displayName: "Set Project Manager", category: "Projects",
            description: "Sets, or clears, the principal managing the selected project, by identity id.")
        {
            Binding = new CommandBinding(
                CommandContextRequirement.SelectedObject,
                (context, values) => new SetProjectManagerCommand(
                    WorkspaceCommandBindings.Target(context).ObjectId, WorkspaceCommandBindings.Target(context).Kind,
                    WorkspaceCommandBindings.OrNull(values["identityId"])),
                [WorkspaceCommandBindings.Text("identityId", "Project manager identity id (blank clears it)", defaultValue: string.Empty)],
                BoundKinds),
        });
    }

    private static string? ValidateBudget(string value) =>
        string.IsNullOrWhiteSpace(value) || TryParseMoney(value, out _)
            ? null
            : "'Budget' must be \"<amount> <currency>\" (e.g. \"50000 GBP\"), or blank to clear.";

    private static string? ValidateOptionalDate(string value) =>
        string.IsNullOrWhiteSpace(value) || DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out _)
            ? null
            : "must be a valid date (yyyy-MM-dd), or blank to clear.";

    private static Money? ParseMoneyOrNull(string value) =>
        !string.IsNullOrWhiteSpace(value) && TryParseMoney(value, out var money) ? money : null;

    private static DateOnly? ParseDateOrNull(string value) =>
        !string.IsNullOrWhiteSpace(value) && DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date : null;

    private static bool TryParseMoney(string value, out Money money)
    {
        var parts = value.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length == 2 && decimal.TryParse(parts[0], NumberStyles.Number, CultureInfo.InvariantCulture, out var amount))
        {
            try
            {
                money = new Money(amount, new CurrencyCode(parts[1]));
                return true;
            }
            catch (ArgumentException)
            {
                // Falls through to the failure return below.
            }
        }

        money = default;
        return false;
    }
}
