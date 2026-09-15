using Avalonia.Controls;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Events;
using Tempest.Desktop.Theming;

namespace Tempest.Desktop.Editors.Sections;

/// <summary>
/// The Validation summary (`WP 10.3A`) — a real, live
/// <see cref="IValidatable.ValidateAsync"/> read, genuinely closing the gap
/// <see cref="Tempest.Desktop.Views.PropertyInspectorView"/>'s own
/// disclosed placeholder names. Informational only, moved verbatim from
/// <see cref="ObjectEditorView"/>'s own former <c>PopulateValidationAsync</c>
/// (`WP 21.1B`).
/// </summary>
/// <remarks>
/// Always visible, for every Kind, including the Requirement-only
/// population path — a Requirement implements no <see cref="IValidatable"/>
/// (genuinely true, not merely untested), so <paramref name="subject"/>
/// being <see langword="null"/> there produces the identical honest
/// "supports no validation" fallback this class already shows for any real
/// object that fails the same type-check; the pre-split shell hand-
/// duplicated that one fallback line directly into its own
/// <c>PopulateFromRequirementAsync</c> rather than calling this method with
/// no target — this class needs no equivalent duplication, since calling
/// <see cref="LoadAsync"/> with <see langword="null"/> already produces the
/// same output.
/// </remarks>
internal sealed class ValidationSection : IEditorSection
{
    private readonly StackPanel _panel = new() { Spacing = DesignTokens.SpaceXs };
    private EditorSectionContext _ctx = null!;

    public string Title => "Validation";

    public bool AppliesTo(IEngineeringObject? subject) => true;

    public Control Build(EditorSectionContext ctx)
    {
        _ctx = ctx;
        return EditorSectionHelpers.BuildSection(Title, _panel);
    }

    public async Task LoadAsync(IEngineeringObject? subject, CancellationToken ct)
    {
        _panel.Children.Clear();

        if (subject is not IValidatable validatable)
        {
            _panel.Children.Add(new TextBlock { Text = "This object supports no validation.", Opacity = 0.7 });
            return;
        }

        var result = await validatable.ValidateAsync().ConfigureAwait(true);

        if (result.IsValid && result.Warnings.Count == 0)
        {
            _panel.Children.Add(ObjectEditorView.BuildSeverityRow(Theming.FeedbackSeverity.Success, "No issues found."));
            return;
        }

        foreach (var error in result.Errors)
            _panel.Children.Add(ObjectEditorView.BuildSeverityRow(Theming.FeedbackSeverity.Error, error.Message));

        foreach (var warning in result.Warnings)
            _panel.Children.Add(ObjectEditorView.BuildSeverityRow(Theming.FeedbackSeverity.Warning, warning.Message));
    }

    public void React(WorkspaceChange change)
    {
        // Reserved — see IEditorSection's own remarks.
    }
}
