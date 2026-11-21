
namespace DocuFlow.Api.Models;

public class Report
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Scope { get; set; } = string.Empty;
    public string Period { get; set; } = string.Empty;
    public string Format { get; set; } = "PDF";
    public string Status { get; set; } = "Ready";
    public int CreatedById { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
