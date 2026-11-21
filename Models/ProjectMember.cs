
namespace DocuFlow.Api.Models;

public class ProjectMember
{
    public Guid ProjectMemberId { get; set; }
    public Guid ProjectId { get; set; }
    public Guid UserId { get; set; }
    public Guid ProjectRoleId { get; set; }

    public bool IsActive { get; set; }

    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedDt { get; set; }

    public string? UpdatedBy { get; set; }
    public DateTime? UpdatedDt { get; set; }

    public string? DeletedBy { get; set; }
    public DateTime? DeletedDt { get; set; }

    public bool IsDeleted { get; set; }
}
