using Tempest.App.Workspace;

namespace Tempest.Desktop.Composition;

/// <summary>
/// The two Desktop product decisions that route a command somewhere other
/// than its own <c>CommandBinding</c> — TD-77 Stage 5.
/// </summary>
/// <remarks>
/// <para>
/// <b>Explicit Ids, never a parsed suffix.</b> Both sets below are written
/// out. The Ribbon used to decide what a command was by reading the text
/// after the last dot in its Id, which silently mis-classified every Id
/// the parser had not anticipated — <c>requirements.delete-group</c> and
/// <c>requirements.revise</c> were both unreachable for exactly that
/// reason. A product decision that applies to eleven named commands is
/// written as eleven names.
/// </para>
/// <para>
/// Everything not named here goes through
/// <c>ICommandRegistry.InvokeAsync(id, context, prompt, ct)</c> like every
/// other command. These two sets are the whole of the exception.
/// </para>
/// </remarks>
internal static class SurfaceCommandPolicy
{
    /// <summary>
    /// The commands whose Ribbon button opens the Object Editor instead of
    /// invoking the command — <c>ADR-0096</c>/<c>ADR-0097</c>, kept
    /// deliberately.
    /// </summary>
    /// <remarks>
    /// Renaming or revising needs text, and the Object Editor tab is the
    /// real surface that collects it — with the object's own content in
    /// front of the user, not a one-line box floating over a ribbon. These
    /// commands still carry real bindings (Stage 3) and are still invocable
    /// by Id from the Command Palette and from automation; this set says
    /// only that <i>this</i> button opens the editor. Requirements has no
    /// rename of its own: a Requirement's editable field is its Statement,
    /// which is what <c>requirements.revise</c> is.
    /// </remarks>
    internal static readonly IReadOnlySet<string> ObjectEditorCommandIds = new HashSet<string>(StringComparer.Ordinal)
    {
        "calculations.rename", "calculations.edit",
        "documents.rename", "documents.edit",
        "manufacturing.rename", "manufacturing.edit",
        "mechanical.rename", "mechanical.edit",
        "verification.rename", "verification.edit",
        "requirements.revise",
    };

    /// <summary>
    /// The commands that delete an object, and must therefore dispatch
    /// through <see cref="IWorkspaceManager.DeleteObjectAsync"/> rather
    /// than through their own binding.
    /// </summary>
    /// <remarks>
    /// Not a stylistic preference: that method is where a successful
    /// delete clears the selection (<c>TD-58</c>), and it is the one place
    /// every deleting surface converges so a deleted object cannot stay
    /// selected, stay in the Property Inspector, or be deleted twice. A
    /// binding dispatched straight to its handler would delete the object
    /// correctly and leave the shell pointing at it, which is the exact
    /// stale-UI defect TD-58 closed. The binding still owns <i>what to
    /// say</i> before deleting — its own ConfirmationMessage — so Core
    /// remains the single source of the confirmation text.
    /// </remarks>
    internal static readonly IReadOnlySet<string> DeleteCommandIds = new HashSet<string>(StringComparer.Ordinal)
    {
        "calculations.delete",
        "documents.delete",
        "manufacturing.delete",
        "mechanical.delete",
        "verification.delete",
        "requirements.delete",
        "requirements.delete-group",
        "requirements.delete-collection",
    };
}
