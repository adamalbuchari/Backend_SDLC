
namespace DocuFlow.Api.Models;

public class DocumentVersion
{
    public Guid DocumentVersionId { get; set; }
    public Guid DocumentId { get; set; }

    public int VersionNo { get; set; }

    public string OriginalFileName { get; set; } = string.Empty;
    public string StoredFileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }

    public string? ChangeDescription { get; set; }

    public Guid UploadedBy { get; set; }
    public DateTime UploadedDt { get; set; }

    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedDt { get; set; }
    public bool IsDeleted { get; set; }

    public Document? Document { get; set; }
}
