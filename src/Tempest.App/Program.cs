using Tempest.Core.Bootstrap;
using Tempest.Core.Configuration;
using Tempest.Core.Hosting;
using Tempest.Core.Projects;

Console.Title = "TempestOS";

var bootstrap = new BootstrapService();
var configuration = bootstrap.Initialise();

var hostingService = new HostingService();
hostingService.Initialise(configuration);

var projectService = new ProjectService();

Console.WriteLine("====================================");
Console.WriteLine(" TempestOS");
Console.WriteLine("====================================");
Console.WriteLine();

Console.WriteLine($"Workspace : {configuration.WorkspaceRoot}");
Console.WriteLine($"Projects  : {configuration.ProjectsPath}");
Console.WriteLine();

while (true)
{
    Console.WriteLine();
    Console.WriteLine("1 - Create Project");
    Console.WriteLine("2 - List Projects");
    Console.WriteLine("0 - Exit");
    Console.Write("> ");

    var input = Console.ReadLine();

    switch (input)
    {
        case "1":
            {
                Console.Write("Project Name: ");
                var projectName = Console.ReadLine() ?? "New Project";

                var project = projectService.CreateProject(
                    configuration.ProjectsPath,
                    projectName);

                Console.WriteLine();
                Console.WriteLine($"Project created: {project.ProjectId}");
                break;
            }

        case "2":
            {
                Console.WriteLine();

                var projects = projectService.GetProjects(
                    configuration.ProjectsPath);

                foreach (var project in projects)
                {
                    Console.WriteLine($"{project.ProjectId} - {project.Name}");
                }

                break;
            }

        case "0":
            return;

        default:
            Console.WriteLine("Invalid option.");
            break;
    }
}