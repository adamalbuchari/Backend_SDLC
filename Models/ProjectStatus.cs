namespace DocuFlow.Api.Models;

public class ProjectStatus
{
    public Guid ProjectStatusId { get; set; }

    public string ProjectStatusName { get; set; } = string.Empty;
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedDt { get; set; }

    public string? UpdatedBy { get; set; }
    public DateTime? UpdatedDt { get; set; }

    public string? DeletedBy { get; set; }
    public DateTime? DeletedDt { get; set; }

    public bool IsDeleted { get; set; }

    public ICollection<Project> Projects { get; set; } = new List<Project>();
}