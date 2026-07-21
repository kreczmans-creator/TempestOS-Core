namespace Tempest.Core.Projects;

public class ProjectNumberGenerator
{
    public string GetNextProjectId(string projectsRoot)
    {
        Directory.CreateDirectory(projectsRoot);

        var existingIds = Directory.GetDirectories(projectsRoot)
            .Select(Path.GetFileName)
            .Where(name => name != null && name.StartsWith("TMP-"))
            .ToList();

        int highest = 0;

        foreach (var id in existingIds)
        {
            var parts = id!.Split('-');

            if (parts.Length != 3)
                continue;

            if (int.TryParse(parts[2], out int number))
            {
                highest = Math.Max(highest, number);
            }
        }

        var year = DateTime.Now.Year;

        return $"TMP-{year}-{highest + 1:0000}";
    }
}
