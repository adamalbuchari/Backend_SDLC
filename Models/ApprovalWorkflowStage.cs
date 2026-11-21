
namespace DocuFlow.Api.Models;

public class ApprovalWorkflowStage
{
    public Guid ApprovalWorkflowStageId { get; set; }

    public Guid ApprovalWorkflowId { get; set; }
    public ApprovalWorkflow? ApprovalWorkflow { get; set; }

    public string StageName { get; set; } = string.Empty;
    public string StageType { get; set; } = "Review";

    public int SequenceNo { get; set; }

    // Sequential atau Parallel
    public string ApprovalMode { get; set; } = "Sequential";

    // All atau Any
    public string ApprovalRule { get; set; } = "All";

    public bool IsRequired { get; set; } = true;
}
