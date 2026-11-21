namespace DocuFlow.Api.DTOs.ReviewApproval;

public class ReviewDecisionRequest
{
    public string Decision { get; set; } = string.Empty;
    public string? Comment { get; set; }
}