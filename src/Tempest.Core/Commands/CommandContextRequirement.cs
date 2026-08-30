namespace Tempest.Core.Commands;

/// <summary>
/// What a <see cref="CommandBinding"/> needs to be present in a
/// <see cref="CommandContext"/> before its command can be constructed —
/// declared by the binding, never inferred from the command's Id or type.
/// </summary>
/// <remarks>
/// Every member here is demonstrated by an existing production command.
/// Nothing speculative is declared: a project requirement was considered
/// and removed after an audit of all 74 production discipline commands
/// found that none of them reads project scope at all.
/// </remarks>
[Flags]
public enum CommandContextRequirement
{
    /// <summary>Needs nothing from the context — a creation command, or a command carrying all its own data.</summary>
    None = 0,

    /// <summary>Needs at least one selected object; the binding reads <see cref="CommandContext.Primary"/>.</summary>
    SelectedObject = 1 << 0,

    /// <summary>
    /// Accepts more than one selected object; the binding reads the whole
    /// <see cref="CommandContext.Selection"/>. Without this, a command is
    /// unavailable while several objects are selected rather than silently
    /// acting on only the first one.
    /// </summary>
    MultipleAllowed = 1 << 1,
}
