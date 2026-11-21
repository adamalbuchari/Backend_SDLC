
namespace DocuFlow.Api.Models;

public class DocumentWorkflowAssignment
{
    public Guid DocumentWorkflowAssignmentId { get; set; }

    public Guid DocumentWorkflowId { get; set; }
    public DocumentWorkflow? DocumentWorkflow { get; set; }

    public Guid ApprovalWorkflowStageId { get; set; }
    public ApprovalWorkflowStage? ApprovalWorkflowStage { get; set; }

    public Guid AssignedUserId { get; set; }
    public User? AssignedUser { get; set; }

    // Reviewer atau Approver
    public string AssignmentType { get; set; } = "Reviewer";

    // Pending, In Progress, Completed, Cancelled
    public string AssignmentStatus { get; set; } = "Pending";

    public DateTime? AssignedAt { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime? CompletedAt { get; set; }
}
