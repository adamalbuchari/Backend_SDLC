namespace DocuFlow.Api.DTOs.ReviewApproval;

public class SubmitDocumentRequest
{
    public Guid DocumentId { get; set; }
    public Guid ApprovalWorkflowId { get; set; }
    public DateTime? DueDate { get; set; }
    public string? Comment { get; set; }

    public List<WorkflowAssignmentRequest> Assignments { get; set; } = new();
}

public class WorkflowAssignmentRequest
{
    public Guid ApprovalWorkflowStageId { get; set; }
    public Guid AssignedUserId { get; set; }
    public string AssignmentType { get; set; } = "Reviewer";
    public DateTime? DueDate { get; set; }
}