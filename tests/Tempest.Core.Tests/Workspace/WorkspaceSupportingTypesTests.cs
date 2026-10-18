using Tempest.Workspace;

namespace Tempest.Core.Tests.Workspace;

// Pure, Host-free unit tests for the small supporting types (exceptions,
// records, the ambient context holder) — none has an external dependency.
public class WorkspaceSupportingTypesTests
{
    [Fact]
    public void DuplicateWorkspaceRegistrationException_CarriesKind()
    {
        var exception = new DuplicateWorkspaceRegistrationException("Requirement");

        Assert.Equal("Requirement", exception.Kind);
        Assert.Contains("Requirement", exception.Message);
    }

    [Fact]
    public void WorkspaceViewFactoryNotFoundException_CarriesKind()
    {
        var exception = new WorkspaceViewFactoryNotFoundException("Material");

        Assert.Equal("Material", exception.Kind);
        Assert.Contains("Material", exception.Message);
    }

    [Fact]
    public void DuplicateWorkspaceRegistrationException_IsAWorkspaceException() =>
        Assert.IsAssignableFrom<WorkspaceException>(new DuplicateWorkspaceRegistrationException("X"));

    [Fact]
    public void WorkspaceViewFactoryNotFoundException_IsAWorkspaceException() =>
        Assert.IsAssignableFrom<WorkspaceException>(new WorkspaceViewFactoryNotFoundException("X"));

    [Fact]
    public void WorkspaceSelection_RecordEquality_SameValuesAreEqual()
    {
        var id = Guid.NewGuid();

        Assert.Equal(new WorkspaceSelection(id, "Requirement"), new WorkspaceSelection(id, "Requirement"));
    }

    [Fact]
    public void WorkspacePanelPlacement_RecordEquality_SameValuesAreEqual()
    {
        var id = Guid.NewGuid();

        Assert.Equal(
            new WorkspacePanelPlacement(id, WorkspaceDockPosition.Left, 30, true),
            new WorkspacePanelPlacement(id, WorkspaceDockPosition.Left, 30, true));
    }

    [Fact]
    public void WorkspaceSelectionChangedEvent_ExposesPreviousAndCurrent()
    {
        var previous = new WorkspaceSelection(Guid.NewGuid(), "Requirement");
        var current = new WorkspaceSelection(Guid.NewGuid(), "Material");

        var @event = new WorkspaceSelectionChangedEvent(previous, current);

        Assert.Same(previous, @event.Previous);
        Assert.Same(current, @event.Current);
    }

    [Fact]
    public void WorkspaceContext_DefaultsToNoSelectionAndNoActiveView()
    {
        var context = new WorkspaceContext();

        Assert.Null(context.CurrentSelection);
        Assert.Null(context.ActiveViewId);
    }

    [Fact]
    public void WorkspaceContext_InternalSetters_UpdateTheExposedProperties()
    {
        var context = new WorkspaceContext();
        var selection = new WorkspaceSelection(Guid.NewGuid(), "Requirement");
        var viewId = Guid.NewGuid();

        context.CurrentSelection = selection;
        context.ActiveViewId = viewId;

        Assert.Equal(selection, context.CurrentSelection);
        Assert.Equal(viewId, context.ActiveViewId);
    }

    [Fact]
    public void WorkspaceStatusBar_DefaultsToReady() =>
        Assert.Equal("Ready.", new WorkspaceStatusBar().StatusText);

    [Fact]
    public void WorkspaceStatusBar_SetStatus_UpdatesText()
    {
        var statusBar = new WorkspaceStatusBar();

        statusBar.SetStatus("Viewing: Home");

        Assert.Equal("Viewing: Home", statusBar.StatusText);
    }

    [Fact]
    public async Task WorkspaceStatusBar_HandleAsync_WithSelection_ShowsSelectionText()
    {
        var statusBar = new WorkspaceStatusBar();
        var selection = new WorkspaceSelection(Guid.NewGuid(), "Requirement");

        await statusBar.HandleAsync(new WorkspaceSelectionChangedEvent(null, selection), CancellationToken.None);

        Assert.Contains("Requirement", statusBar.StatusText);
        Assert.Contains(selection.ObjectId.ToString(), statusBar.StatusText);
    }

    /// <summary>`TD-186`: the operator reads a name on the Selected segment, never a GUID, once a resolver is wired.</summary>
    [Fact]
    public async Task WorkspaceStatusBar_HandleAsync_WithResolver_ShowsTheObjectsNameNotItsId()
    {
        var statusBar = new WorkspaceStatusBar();
        var selection = new WorkspaceSelection(Guid.NewGuid(), "Calculation");
        statusBar.UseDisplayNameResolver((id, _) => Task.FromResult<string?>(id == selection.ObjectId ? "Bracket check B-01" : null));

        await statusBar.HandleAsync(new WorkspaceSelectionChangedEvent(null, selection), CancellationToken.None);

        Assert.Equal("Selected: Calculation Bracket check B-01", statusBar.StatusText);
        Assert.DoesNotContain(selection.ObjectId.ToString(), statusBar.StatusText);
    }

    [Fact]
    public async Task WorkspaceStatusBar_HandleAsync_ResolverFindsNothingOrThrows_FallsBackToTheId()
    {
        var statusBar = new WorkspaceStatusBar();
        var unknown = new WorkspaceSelection(Guid.NewGuid(), "Requirement");
        statusBar.UseDisplayNameResolver((_, _) => Task.FromResult<string?>(null));
        await statusBar.HandleAsync(new WorkspaceSelectionChangedEvent(null, unknown), CancellationToken.None);
        Assert.Equal($"Selected: Requirement {unknown.ObjectId}", statusBar.StatusText);

        statusBar.UseDisplayNameResolver((_, _) => throw new InvalidOperationException("store offline"));
        await statusBar.HandleAsync(new WorkspaceSelectionChangedEvent(unknown, unknown), CancellationToken.None);
        Assert.Equal($"Selected: Requirement {unknown.ObjectId}", statusBar.StatusText);
    }

    [Fact]
    public async Task WorkspaceStatusBar_HandleAsync_SelectionCleared_ResetsToReady()
    {
        var statusBar = new WorkspaceStatusBar();
        await statusBar.HandleAsync(new WorkspaceSelectionChangedEvent(null, new WorkspaceSelection(Guid.NewGuid(), "Requirement")), CancellationToken.None);

        await statusBar.HandleAsync(new WorkspaceSelectionChangedEvent(new WorkspaceSelection(Guid.NewGuid(), "Requirement"), null), CancellationToken.None);

        Assert.Equal("Ready.", statusBar.StatusText);
    }
}
