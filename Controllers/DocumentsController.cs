
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DocuFlow.Api.Data;
using DocuFlow.Api.Models;

namespace DocuFlow.Api.Controllers;

[ApiController]
[Route("api/documents")]
[Authorize]
public class DocumentsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _environment;
    private readonly IConfiguration _configuration;

    private const long MaxFileSize = 20 * 1024 * 1024;

    private static readonly Dictionary<string, string> AllowedFiles =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [".pdf"] = "application/pdf",
            [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            [".pptx"] = "application/vnd.openxmlformats-officedocument.presentationml.presentation"
        };

    public DocumentsController(
        AppDbContext db,
        IWebHostEnvironment environment,
        IConfiguration configuration)
    {
        _db = db;
        _environment = environment;
        _configuration = configuration;
    }

    private Guid? ActorId
    {
        get
        {
            var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(value, out var id) ? id : null;
        }
    }

    private string ActorName =>
        User.FindFirstValue(ClaimTypes.Email)
        ?? User.Identity?.Name
        ?? "System";

    private string StorageRoot
    {
        get
        {
            var configured = _configuration["DocumentStorage:RootPath"];

            return Path.IsPathRooted(configured ?? "")
                ? configured!
                : Path.Combine(
                    _environment.ContentRootPath,
                    configured ?? "App_Data/DocumentStorage");
        }
    }

    private async Task<bool> IsSystemAdministratorAsync(Guid userId)
    {
        return await (
            from userRole in _db.UserRoles
            join role in _db.Roles
                on userRole.RoleId equals role.RoleId
            where userRole.UserId == userId
                  && userRole.IsActive
                  && !userRole.IsDeleted
                  && role.RoleName == "System Administrator"
            select userRole
        ).AnyAsync();
    }

    private async Task<bool> CanAccessProjectAsync(
        Guid projectId,
        Guid userId)
    {
        if (await IsSystemAdministratorAsync(userId))
            return true;

        return await _db.ProjectMembers.AnyAsync(x =>
            x.ProjectId == projectId &&
            x.UserId == userId &&
            x.IsActive &&
            !x.IsDeleted);
    }

    private async Task<bool> CanAccessDocumentAsync(
        Guid documentId,
        Guid userId)
    {
        var projectId = await _db.Documents
            .Where(x => x.DocumentId == documentId && !x.IsDeleted)
            .Select(x => (Guid?)x.ProjectId)
            .FirstOrDefaultAsync();

        return projectId.HasValue &&
               await CanAccessProjectAsync(projectId.Value, userId);
    }

    private async Task<string> SaveFileAsync(
        IFormFile file,
        Guid projectId)
    {
        var extension = Path.GetExtension(file.FileName);
        var storedFileName = $"{Guid.NewGuid():N}{extension}";
        var relativeFolder = Path.Combine(
            "documents",
            projectId.ToString("N"));

        var directory = Path.Combine(StorageRoot, relativeFolder);
        Directory.CreateDirectory(directory);

        var destination = Path.Combine(directory, storedFileName);

        await using var stream = new FileStream(
            destination,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None);

        await file.CopyToAsync(stream);

        return Path.Combine(relativeFolder, storedFileName)
            .Replace("\\", "/");
    }

    private string GetPhysicalPath(string relativePath)
    {
        var root = Path.GetFullPath(StorageRoot);
        var fullPath = Path.GetFullPath(
            Path.Combine(root, relativePath));

        if (!fullPath.StartsWith(
                root + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Invalid file path.");
        }

        return fullPath;
    }

    private async Task<(bool Valid, string Message)> ValidateFileAsync(
        IFormFile? file)
    {
        if (file == null || file.Length == 0)
            return (false, "File belum dipilih.");

        if (file.Length > MaxFileSize)
            return (false, "Ukuran file maksimal 20 MB.");

        var extension = Path.GetExtension(file.FileName);

        if (!AllowedFiles.TryGetValue(extension, out var expectedMime))
            return (false, "Format file harus PDF, DOCX, XLSX, atau PPTX.");

        if (!string.Equals(
                file.ContentType,
                expectedMime,
                StringComparison.OrdinalIgnoreCase))
            return (false, "Content type file tidak sesuai dengan ekstensi.");

        await using var stream = file.OpenReadStream();
        var header = new byte[8];
        var read = await stream.ReadAsync(header.AsMemory(0, header.Length));

        if (extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            if (read < 5 ||
                header[0] != 0x25 ||
                header[1] != 0x50 ||
                header[2] != 0x44 ||
                header[3] != 0x46 ||
                header[4] != 0x2D)
                return (false, "Isi file bukan PDF yang valid.");
        }
        else
        {
            // DOCX, XLSX, PPTX merupakan paket Office berbasis ZIP.
            if (read < 4 ||
                header[0] != 0x50 ||
                header[1] != 0x4B ||
                header[2] != 0x03 ||
                header[3] != 0x04)
                return (false, "Isi file Office tidak valid.");
        }

        return (true, "");
    }

    // =========================================
    // GET: api/documents
    // =========================================
    [HttpGet]
    [Authorize(Policy = "DOCUMENT_VIEW")]
    public async Task<IActionResult> GetDocuments(
        [FromQuery] Guid? projectId,
        [FromQuery] Guid? phaseId,
        [FromQuery] string? status,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (ActorId == null)
            return Unauthorized();

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var userId = ActorId.Value;
        var isAdmin = await IsSystemAdministratorAsync(userId);

        var query = _db.Documents
            .AsNoTracking()
            .Where(x => !x.IsDeleted);

        if (!isAdmin)
        {
            var accessibleProjects = _db.ProjectMembers
                .Where(x =>
                    x.UserId == userId &&
                    x.IsActive &&
                    !x.IsDeleted)
                .Select(x => x.ProjectId);

            query = query.Where(x =>
                accessibleProjects.Contains(x.ProjectId));
        }

        if (projectId.HasValue)
            query = query.Where(x => x.ProjectId == projectId.Value);

        if (phaseId.HasValue)
            query = query.Where(x => x.SDLPhaseId == phaseId.Value);

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(x => x.Status == status);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var keyword = search.Trim();

            query = query.Where(x =>
                x.DocumentTitle.Contains(keyword) ||
                (x.Description != null &&
                 x.Description.Contains(keyword)));
        }

        var total = await query.CountAsync();

        var data = await query
            .OrderByDescending(x => x.UpdatedDt ?? x.CreatedDt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new
            {
                x.DocumentId,
                x.ProjectId,
                x.ProjectDocumentRequirementId,
                x.DocumentTypeId,
                x.SDLPhaseId,
                x.DocumentOwnerId,
                x.DocumentTitle,
                x.Description,
                x.CurrentVersionNo,
                x.Status,
                x.ApprovalStatus,
                x.CreatedDt,
                x.UpdatedDt
            })
            .ToListAsync();

        return Ok(new
        {
            success = true,
            data,
            pagination = new
            {
                page,
                pageSize,
                total,
                totalPages = (int)Math.Ceiling(total / (double)pageSize)
            }
        });
    }

    // =========================================
    // GET: api/documents/{id}
    // =========================================
    [HttpGet("{id:guid}")]
    [Authorize(Policy = "DOCUMENT_VIEW")]
    public async Task<IActionResult> GetDocument(Guid id)
    {
        if (ActorId == null)
            return Unauthorized();

        var document = await _db.Documents
            .AsNoTracking()
            .Where(x => x.DocumentId == id && !x.IsDeleted)
            .Select(x => new
            {
                x.DocumentId,
                x.ProjectId,
                x.ProjectDocumentRequirementId,
                x.DocumentTypeId,
                x.SDLPhaseId,
                x.DocumentOwnerId,
                x.DocumentTitle,
                x.Description,
                x.CurrentVersionNo,
                x.Status,
                x.ApprovalStatus,
                x.CreatedBy,
                x.CreatedDt,
                x.UpdatedBy,
                x.UpdatedDt
            })
            .FirstOrDefaultAsync();

        if (document == null)
            return NotFound(new { message = "Dokumen tidak ditemukan." });

        if (!await CanAccessProjectAsync(document.ProjectId, ActorId.Value))
            return Forbid();

        var versions = await _db.DocumentVersions
            .AsNoTracking()
            .Where(x => x.DocumentId == id && !x.IsDeleted)
            .OrderByDescending(x => x.VersionNo)
            .Select(x => new
            {
                x.DocumentVersionId,
                x.VersionNo,
                x.OriginalFileName,
                x.ContentType,
                x.FileSize,
                x.ChangeDescription,
                x.UploadedBy,
                x.UploadedDt
            })
            .ToListAsync();

        return Ok(new
        {
            success = true,
            data = document,
            versions
        });
    }

    // =========================================
    // POST: api/documents/upload
    // =========================================
    [HttpPost("upload")]
    [Authorize(Policy = "DOCUMENT_UPLOAD")]
    [RequestSizeLimit(21 * 1024 * 1024)]
    public async Task<IActionResult> UploadDocument(
        [FromForm] UploadDocumentRequest request)
    {
        if (ActorId == null)
            return Unauthorized();

        var validation = await ValidateFileAsync(request.File);

        if (!validation.Valid)
            return BadRequest(new { message = validation.Message });

        if (!await CanAccessProjectAsync(request.ProjectId, ActorId.Value))
            return Forbid();

        var requirement = await _db.ProjectDocumentRequirements
            .FirstOrDefaultAsync(x =>
                x.ProjectDocumentRequirementId ==
                    request.ProjectDocumentRequirementId &&
                x.ProjectId == request.ProjectId &&
                x.IsActive &&
                !x.IsDeleted);

        if (requirement == null)
            return BadRequest(new
            {
                message = "Document Requirement tidak ditemukan pada project ini."
            });

        if (requirement.DocumentTypeId != request.DocumentTypeId ||
            requirement.SDLPhaseId != request.SDLPhaseId)
            return BadRequest(new
            {
                message = "Document Type atau SDLC Phase tidak sesuai dengan requirement."
            });

        var ownerExists = await _db.Users.AnyAsync(x =>
            x.UserId == request.DocumentOwnerId &&
            x.IsActive &&
            !x.IsDeleted);

        if (!ownerExists)
            return BadRequest(new { message = "Document Owner tidak valid." });

        var existingDocument = await _db.Documents.AnyAsync(x =>
            x.ProjectDocumentRequirementId ==
                request.ProjectDocumentRequirementId &&
            !x.IsDeleted);

        if (existingDocument)
            return Conflict(new
            {
                message = "Dokumen untuk requirement ini sudah tersedia. Gunakan fitur upload versi baru."
            });

        var extension = Path.GetExtension(request.File!.FileName);
        var contentType = AllowedFiles[extension];
        var originalFileName = Path.GetFileName(request.File.FileName);

        string? relativePath = null;

        await using var transaction = await _db.Database.BeginTransactionAsync();

        try
        {
            relativePath = await SaveFileAsync(request.File, request.ProjectId);

            var document = new Document
            {
                DocumentId = Guid.NewGuid(),
                ProjectId = request.ProjectId,
                ProjectDocumentRequirementId =
                    request.ProjectDocumentRequirementId,
                DocumentTypeId = request.DocumentTypeId,
                SDLPhaseId = request.SDLPhaseId,
                DocumentOwnerId = request.DocumentOwnerId,
                DocumentTitle = request.DocumentTitle.Trim(),
                Description = request.Description?.Trim(),
                CurrentVersionNo = 1,
                Status = "Draft",
                ApprovalStatus = "Not Submitted",
                CreatedBy = ActorName,
                CreatedDt = DateTime.Now
            };

            var version = new DocumentVersion
            {
                DocumentVersionId = Guid.NewGuid(),
                DocumentId = document.DocumentId,
                VersionNo = 1,
                OriginalFileName = originalFileName,
                StoredFileName = Path.GetFileName(relativePath),
                FilePath = relativePath,
                ContentType = contentType,
                FileSize = request.File.Length,
                ChangeDescription = request.ChangeDescription?.Trim(),
                UploadedBy = ActorId.Value,
                UploadedDt = DateTime.Now,
                CreatedBy = ActorName,
                CreatedDt = DateTime.Now
            };

            _db.Documents.Add(document);
            _db.DocumentVersions.Add(version);

            await _db.SaveChangesAsync();
            await transaction.CommitAsync();

            return CreatedAtAction(
                nameof(GetDocument),
                new { id = document.DocumentId },
                new
                {
                    success = true,
                    message = "Dokumen berhasil di-upload.",
                    documentId = document.DocumentId,
                    versionNo = 1
                });
        }
        catch
        {
            await transaction.RollbackAsync();

            if (relativePath != null)
            {
                var physicalPath = GetPhysicalPath(relativePath);

                if (System.IO.File.Exists(physicalPath))
                    System.IO.File.Delete(physicalPath);
            }

            throw;
        }
    }

    // =========================================
    // GET: api/documents/{id}/download
    // =========================================
    [HttpGet("{id:guid}/download")]
    [Authorize(Policy = "DOCUMENT_VIEW")]
    public async Task<IActionResult> DownloadCurrentVersion(Guid id)
    {
        if (ActorId == null)
            return Unauthorized();

        var document = await _db.Documents
            .FirstOrDefaultAsync(x => x.DocumentId == id && !x.IsDeleted);

        if (document == null)
            return NotFound();

        if (!await CanAccessProjectAsync(document.ProjectId, ActorId.Value))
            return Forbid();

        var version = await _db.DocumentVersions
            .FirstOrDefaultAsync(x =>
                x.DocumentId == id &&
                x.VersionNo == document.CurrentVersionNo &&
                !x.IsDeleted);

        if (version == null)
            return NotFound(new { message = "File dokumen tidak ditemukan." });

        var path = GetPhysicalPath(version.FilePath);

        if (!System.IO.File.Exists(path))
            return NotFound(new { message = "File tidak tersedia di storage." });

        return PhysicalFile(
            path,
            version.ContentType,
            version.OriginalFileName);
    }

    // =========================================
    // GET: api/documents/{id}/versions/{versionNo}/download
    // =========================================
    [HttpGet("{id:guid}/versions/{versionNo:int}/download")]
    [Authorize(Policy = "DOCUMENT_VIEW")]
    public async Task<IActionResult> DownloadVersion(
        Guid id,
        int versionNo)
    {
        if (ActorId == null)
            return Unauthorized();

        var document = await _db.Documents
            .FirstOrDefaultAsync(x => x.DocumentId == id && !x.IsDeleted);

        if (document == null)
            return NotFound();

        if (!await CanAccessProjectAsync(document.ProjectId, ActorId.Value))
            return Forbid();

        var version = await _db.DocumentVersions
            .FirstOrDefaultAsync(x =>
                x.DocumentId == id &&
                x.VersionNo == versionNo &&
                !x.IsDeleted);

        if (version == null)
            return NotFound();

        var path = GetPhysicalPath(version.FilePath);

        if (!System.IO.File.Exists(path))
            return NotFound(new { message = "File tidak tersedia di storage." });

        return PhysicalFile(
            path,
            version.ContentType,
            version.OriginalFileName);
    }

    // =========================================
    // POST: api/documents/{id}/versions
    // =========================================
    [HttpPost("{id:guid}/versions")]
    [Authorize(Policy = "DOCUMENT_UPLOAD")]
    [RequestSizeLimit(21 * 1024 * 1024)]
    public async Task<IActionResult> UploadNewVersion(
        Guid id,
        [FromForm] UploadDocumentVersionRequest request)
    {
        if (ActorId == null)
            return Unauthorized();

        var validation = await ValidateFileAsync(request.File);

        if (!validation.Valid)
            return BadRequest(new { message = validation.Message });

        var document = await _db.Documents
            .FirstOrDefaultAsync(x => x.DocumentId == id && !x.IsDeleted);

        if (document == null)
            return NotFound();

        if (!await CanAccessProjectAsync(document.ProjectId, ActorId.Value))
            return Forbid();

        var extension = Path.GetExtension(request.File!.FileName);
        var contentType = AllowedFiles[extension];
        var originalFileName = Path.GetFileName(request.File.FileName);

        var nextVersion = document.CurrentVersionNo + 1;
        string? relativePath = null;

        await using var transaction = await _db.Database.BeginTransactionAsync();

        try
        {
            relativePath = await SaveFileAsync(request.File, document.ProjectId);

            var version = new DocumentVersion
            {
                DocumentVersionId = Guid.NewGuid(),
                DocumentId = document.DocumentId,
                VersionNo = nextVersion,
                OriginalFileName = originalFileName,
                StoredFileName = Path.GetFileName(relativePath),
                FilePath = relativePath,
                ContentType = contentType,
                FileSize = request.File.Length,
                ChangeDescription = request.ChangeDescription?.Trim(),
                UploadedBy = ActorId.Value,
                UploadedDt = DateTime.Now,
                CreatedBy = ActorName,
                CreatedDt = DateTime.Now
            };

            document.CurrentVersionNo = nextVersion;
            document.Status = "Draft";
            document.ApprovalStatus = "Not Submitted";
            document.UpdatedBy = ActorName;
            document.UpdatedDt = DateTime.Now;

            _db.DocumentVersions.Add(version);

            await _db.SaveChangesAsync();
            await transaction.CommitAsync();

            return Ok(new
            {
                success = true,
                message = "Versi dokumen baru berhasil di-upload.",
                documentId = id,
                versionNo = nextVersion
            });
        }
        catch
        {
            await transaction.RollbackAsync();

            if (relativePath != null)
            {
                var physicalPath = GetPhysicalPath(relativePath);

                if (System.IO.File.Exists(physicalPath))
                    System.IO.File.Delete(physicalPath);
            }

            throw;
        }
    }

    // =========================================
    // DELETE: api/documents/{id}
    // Soft delete
    // =========================================
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "DOCUMENT_DELETE")]
    public async Task<IActionResult> DeleteDocument(Guid id)
    {
        if (ActorId == null)
            return Unauthorized();

        var document = await _db.Documents
            .FirstOrDefaultAsync(x => x.DocumentId == id && !x.IsDeleted);

        if (document == null)
            return NotFound();

        if (!await CanAccessProjectAsync(document.ProjectId, ActorId.Value))
            return Forbid();

        document.IsDeleted = true;
        document.DeletedBy = ActorName;
        document.DeletedDt = DateTime.Now;
        document.UpdatedBy = ActorName;
        document.UpdatedDt = DateTime.Now;

        await _db.SaveChangesAsync();

        return Ok(new
        {
            success = true,
            message = "Dokumen berhasil diarsipkan melalui soft delete."
        });
    }



    [HttpGet("upload-options")]
    [Authorize(Policy = "DOCUMENT_UPLOAD")]
    public async Task<IActionResult> GetUploadOptions([FromQuery] Guid projectId)
    {
        if (ActorId == null)
            return Unauthorized();

        if (projectId == Guid.Empty)
            return BadRequest(new
            {
                success = false,
                message = "Project wajib dipilih."
            });

        if (!await CanAccessProjectAsync(projectId, ActorId.Value))
            return Forbid();

        var requirements = await _db.ProjectDocumentRequirements
            .AsNoTracking()
            .Where(x =>
                x.ProjectId == projectId &&
                x.IsActive &&
                !x.IsDeleted &&
                !_db.Documents.Any(d =>
                    d.ProjectDocumentRequirementId == x.ProjectDocumentRequirementId &&
                    !d.IsDeleted))
            .OrderBy(x => x.SequenceNo)
            .Select(x => new
            {
                x.ProjectDocumentRequirementId,
                x.DocumentName,
                x.DocumentTypeId,
                x.SDLPhaseId,
                x.IsRequired,
                x.SequenceNo
            })
            .ToListAsync();

        var users = await _db.Users
            .AsNoTracking()
            .Where(x => x.IsActive && !x.IsDeleted)
            .OrderBy(x => x.FullName)
            .Select(x => new
            {
                x.UserId,
                x.FullName,
                x.Email
            })
            .ToListAsync();

        return Ok(new
        {
            success = true,
            data = new
            {
                projectId,
                requirements,
                users
            }
        });
    }
}

public class UploadDocumentRequest
{
    public Guid ProjectId { get; set; }
    public Guid ProjectDocumentRequirementId { get; set; }
    public Guid DocumentTypeId { get; set; }
    public Guid SDLPhaseId { get; set; }
    public Guid DocumentOwnerId { get; set; }

    public string DocumentTitle { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ChangeDescription { get; set; }

    public IFormFile? File { get; set; }
}

public class UploadDocumentVersionRequest
{
    public string? ChangeDescription { get; set; }
    public IFormFile? File { get; set; }
}
