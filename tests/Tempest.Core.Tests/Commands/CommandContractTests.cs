using Tempest.Core.Commands;

namespace Tempest.Core.Tests.Commands;

public class CommandContractTests
{
    [Fact]
    public void ICommand_ConcreteCommand_CarriesItsOwnParametersAsData()
    {
        var command = new SaveProjectCommand("tempest.sample");

        Assert.IsAssignableFrom<ICommand>(command);
        Assert.Equal("tempest.sample", command.ProjectId);
    }

    [Fact]
    public void ICommand_DistinctCommandTypes_AreIndependentOfEachOther()
    {
        var save = new SaveProjectCommand("tempest.sample");
        var open = new OpenModuleCommand("tempest.module.alpha");

        Assert.IsAssignableFrom<ICommand>(save);
        Assert.IsAssignableFrom<ICommand>(open);
        Assert.NotEqual(save.GetType(), open.GetType());
    }

    private sealed class SaveProjectCommand : ICommand
    {
        public SaveProjectCommand(string projectId)
        {
            ProjectId = projectId;
        }

        public string ProjectId { get; }
    }

    private sealed class OpenModuleCommand : ICommand
    {
        public OpenModuleCommand(string moduleId)
        {
            ModuleId = moduleId;
        }

        public string ModuleId { get; }
    }
}
