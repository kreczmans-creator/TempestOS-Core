using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Tempest.Desktop.Theming;

namespace Tempest.Desktop.CalcSheets;

/// <summary>
/// Addendum C, C-6: the section of Settings → System that holds the two menu actions "Install demo project" and "Remove demo project" and says in a sentence
/// what is installed. All behaviour is in <see cref="DemoProjectViewModel"/>; this class only draws it. A button asks the person in the shell's confirmation
/// dialog before anything is made or removed.
/// </summary>
public sealed class DemoProjectPanel : StackPanel
{
    public const string PanelName = "Demo project";

    private readonly DemoProjectViewModel _model;
    private readonly TextBlock _about = new() { FontSize = DesignTokens.FontSizeBody, TextWrapping = Avalonia.Media.TextWrapping.Wrap, MaxWidth = 720, Text = DemoProjectViewModel.About };
    private readonly Button _install = new() { Content = DemoProjectViewModel.InstallCaption, MinHeight = DesignTokens.ControlSizeMedium };
    private readonly Button _remove = new() { Content = DemoProjectViewModel.RemoveCaption, MinHeight = DesignTokens.ControlSizeMedium };
    private readonly TextBlock _status = new() { FontSize = DesignTokens.FontSizeBody, TextWrapping = Avalonia.Media.TextWrapping.Wrap, MaxWidth = 720, Opacity = 0.9 };

    public DemoProjectPanel(DemoProjectViewModel model)
    {
        _model = model ?? throw new ArgumentNullException(nameof(model));
        Spacing = DesignTokens.SpaceSm;

        AutomationProperties.SetName(this, PanelName);
        AutomationProperties.SetName(_install, DemoProjectViewModel.InstallCaption);
        AutomationProperties.SetName(_remove, DemoProjectViewModel.RemoveCaption);
        AutomationProperties.SetName(_status, "Demo project status");
        AutomationProperties.SetLiveSetting(_status, AutomationLiveSetting.Polite);
        ToolTip.SetTip(_install, "Adds the invented demo project to this database, after you confirm. Nothing is installed unless you ask.");
        ToolTip.SetTip(_remove, "Removes the demo project and everything in it, after you confirm. No other project is touched.");
        _remove.Classes.Add(ChromeStyles.Subtle);
        _install.Classes.Add(ChromeStyles.Subtle);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceSm };
        buttons.Children.Add(_install);
        buttons.Children.Add(_remove);

        Children.Add(_about);
        Children.Add(buttons);
        Children.Add(_status);

        _install.Click += async (_, _) => await GuardedAsync(() => _model.InstallAsync()).ConfigureAwait(true);
        _remove.Click += async (_, _) => await GuardedAsync(() => _model.RemoveAsync()).ConfigureAwait(true);
        _model.Changed += Render;
        Render();
    }

    public DemoProjectViewModel Model => _model;

    private async Task GuardedAsync(Func<Task> action)
    {
        try
        {
            await action().ConfigureAwait(true);
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            _status.Text = $"The demo project act failed: {failure.Message}";
        }
    }

    private void Render()
    {
        _install.IsEnabled = _model.CanInstall;
        _remove.IsEnabled = _model.CanRemove;
        _status.Text = _model.Status;
    }
}
