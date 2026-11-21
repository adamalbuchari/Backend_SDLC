
namespace DocuFlow.Api.Models;

public class Project
{
    public Guid ProjectId { get; set; }

    public Guid ProjectTypeId { get; set; }
    public ProjectType? ProjectType { get; set; }

    public Guid ProjectStatusId { get; set; }
    public ProjectStatus? ProjectStatus { get; set; }

    public string ProjectCode { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public string? Description { get; set; }

    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }

    public Guid ProjectManagerId { get; set; }
    public Guid? DocumentTemplateId { get; set; }
    public DocumentTemplate? DocumentTemplate { get; set; }
    public User? ProjectManager { get; set; }

    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedDt { get; set; }

    public string? UpdatedBy { get; set; }
    public DateTime? UpdatedDt { get; set; }

    public string? DeletedBy { get; set; }
    public DateTime? DeletedDt { get; set; }

    public bool IsDeleted { get; set; }
}
