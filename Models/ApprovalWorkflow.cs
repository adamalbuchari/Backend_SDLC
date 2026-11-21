
namespace DocuFlow.Api.Models;

public class ApprovalWorkflow
{
    public Guid ApprovalWorkflowId { get; set; }

    public string WorkflowName { get; set; } = string.Empty;
    public string? Description { get; set; }

    public Guid? ProjectId { get; set; }
    public Guid? DocumentTypeId { get; set; }

    public DocumentType? DocumentType { get; set; }

    public bool IsActive { get; set; } = true;

    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedDt { get; set; }

    public string? UpdatedBy { get; set; }
    public DateTime? UpdatedDt { get; set; }

    public ICollection<ApprovalWorkflowStage> Stages { get; set; }
        = new List<ApprovalWorkflowStage>();
}
