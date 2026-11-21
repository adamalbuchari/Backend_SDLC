namespace DocuFlow.Api.DTOs.ReviewApproval;

public class ApprovalDecisionRequest
{
    public string Decision { get; set; } = string.Empty;
    public string? Comment { get; set; }
}