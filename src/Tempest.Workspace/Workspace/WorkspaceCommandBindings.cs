using System.Globalization;
using Tempest.Core.Commands;

namespace Tempest.App.Workspace;

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

    // The two capabilities this platform genuinely does not have yet.
    // Named specifically, and per command, because ADR-0070 requires an
    // unavailable command to state its own reason rather than fall through
    // to a generic one.
    private const string NoObjectPicker =
        "this platform has no object picker to choose one with yet (FCR-0073, Copy/Move Destination-Picker Dialog & Wired Dispatch).";

    private const string NoStructuredInput =
        "this platform's command input surface collects single-line text only, and cannot collect that.";

    /// <summary>
    /// The reason a command needing a destination/target object declares —
    /// <c>U1</c>, object-picker unavailable.
    /// </summary>
    /// <param name="whatIsMissing">
    /// What must be chosen, phrased as the sentence's own subject — for
    /// example, <c>"Moving a Calculation needs a destination parent"</c>.
    /// </param>
    internal static string ObjectPickerRequired(string whatIsMissing) => $"{whatIsMissing}, and {NoObjectPicker}";

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

    /// <summary>The selected object a single-target binding acts on. Never <see langword="null"/>: the binding declared <see cref="CommandContextRequirement.SelectedObject"/>.</summary>
    internal static CommandContextObject Target(CommandContext context) => context.Primary!;

    /// <summary>Every selected object's own Id, in selection order — what a bulk command acts on.</summary>
    internal static IReadOnlyList<Guid> SelectedIds(CommandContext context) =>
        context.Selection.Select(selected => selected.ObjectId).ToList();
}
