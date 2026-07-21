namespace Tempest.Core.Models;

public class ProjectModel
{
    public string ProjectId { get; set; } = "";

    public string Name { get; set; } = "";

    public string Customer { get; set; } = "";

    public string ContractNumber { get; set; } = "";

    public string Owner { get; set; } = "";

    public string Status { get; set; } = "Active";

    public string Classification { get; set; } = "Internal";

    public string SecurityLevel { get; set; } = "BPSS";

    public bool ExportControlled { get; set; }

    public DateTime Created { get; set; } = DateTime.Now;

    public DateTime LastModified { get; set; } = DateTime.Now;

    public string Version { get; set; } = "0.1";

    public int RequirementCount { get; set; }

    public int CalculationCount { get; set; }

    public int DocumentCount { get; set; }

    public int RiskCount { get; set; }

    public int ActionCount { get; set; }
}