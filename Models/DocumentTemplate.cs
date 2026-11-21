
namespace DocuFlow.Api.Models;

public class DocumentTemplate
{
    public Guid DocumentTemplateId { get; set; }
    public Guid ProjectTypeId { get; set; }

    public string TemplateName { get; set; } = string.Empty;
    public string? Description { get; set; }

    public int VersionNo { get; set; }

    public bool IsActive { get; set; }

    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedDt { get; set; }

    public string? UpdatedBy { get; set; }
    public DateTime? UpdatedDt { get; set; }

    public string? DeletedBy { get; set; }
    public DateTime? DeletedDt { get; set; }

    public bool IsDeleted { get; set; }
}
