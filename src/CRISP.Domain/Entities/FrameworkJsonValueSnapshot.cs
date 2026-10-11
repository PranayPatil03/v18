namespace CRISP.Domain.Entities;

public class FrameworkJsonValueSnapshot
{
    public int Id { get; set; }
    public int FrameworkInstanceId { get; set; }
    public int WorkflowVersionNo { get; set; }
    public string JsonContent { get; set; } = "";
    public DateTime CreatedOn { get; set; }
    public bool IsCurrent { get; set; }

    public FrameworkInstance FrameworkInstance { get; set; } = null!;
}
