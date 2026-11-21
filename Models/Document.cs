
namespace DocuFlow.Api.Models;

public class Document
{
    public Guid DocumentId { get; set; }
    public Guid ProjectId { get; set; }
    public Guid ProjectDocumentRequirementId { get; set; }
    public Guid DocumentTypeId { get; set; }
    public Guid SDLPhaseId { get; set; }
    public Guid DocumentOwnerId { get; set; }

    public string DocumentTitle { get; set; } = string.Empty;
    public string? Description { get; set; }

    public int CurrentVersionNo { get; set; } = 1;
    public string Status { get; set; } = "Draft";
    public string ApprovalStatus { get; set; } = "Not Submitted";

    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedDt { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTime? UpdatedDt { get; set; }
    public string? DeletedBy { get; set; }
    public DateTime? DeletedDt { get; set; }
    public bool IsDeleted { get; set; }

    public ICollection<DocumentVersion> Versions { get; set; }
        = new List<DocumentVersion>();
}
