using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
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

    /// <summary>
    /// v0.23.0 board B5: <c>ComboBox.OnKeyDown</c> handles Enter itself
    /// (it opens the dropdown and marks the event handled), so an
    /// instance <c>KeyDown +=</c> handler never ran and Enter could not
    /// confirm any Choice prompt. Real keyboard input through the headless
    /// window, not a synthetic event raised on the dialog.
    /// </summary>
    [AvaloniaFact]
    public async Task Choices_EnterWithTheDropdownClosed_ConfirmsThePick()
    {
        var dialog = new InputDialog();
        var window = new Window { Content = dialog };
        try
        {
            window.Show();
            var pending = dialog.PromptAsync("Create Document", "Kind:", initialValue: "Drawing", choices: ["Document", "Drawing", "CADModel"]);
            Dispatcher.UIThread.RunJobs();

            var combo = dialog.GetLogicalDescendants().OfType<ComboBox>().Single();
            Assert.True(combo.IsFocused);
            Assert.False(combo.IsDropDownOpen);

            window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
            Dispatcher.UIThread.RunJobs();

            Assert.True(pending.IsCompleted, "Enter on a closed Choice dropdown did not confirm the prompt.");
            Assert.Equal("Drawing", await pending);
            Assert.False(combo.IsDropDownOpen);
            Assert.False(dialog.IsVisible);
        }
        finally
        {
            window.Close();
            Dispatcher.UIThread.RunJobs();
        }
    }

    /// <summary>B5: Escape with the dropdown closed cancels the Choice prompt.</summary>
    [AvaloniaFact]
    public async Task Choices_EscapeWithTheDropdownClosed_Cancels()
    {
        var dialog = new InputDialog();
        var window = new Window { Content = dialog };
        try
        {
            window.Show();
            var pending = dialog.PromptAsync("Create Document", "Kind:", initialValue: "Drawing", choices: ["Document", "Drawing"]);
            Dispatcher.UIThread.RunJobs();

            window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
            Dispatcher.UIThread.RunJobs();

            Assert.True(pending.IsCompleted);
            Assert.Null(await pending);
        }
        finally
        {
            window.Close();
            Dispatcher.UIThread.RunJobs();
        }
    }
}
