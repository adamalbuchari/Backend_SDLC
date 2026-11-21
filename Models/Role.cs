namespace DocuFlow.Api.Models;

public class Role
{
    public Guid RoleId { get; set; }

    public string RoleName { get; set; } = string.Empty;
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedDt { get; set; }

    public string? UpdatedBy { get; set; }
    public DateTime? UpdatedDt { get; set; }

    public string? DeletedBy { get; set; }
    public DateTime? DeletedDt { get; set; }

    public bool IsDeleted { get; set; }

    public ICollection<User> Users { get; set; } = new List<User>();
}