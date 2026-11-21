
namespace DocuFlow.Api.Models;

public class DocumentWorkflowHistory
{
    public Guid DocumentWorkflowHistoryId { get; set; }

    public Guid DocumentWorkflowId { get; set; }
    public DocumentWorkflow? DocumentWorkflow { get; set; }

    public Guid? ApprovalWorkflowStageId { get; set; }
    public ApprovalWorkflowStage? ApprovalWorkflowStage { get; set; }

    public Guid ActorUserId { get; set; }
    public User? ActorUser { get; set; }

    public string Action { get; set; } = string.Empty;
    public string? PreviousStatus { get; set; }
    public string? NewStatus { get; set; }
    public string? Comment { get; set; }

    public DateTime ActionAt { get; set; }
}
