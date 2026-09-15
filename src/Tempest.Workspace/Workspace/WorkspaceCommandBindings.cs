using System.Globalization;
using Tempest.Core.Commands;
using Tempest.Core.EngineeringDomain;

namespace Tempest.Workspace;

/// <summary>
/// The parameter shapes, validation callbacks and confirmation wording the
/// six discipline registrations' own <see cref="CommandBinding"/>s share —
/// TD-77 Stage 3.
/// </summary>
/// <remarks>
/// <para>
/// <b>Not a second registration mechanism.</b> Nothing here builds a
/// binding, registers a descriptor, or knows a command type: every
/// <c>build</c> lambda stays hand-written at the call site that registers
/// its own descriptor and handler, closing over that command's real
/// constructor (<c>ADR-0037</c>, and <see cref="CommandBinding"/>'s own
/// remarks). This holds only the pieces that would otherwise be copied
/// verbatim seventy-odd times — a length rule, an enum's own names, a
/// confirmation sentence — so that one rule cannot drift into six
/// slightly different rules across six files.
/// </para>
/// <para>
/// <b>Validation is preserved, never invented.</b> Every rule below
/// already exists somewhere in the running product: the 200-character
/// limit and the "an identifier is required" rule come from
/// <c>RibbonObjectActionHandlers</c>' own <c>InputDialog</c> prompts, and
/// the non-blank rules come from the command constructors themselves,
/// which throw <see cref="ArgumentException"/> on a blank value. That
/// second source is not optional: a throw out of
/// <see cref="CommandBinding.Build"/> is a defect in the binding
/// (<see cref="CommandBinding"/>'s own remarks), so a binding whose
/// constructor rejects blank input must reject it as a value first.
/// </para>
/// </remarks>
internal static class WorkspaceCommandBindings
{
    /// <summary>
    /// The display-name limit every Ribbon Create prompt already applies
    /// (<c>RibbonObjectActionHandlers</c>, <c>WP 10.5B</c>/<c>WP 10.7A</c>).
    /// </summary>
    internal const int MaxNameLength = 200;

    // The one capability this platform genuinely does not have yet. Named
    // specifically because ADR-0070 requires an unavailable command to
    // state its own reason rather than fall through to a generic one. The
    // object-picker half of this pair (U1) is gone: WP 20.2A built the
    // picker FCR-0073 named, so the fifteen commands that used to declare
    // ObjectPickerRequired now declare real bindings instead (Destination/
    // RequiredObjectReference below).
    private const string NoStructuredInput =
        "this platform's command input surface collects single-line text only, and cannot collect that.";

    /// <summary>
    /// The reason a command needing structured or binary input declares —
    /// <c>U2</c>, structured-input unavailable.
    /// </summary>
    /// <param name="whatIsMissing">What must be supplied, phrased as the sentence's own subject.</param>
    internal static string StructuredInputRequired(string whatIsMissing) => $"{whatIsMissing}, and {NoStructuredInput}";

    /// <summary>The Ribbon's own delete confirmation wording, kept identical.</summary>
    internal static string DeleteConfirmation(string noun) => $"Delete the selected {noun}? This cannot be undone.";

    /// <summary>The Ribbon's own duplicate confirmation wording, kept identical.</summary>
    internal static string DuplicateConfirmation(string noun) => $"Create a duplicate of the selected {noun}?";

    /// <summary>A free-text value with no rule of its own — the collected string reaches the command verbatim.</summary>
    internal static CommandParameter Text(string name, string label, string? defaultValue = null) =>
        new(name, label, defaultValue);

    /// <summary>A value the command's own constructor rejects when blank.</summary>
    internal static CommandParameter Required(string name, string label, string? defaultValue = null) =>
        new(name, label, defaultValue, Validate: value =>
            string.IsNullOrWhiteSpace(value) ? $"'{label}' is required." : null);

    /// <summary>
    /// A display name: non-blank (every <c>Create</c>/<c>Rename</c>
    /// constructor throws otherwise) and within the Ribbon's own
    /// already-applied <see cref="MaxNameLength"/> limit.
    /// </summary>
    internal static CommandParameter ObjectName(string name, string label, string? defaultValue = null) =>
        new(name, label, defaultValue, Validate: value => value switch
        {
            _ when string.IsNullOrWhiteSpace(value) => $"'{label}' is required.",
            { Length: > MaxNameLength } => $"'{label}' is too long ({MaxNameLength} characters max).",
            _ => null,
        });

    /// <summary>
    /// A closed set of values — a discipline's own already-declared
    /// <c>SupportedKinds</c> constant, or an enum's own names. Never a
    /// widened or re-derived set.
    /// </summary>
    internal static CommandParameter Choice(
        string name, string label, IReadOnlyList<string> allowedValues, string? defaultValue = null) =>
        new(name, label, defaultValue, allowedValues);

    /// <summary>An enum's own names, matched the same case-insensitive way the Ribbon's own <c>Enum.TryParse</c> prompts already do.</summary>
    internal static CommandParameter EnumChoice<TEnum>(string name, string label, string? defaultValue = null)
        where TEnum : struct, Enum =>
        new(name, label, defaultValue, Enum.GetNames<TEnum>());

    /// <summary>
    /// A decimal, rejected before <see cref="CommandBinding.Build"/> runs
    /// rather than thrown out of it — <c>SetBomLineCommand.Quantity</c>'s
    /// own requirement.
    /// </summary>
    internal static CommandParameter Decimal(string name, string label, string? defaultValue = null) =>
        new(name, label, defaultValue, Validate: value =>
            ParseDecimal(value) is null ? $"'{label}' must be a number." : null);

    /// <summary>
    /// Parses a decimal the one way this platform parses one — invariant
    /// culture, so a value that validated is a value that builds.
    /// </summary>
    internal static decimal? ParseDecimal(string value) =>
        decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;

    /// <summary>
    /// Returns <paramref name="allowedValues"/>' own entry matching
    /// <paramref name="value"/>.
    /// </summary>
    /// <remarks>
    /// <see cref="CommandParameter.AllowedValues"/> is matched
    /// case-insensitively, but a <c>Kind</c> is a canonical vocabulary
    /// value every factory switches on ordinally (<c>ADR-0105</c>). Without
    /// this, <c>"part"</c> would validate and then fail in the handler for
    /// a reason the user could not act on.
    /// </remarks>
    internal static string Canonical(IReadOnlyList<string> allowedValues, string value)
    {
        foreach (var allowed in allowedValues)
        {
            if (string.Equals(allowed, value, StringComparison.OrdinalIgnoreCase))
                return allowed;
        }

        return value;
    }

    /// <summary>
    /// An optional string the command takes as <see langword="null"/> when
    /// unset. A declared parameter is always collected, so "left blank"
    /// is the only way to say "leave it unset" — and blank is exactly what
    /// the command's own <see langword="null"/> already means.
    /// </summary>
    internal static string? OrNull(string value) => string.IsNullOrWhiteSpace(value) ? null : value;

    /// <summary>
    /// An existing object's own Id, chosen through the platform's
    /// object-reference picker (`FCR-0073`, `WP 20.2A`) rather than typed —
    /// optional: blank means "none" (a top-level Move/Copy destination, an
    /// ungrouped Requirement, a root Requirement Group). <paramref name="allowedKinds"/>
    /// only narrows what the picker itself offers; it is never re-checked
    /// here, because the domain's own <c>IHasParent.MoveAsync</c> is the one
    /// place a wrong choice is actually judged (a circular assignment), not
    /// this parameter's own <see cref="CommandParameter.Check"/>.
    /// </summary>
    internal static CommandParameter Destination(string name, string label, IReadOnlyList<string>? allowedKinds = null) =>
        new(name, label, DefaultValue: string.Empty, ObjectPickerKinds: allowedKinds ?? [],
            Validate: value => string.IsNullOrEmpty(value) || Guid.TryParse(value, out _)
                ? null
                : $"'{label}' must be an object chosen from the picker.");

    /// <summary>
    /// An existing object's own Id, chosen through the platform's
    /// object-reference picker (`FCR-0073`, `WP 20.2A`) — required, unlike
    /// <see cref="Destination"/>: there is no "none" this command can act on
    /// (a Link's own target, an Add-to-Collection's own Collection, a
    /// Compare-Baselines' own second Baseline/Release).
    /// </summary>
    internal static CommandParameter RequiredObjectReference(string name, string label, IReadOnlyList<string>? allowedKinds = null) =>
        new(name, label, DefaultValue: Guid.Empty.ToString(), ObjectPickerKinds: allowedKinds ?? [],
            Validate: value => Guid.TryParse(value, out _)
                ? null
                : $"'{label}' is required — an object chosen from the picker.");

    /// <summary>
    /// Parses a <see cref="Destination"/>/<see cref="RequiredObjectReference"/>
    /// parameter's own collected value — blank means "none", exactly as
    /// <see cref="OrNull"/> means "unset" for free text. Never called on a
    /// value that has not already passed <see cref="CommandParameter.Check"/>:
    /// a malformed Guid reaching here is a defect in the binding, the same
    /// invariant <see cref="ParseDecimal"/>'s own callers already rely on.
    /// </summary>
    internal static Guid? ParseDestination(string value) =>
        string.IsNullOrEmpty(value) ? null : Guid.Parse(value);

    /// <summary>The selected object a single-target binding acts on. Never <see langword="null"/>: the binding declared <see cref="CommandContextRequirement.SelectedObject"/>.</summary>
    internal static CommandContextObject Target(CommandContext context) => context.Primary!;

    /// <summary>Every selected object's own Id, in selection order — what a bulk command acts on.</summary>
    internal static IReadOnlyList<Guid> SelectedIds(CommandContext context) =>
        context.Selection.Select(selected => selected.ObjectId).ToList();

    /// <summary>
    /// Resolves <paramref name="id"/>'s own real display name for a
    /// Move/Copy result message (`WP 20.10C`, PO finding T6) — the
    /// object's own <see cref="IHasBusinessIdentifier.DisplayName"/> when
    /// it carries one, its own real Id's text otherwise (never invented,
    /// and never the blank string a not-found lookup would otherwise
    /// produce). The status bar and Command History both read this
    /// result's own <see cref="CommandResult.Message"/> directly, so a
    /// message naming a raw Guid ("Moved 'a3f2…' under 'b7e1…'.") is,
    /// functionally, still invisible to the reader the finding was about.
    /// </summary>
    internal static async Task<string> DisplayNameAsync(EngineeringDomainContext context, Guid id, CancellationToken cancellationToken)
    {
        var found = await context.Repository.FindAsync(id, cancellationToken).ConfigureAwait(false);
        return (found as IHasBusinessIdentifier)?.DisplayName ?? id.ToString();
    }

    /// <summary>"under 'Name'" or "to top level" — the destination half of a Move/Copy result message, resolving <paramref name="newParentId"/> through <see cref="DisplayNameAsync"/>.</summary>
    internal static async Task<string> DestinationPhraseAsync(EngineeringDomainContext context, Guid? newParentId, CancellationToken cancellationToken) =>
        newParentId is { } parentId
            ? $"under '{await DisplayNameAsync(context, parentId, cancellationToken).ConfigureAwait(false)}'"
            : "to top level";

    /// <summary>
    /// The shared body every plain <c>Move*ObjectCommandHandler</c> over
    /// <see cref="IHasParent.MoveAsync"/> used to duplicate five times
    /// (`WP 20.10C`, PO finding T6): resolves both ends' own real display
    /// names for the result message instead of a raw Guid, and — the
    /// finding's own explicit acceptance — reports choosing the object's
    /// own current parent as "Already under 'Name'." rather than running
    /// (and then reporting as an ordinary Move) a structural write that
    /// changes nothing. <paramref name="target"/> is the same object
    /// <paramref name="targetObjectId"/> already names, as
    /// <see cref="IHasParent"/> — the caller's own "not found, or cannot
    /// be moved" check stays at the caller, before this runs, since that
    /// message differs by discipline in a couple of callers.
    /// </summary>
    internal static async Task<CommandResult> MoveResultAsync(
        EngineeringDomainContext context, ICommandDispatcher? dispatcher, IHasParent target, Guid targetObjectId, string targetKind,
        Guid? newParentId, Func<Guid?, ICommand> buildMove, CancellationToken cancellationToken)
    {
        var sourceName = (target as IHasBusinessIdentifier)?.DisplayName ?? targetObjectId.ToString();
        var previousParentId = target.ParentId;

        if (previousParentId == newParentId)
        {
            var already = newParentId is { } currentParentId
                ? $"Already under '{await DisplayNameAsync(context, currentParentId, cancellationToken).ConfigureAwait(false)}'."
                : "Already at top level.";
            return CommandResult.Success(already, targetObjectId, targetKind);
        }

        try
        {
            await target.MoveAsync(newParentId, cancellationToken).ConfigureAwait(false);
        }
        catch (CircularParentAssignmentException ex)
        {
            return CommandResult.Failure(ex.Message);
        }

        var destinationPhrase = await DestinationPhraseAsync(context, newParentId, cancellationToken).ConfigureAwait(false);
        var compensation = MoveCompensation(context, dispatcher, targetObjectId, targetKind, sourceName, previousParentId, newParentId, buildMove);
        return CommandResult.Success($"Moved '{sourceName}' {destinationPhrase}.", targetObjectId, targetKind, compensation);
    }

    // ==================================================================
    // Compensation (`WP 21.1A`, `ADR-0099`'s own addendum)
    // ==================================================================

    /// <summary>
    /// Dispatches <paramref name="command"/> as a compensation — after the
    /// identical archived-project guard check <c>CommandRegistry.Evaluate</c>
    /// performs for a <c>Mutates</c> binding, checked explicitly here
    /// because a compensation is dispatched directly through
    /// <see cref="ICommandDispatcher"/>, never through
    /// <see cref="ICommandRegistry"/>'s own Id-based path (no compensation
    /// is itself a Ribbon- or Palette-visible command). Never a direct
    /// repository write: <paramref name="command"/> reaches its own
    /// registered handler, which commits through the identical
    /// one-transaction, one-audit-row state path any other command's
    /// handler uses.
    /// </summary>
    internal static async Task<CommandResult> RunCompensationAsync(
        EngineeringDomainContext context, ICommandDispatcher dispatcher, Guid targetObjectId, string targetKind,
        ICommand command, CancellationToken cancellationToken)
    {
        var reason = new ArchivedProjectCommandGuard(context).FindReason(CommandContext.For(targetObjectId, targetKind));
        if (reason is not null)
            return CommandResult.Failure(reason);

        return await dispatcher.DispatchAsync(command, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// The compensation a real Move produces: undo moves back to
    /// <paramref name="previousParentId"/>; redo moves forward to
    /// <paramref name="newParentId"/> again — both through
    /// <paramref name="buildMove"/>, the same command type the original
    /// invocation itself used, with the recorded parent value each
    /// direction needs (the redo half of `WP 20.2C`'s own "record values,
    /// replay them" pattern, applied here directly rather than through a
    /// second Id-based round trip).
    /// </summary>
    internal static CommandCompensation? MoveCompensation(
        EngineeringDomainContext context, ICommandDispatcher? dispatcher, Guid targetObjectId, string targetKind, string sourceName,
        Guid? previousParentId, Guid? newParentId, Func<Guid?, ICommand> buildMove)
    {
        if (dispatcher is null)
            return null;

        return new CommandCompensation(
            $"Move '{sourceName}'",
            undo: ct => RunCompensationAsync(context, dispatcher, targetObjectId, targetKind, buildMove(previousParentId), ct),
            redo: ct => RunCompensationAsync(context, dispatcher, targetObjectId, targetKind, buildMove(newParentId), ct));
    }

    /// <summary>
    /// The compensation a real Delete produces: undo restores the deleted
    /// object through <paramref name="buildUndelete"/> (<see cref="IDeletable.UndeleteAsync"/>,
    /// `WP 21.1A`); redo deletes it again through <paramref name="buildDelete"/>
    /// — the same command type the original Delete itself used.
    /// </summary>
    internal static CommandCompensation? DeleteCompensation(
        EngineeringDomainContext context, ICommandDispatcher? dispatcher, Guid targetObjectId, string targetKind, string sourceName,
        Func<ICommand> buildDelete, Func<ICommand> buildUndelete)
    {
        if (dispatcher is null)
            return null;

        return new CommandCompensation(
            $"Delete '{sourceName}'",
            undo: ct => RunCompensationAsync(context, dispatcher, targetObjectId, targetKind, buildUndelete(), ct),
            redo: ct => RunCompensationAsync(context, dispatcher, targetObjectId, targetKind, buildDelete(), ct));
    }

    /// <summary>
    /// The compensation a real Create or Copy produces — both make a new
    /// object, so both invert the identical way: undo soft-deletes the
    /// object <paramref name="createdId"/> names, through
    /// <paramref name="buildDelete"/>; redo restores it through
    /// <paramref name="buildUndelete"/> — never a second Create/Copy, which
    /// would mint a second object under a new Id rather than restore the
    /// one this action actually made.
    /// </summary>
    internal static CommandCompensation? CreationCompensation(
        EngineeringDomainContext? context, ICommandDispatcher? dispatcher, Guid createdId, string kind, string description,
        Func<ICommand> buildDelete, Func<ICommand> buildUndelete)
    {
        if (context is null || dispatcher is null)
            return null;

        return new CommandCompensation(
            description,
            undo: ct => RunCompensationAsync(context, dispatcher, createdId, kind, buildDelete(), ct),
            redo: ct => RunCompensationAsync(context, dispatcher, createdId, kind, buildUndelete(), ct));
    }

    /// <summary>
    /// The compensation a real status transition produces, or
    /// <see langword="null"/> when the platform-wide lifecycle transition
    /// table (`ADR-0074`, consulted here exactly as
    /// <see cref="IHasLifecycle.TransitionAsync"/> itself already consults
    /// it) does not permit going back from <paramref name="newStatus"/> to
    /// <paramref name="previousStatus"/> — a Released document, for
    /// example, since <c>Released</c> permits only <c>Superseded</c>/
    /// <c>Obsolete</c>, never a return to <c>Approved</c>. A caller
    /// receiving <see langword="null"/> reports
    /// <see cref="UndoUnavailableForStatus"/> instead of a compensation.
    /// </summary>
    internal static CommandCompensation? StatusCompensation(
        EngineeringDomainContext context, ICommandDispatcher? dispatcher, Guid targetObjectId, string targetKind, string sourceName,
        LifecycleState previousStatus, LifecycleState newStatus, Func<LifecycleState, ICommand> buildSetStatus)
    {
        if (dispatcher is null || !context.LifecycleTable.IsPermitted(newStatus, previousStatus))
            return null;

        return new CommandCompensation(
            $"Status change for '{sourceName}'",
            undo: ct => RunCompensationAsync(context, dispatcher, targetObjectId, targetKind, buildSetStatus(previousStatus), ct),
            redo: ct => RunCompensationAsync(context, dispatcher, targetObjectId, targetKind, buildSetStatus(newStatus), ct));
    }

    /// <summary>The reason recorded on <see cref="CommandResult.UndoUnavailableReason"/> when <see cref="StatusCompensation"/> returns <see langword="null"/>.</summary>
    internal static string UndoUnavailableForStatus(LifecycleState previousStatus, LifecycleState newStatus) =>
        $"the lifecycle does not permit reversing '{previousStatus}' → '{newStatus}'.";
}
