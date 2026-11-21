
namespace DocuFlow.Api.Models;

public class Approval
{
    public int Id { get; set; }

    public int DocumentId { get; set; }
    public Document? Document { get; set; }

    public int ReviewerId { get; set; }
    public User? Reviewer { get; set; }

    public string Status { get; set; } = "Pending";
    public string Comment { get; set; } = string.Empty;

    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DecisionAt { get; set; }
}
