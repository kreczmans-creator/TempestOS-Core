using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Tempest.Desktop.Views;

namespace Tempest.Desktop.Tests;

/// <summary>
/// PO request: a command parameter with a fixed option list (Create
/// Document's Kind, every <c>Choice</c>/<c>EnumChoice</c>) is a dropdown,
/// never a typed field.
/// </summary>
public sealed class InputDialogChoiceTests
{
    [AvaloniaFact]
    public async Task Choices_ShowADropdown_PreselectTheDefault_AndReturnThePick()
    {
        var dialog = new InputDialog();
        var window = new Window { Content = dialog };
        window.Show();

        var pending = dialog.PromptAsync("Create Document", "Kind:", initialValue: "drawing", choices: ["Document", "Drawing", "CADModel"]);

        var combo = dialog.GetLogicalDescendants().OfType<ComboBox>().Single();
        var text = dialog.GetLogicalDescendants().OfType<TextBox>().Single();
        Assert.True(combo.IsVisible);
        Assert.False(text.IsVisible);
        Assert.Equal("Drawing", combo.SelectedItem);

        combo.SelectedItem = "CADModel";
        dialog.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "OK")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.Equal("CADModel", await pending);

        // A free-text prompt afterwards is a text box again.
        var next = dialog.PromptAsync("Rename", "Name:", initialValue: "x");
        Assert.True(text.IsVisible);
        Assert.False(combo.IsVisible);
        dialog.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "OK")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.Equal("x", await next);
    }
}
