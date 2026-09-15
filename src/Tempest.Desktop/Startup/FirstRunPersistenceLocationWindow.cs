using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Tempest.Desktop.Theming;

namespace Tempest.Desktop.Startup;

/// <summary>
/// The installed application's own first-run dialog (`WP 21.5A`, `WP RC.0A`
/// scope item 2): shown once, before <see cref="WorkspaceHost"/> — and
/// therefore before any persistence — exists, so it is a real
/// <see cref="Window"/> rather than one of the in-shell overlay dialogs
/// (<c>MessageDialog</c>, <c>ConfirmationDialog</c>) every other dialog in
/// this project is, all of which need a shell already on screen to sit
/// over. States the default location, offers <b>Change…</b> (a real folder
/// picker) and <b>Continue</b> — no Cancel: the operator is choosing where
/// their data will live, not deciding whether it will exist at all, so
/// there is nothing a Cancel could sensibly mean here.
/// </summary>
public sealed class FirstRunPersistenceLocationWindow : Window
{
    private readonly TextBox _pathBox;

    /// <summary>Initialises a new instance of the <see cref="FirstRunPersistenceLocationWindow"/> class.</summary>
    /// <param name="suggestedDefault">The installed default persistence root, shown pre-filled.</param>
    public FirstRunPersistenceLocationWindow(string suggestedDefault)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(suggestedDefault);

        Title = "TempestOS — Where should your data live?";
        Width = 560;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        ShowInTaskbar = true;

        var heading = new TextBlock
        {
            Text = "Where should your data live?",
            FontFamily = DesignTokens.TitleFont,
            FontSize = DesignTokens.FontSizeTitle,
            FontWeight = DesignTokens.WeightHeading,
        };

        var lead = new TextBlock
        {
            Text = "TempestOS keeps every project, drawing and calculation in one file, below. "
                 + "This is asked once — you can change it later from Settings → Data.",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Avalonia.Thickness(0, DesignTokens.SpaceSm, 0, 0),
        };

        _pathBox = new TextBox
        {
            Text = suggestedDefault,
            IsReadOnly = true,
            MinHeight = DesignTokens.ControlSizeMedium,
            Margin = new Avalonia.Thickness(0, DesignTokens.SpaceLg, 0, 0),
        };
        AutomationProperties.SetName(_pathBox, "Persistence data location");

        var changeButton = new Button { Content = "Change…", MinHeight = DesignTokens.ControlSizeMedium };
        AutomationProperties.SetName(changeButton, "Change data location");
        changeButton.Click += async (_, _) => await OnChangeAsync().ConfigureAwait(true);

        var continueButton = new Button
        {
            Content = "Continue",
            MinHeight = DesignTokens.ControlSizeMedium,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        continueButton.Classes.Add(ChromeStyles.Primary);
        AutomationProperties.SetName(continueButton, "Continue");
        continueButton.Click += (_, _) => Close(_pathBox.Text ?? suggestedDefault);

        var buttonRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = DesignTokens.SpaceMd,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Avalonia.Thickness(0, DesignTokens.SpaceXl, 0, 0),
        };
        buttonRow.Children.Add(changeButton);
        buttonRow.Children.Add(continueButton);

        var body = new StackPanel { Margin = DesignTokens.DialogPadding };
        body.Children.Add(heading);
        body.Children.Add(lead);
        body.Children.Add(_pathBox);
        body.Children.Add(buttonRow);

        Content = body;
    }

    private async Task OnChangeAsync()
    {
        if (StorageProvider is not { } storageProvider)
            return;

        var folders = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Choose where TempestOS keeps your data",
            AllowMultiple = false,
        }).ConfigureAwait(true);

        if (folders is [{ } chosen, ..] && chosen.TryGetLocalPath() is { } localPath)
            _pathBox.Text = localPath;
    }
}
