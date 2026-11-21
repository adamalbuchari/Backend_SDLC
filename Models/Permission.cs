
namespace DocuFlow.Api.Models;

public class Permission
{
    public Guid PermissionId { get; set; }
    public string PermissionCode { get; set; } = string.Empty;
    public string PermissionName { get; set; } = string.Empty;
    public string? Description { get; set; }

    public bool IsActive { get; set; }

    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedDt { get; set; }

    public bool IsDeleted { get; set; }
}
