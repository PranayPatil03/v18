namespace CRISP.Domain.Entities;

public class ValidationDefinition
{
    public int Id { get; set; }
    public int WorkflowMasterId { get; set; }
    public string ValidationName { get; set; } = "";
    public string Expression { get; set; } = "true";
    public string ErrorMessage { get; set; } = "Validation failed.";
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public WorkflowMaster WorkflowMaster { get; set; } = null!;
}
