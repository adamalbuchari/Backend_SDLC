
namespace DocuFlow.Api.Models;

public class RolePermission
{
    public Guid RolePermissionId { get; set; }
    public Guid RoleId { get; set; }
    public Guid PermissionId { get; set; }

    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedDt { get; set; }

    public bool IsDeleted { get; set; }
}
