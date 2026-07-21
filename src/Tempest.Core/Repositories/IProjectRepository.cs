using Tempest.Core.Models;

namespace Tempest.Core.Repositories;

public interface IProjectRepository
{
    void Save(ProjectModel project, string projectFolder);

    ProjectModel? Load(string projectFolder);

    bool Exists(string projectFolder);

    IEnumerable<ProjectModel> List(string projectsRoot);
}