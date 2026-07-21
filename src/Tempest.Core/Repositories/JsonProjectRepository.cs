using System.Text.Json;
using Tempest.Core.Models;

namespace Tempest.Core.Repositories;

public class JsonProjectRepository : IProjectRepository
{
    public void Save(ProjectModel project, string projectFolder)
    {
        var file = Path.Combine(projectFolder, "project.json");

        File.WriteAllText(
            file,
            JsonSerializer.Serialize(
                project,
                new JsonSerializerOptions
                {
                    WriteIndented = true
                }));
    }

    public ProjectModel? Load(string projectFolder)
    {
        var file = Path.Combine(projectFolder, "project.json");

        if (!File.Exists(file))
            return null;

        return JsonSerializer.Deserialize<ProjectModel>(
            File.ReadAllText(file));
    }

    public bool Exists(string projectFolder)
    {
        return File.Exists(
            Path.Combine(projectFolder, "project.json"));
    }

    public IEnumerable<ProjectModel> List(string projectsRoot)
    {
        foreach (var directory in Directory.GetDirectories(projectsRoot))
        {
            var project = Load(directory);

            if (project != null)
                yield return project;
        }
    }
}
