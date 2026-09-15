using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using System.Text;

namespace Tempest.Desktop.Tests.Editors;

/// <summary>
/// `WP 21.1B` scope item 4 — the "proof of identity" the god-object split
/// leans on: a deterministic, ordered rendering of one built
/// <c>ObjectEditorView</c>'s own logical tree (every real <see cref="Control"/>,
/// depth-first, document order), naming each node's own runtime type, its
/// <see cref="AutomationProperties.NameProperty"/> (or <c>(unnamed)</c>)
/// and whether it is currently visible. Two editors — one built before the
/// shell/section split, one after — produce byte-identical output from this
/// walker if and only if the split changed no control's type, order,
/// automation name or visibility for that Kind.
/// </summary>
/// <remarks>
/// Deliberately never renders a control's own <c>Text</c>/<c>Content</c> —
/// only its type, automation name and visibility. Doing so keeps the walk
/// stable across runs that create fixture objects with fresh
/// <see cref="Guid"/>s (an identity readout's own "Id: …" text, an
/// attachment row's own byte count) while still catching the one class of
/// regression this Work Package's brief actually cares about: a section
/// whose own controls, order, accessible names or default visibility moved
/// during the split. Every fixture used against this walker is built with
/// fixed, deterministic display names precisely so nothing else leaks into
/// an automation name either (e.g. a relationship row's own "Open {name}").
/// </remarks>
internal static class EditorAutomationTreeWalker
{
    /// <summary>Walks <paramref name="root"/> (included) and every logical descendant, depth-first, producing one line per control.</summary>
    public static string Walk(Control root)
    {
        ArgumentNullException.ThrowIfNull(root);

        var builder = new StringBuilder();
        Visit(root, 0, builder);
        return builder.ToString();
    }

    private static void Visit(Control control, int depth, StringBuilder builder)
    {
        var name = AutomationProperties.GetName(control);
        var displayName = string.IsNullOrWhiteSpace(name) ? "(unnamed)" : name;

        builder
            .Append(' ', depth * 2)
            .Append(control.GetType().Name)
            .Append(" — ")
            .Append(displayName)
            .Append(" — visible=")
            .Append(control.IsVisible)
            .Append('\n');

        foreach (var child in control.GetLogicalChildren().OfType<Control>())
            Visit(child, depth + 1, builder);
    }
}
