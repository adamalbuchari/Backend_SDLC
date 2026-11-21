
namespace DocuFlow.Api.Models;

public class DocumentWorkflow
{
    public Guid DocumentWorkflowId { get; set; }

    public Guid DocumentId { get; set; }
    public Document? Document { get; set; }

    public Guid ApprovalWorkflowId { get; set; }
    public ApprovalWorkflow? ApprovalWorkflow { get; set; }

    public string WorkflowStatus { get; set; } = "Submitted";

    public int CurrentStageNo { get; set; } = 1;

    public DateTime SubmittedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedDt { get; set; }

    public ICollection<DocumentWorkflowAssignment> Assignments { get; set; }
        = new List<DocumentWorkflowAssignment>();

    public ICollection<DocumentWorkflowHistory> History { get; set; }
        = new List<DocumentWorkflowHistory>();
}
