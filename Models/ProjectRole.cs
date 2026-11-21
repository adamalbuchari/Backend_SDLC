
namespace DocuFlow.Api.Models;

public class ProjectRole
{
    public Guid ProjectRoleId { get; set; }
    public string ProjectRoleName { get; set; } = string.Empty;
    public string? Description { get; set; }

    public bool IsActive { get; set; }

    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedDt { get; set; }

    public bool IsDeleted { get; set; }
}
