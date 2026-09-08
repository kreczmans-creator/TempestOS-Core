using Tempest.Core.Commands;
using Tempest.Workspace;
using Tempest.Workspace.Mechanical;

namespace Tempest.Core.Tests.Workspace;

/// <summary>
/// `WP 17.9.2`: where a new Mechanical object goes when the user did not
/// say. The first Windows review of `v0.17.0` created a Part with a project
/// open and could not find it, because nothing chose a parent.
/// </summary>
public class MechanicalCreateParentPolicyTests
{
    private static readonly Guid ProjectId = Guid.NewGuid();
    private static readonly Guid AssemblyId = Guid.NewGuid();
    private static readonly Guid PartId = Guid.NewGuid();

    [Theory]
    [InlineData("Part")]
    [InlineData("Assembly")]
    [InlineData("SubAssembly")]
    [InlineData("Component")]
    [InlineData("Configuration")]
    public void NothingSelected_ProjectOpen_GoesUnderTheProject(string kind)
    {
        var context = new CommandContext([], ProjectId);

        Assert.Equal(ProjectId, MechanicalCreateParentPolicy.Resolve(kind, context));
    }

    [Fact]
    public void NothingSelected_NoProject_HasNoParent_WhichIsWhatStandaloneEngineeringMeans()
    {
        Assert.Null(MechanicalCreateParentPolicy.Resolve("Part", CommandContext.Empty));
    }

    [Theory]
    [InlineData("Project")]
    [InlineData("Assembly")]
    [InlineData("SubAssembly")]
    public void AContainerSelected_GoesUnderIt_EvenWithAProjectOpen(string selectedKind)
    {
        var context = new CommandContext([new CommandContextObject(AssemblyId, selectedKind)], ProjectId);

        Assert.Equal(AssemblyId, MechanicalCreateParentPolicy.Resolve("Part", context));
    }

    [Theory]
    [InlineData("Part")]
    [InlineData("Component")]
    [InlineData("Requirement")]
    public void ANonContainerSelected_FallsBackToTheProject(string selectedKind)
    {
        var context = new CommandContext([new CommandContextObject(PartId, selectedKind)], ProjectId);

        Assert.Equal(ProjectId, MechanicalCreateParentPolicy.Resolve("Part", context));
    }

    [Fact]
    public void ANewProject_IsAlwaysARoot()
    {
        var context = new CommandContext([new CommandContextObject(AssemblyId, "Assembly")], ProjectId);

        Assert.Null(MechanicalCreateParentPolicy.Resolve("Project", context));
    }

    [Fact]
    public void TheWorkspaceAdapter_CarriesTheOpenProject_WithAndWithoutASelection()
    {
        var empty = WorkspaceCommandContext.From(current: null, selectedItems: [], ProjectId);
        Assert.Equal(ProjectId, empty.ProjectId);
        Assert.Empty(empty.Selection);

        var selection = new WorkspaceSelection(AssemblyId, "Assembly");
        var withSelection = WorkspaceCommandContext.From(selection, [selection], ProjectId);
        Assert.Equal(ProjectId, withSelection.ProjectId);
        Assert.Equal(AssemblyId, withSelection.Primary!.ObjectId);

        // The older overloads still mean "no project": nothing that called
        // them before carries one now by accident.
        Assert.Null(WorkspaceCommandContext.From(selection, [selection]).ProjectId);
        Assert.Null(CommandContext.Empty.ProjectId);
    }
}
