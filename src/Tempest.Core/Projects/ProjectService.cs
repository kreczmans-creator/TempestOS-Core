using Tempest.Core.Models;
using Tempest.Core.Repositories;

namespace Tempest.Core.Projects;

public class ProjectService
{
    private readonly IProjectRepository _repository;
    private readonly ProjectNumberGenerator _numberGenerator;

    public ProjectService()
    {
        _repository = new JsonProjectRepository();
        _numberGenerator = new ProjectNumberGenerator();
    }

    public ProjectModel CreateProject(
        string projectsRoot,
        string projectName)
    {
        var projectId = _numberGenerator.GetNextProjectId(projectsRoot);

        var project = new ProjectModel
        {
            ProjectId = projectId,
            Name = projectName,
            Owner = Environment.UserName,
            Customer = ""
        };

        var projectFolder = Path.Combine(projectsRoot, projectId);

        Directory.CreateDirectory(projectFolder);

        string[] folders =
        {
            "00_Project",
            "01_Requirements",
            "02_Inputs",
            "03_CAD",
            "04_Analysis",
            "05_Verification",
            "06_Manufacturing",
            "07_Deliverables",
            "08_Reviews",
            "09_Archive"
        };

        foreach (var folder in folders)
        {
            Directory.CreateDirectory(
                Path.Combine(projectFolder, folder));
        }

        _repository.Save(project, projectFolder);

        return project;
    }

    public IEnumerable<ProjectModel> GetProjects(string projectsRoot)
    {
        return _repository.List(projectsRoot);
    }
}