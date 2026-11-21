
namespace DocuFlow.Api.Models;

public class ProjectDocumentRequirement
{
    public Guid ProjectDocumentRequirementId { get; set; }

    public Guid ProjectId { get; set; }
    public Guid DocumentTemplateItemId { get; set; }
    public Guid DocumentTypeId { get; set; }

    public string DocumentName { get; set; } = string.Empty;

    public Guid SDLPhaseId { get; set; }

    public bool IsRequired { get; set; }
    public int SequenceNo { get; set; }
    public bool IsActive { get; set; }

    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedDt { get; set; }

    public string? UpdatedBy { get; set; }
    public DateTime? UpdatedDt { get; set; }

    public string? DeletedBy { get; set; }
    public DateTime? DeletedDt { get; set; }

    public bool IsDeleted { get; set; }
}
