using System.Security.Claims;
using DocuFlow.Api.Data;
using DocuFlow.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DocuFlow.Api.Controllers;

[ApiController]
[Route("api/sdlc-templates")]
[Authorize]
public class SDLCTemplatesController : ControllerBase
{
    private readonly AppDbContext _db;

    public SDLCTemplatesController(AppDbContext db)
    {
        _db = db;
    }

    private Guid UserId =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id
            : Guid.Empty;

    private string AuditUser =>
        User.Identity?.Name ??
        User.FindFirstValue(ClaimTypes.Email) ??
        UserId.ToString();

    private async Task<bool> IsAdminAsync()
    {
        if (UserId == Guid.Empty)
            return false;

        var hasUserRole = await (
            from ur in _db.UserRoles
            join r in _db.Roles on ur.RoleId equals r.RoleId
            where ur.UserId == UserId
                  && ur.IsActive
                  && !ur.IsDeleted
                  && r.IsActive
                  && !r.IsDeleted
                  && r.RoleName == "System Administrator"
            select ur.UserId
        ).AnyAsync();

        if (hasUserRole)
            return true;

        return await _db.Users.AnyAsync(u =>
            u.UserId == UserId &&
            u.IsActive &&
            !u.IsDeleted &&
            u.Role != null &&
            u.Role.RoleName == "System Administrator");
    }

    private async Task<IActionResult?> RequireAdminAsync()
    {
        return await IsAdminAsync()
            ? null
            : StatusCode(403, new
            {
                success = false,
                message = "Hanya System Administrator yang dapat mengelola konfigurasi SDLC."
            });
    }

    [HttpGet("lookups")]
    public async Task<IActionResult> Lookups()
    {
        var phases = await _db.SDLCPhases
            .AsNoTracking()
            .Where(x => x.IsActive && !x.IsDeleted)
            .OrderBy(x => x.SequenceNo)
            .Select(x => new
            {
                x.SDLPhaseId,
                x.SDLPhaseName,
                x.Description,
                x.SequenceNo
            })
            .ToListAsync();

        var projectTypes = await _db.ProjectTypes
            .AsNoTracking()
            .Where(x => x.IsActive && !x.IsDeleted)
            .OrderBy(x => x.ProjectTypeName)
            .Select(x => new
            {
                x.ProjectTypeId,
                x.ProjectTypeName
            })
            .ToListAsync();

        var documentTypes = await _db.DocumentTypes
            .AsNoTracking()
            .Where(x => x.IsActive && !x.IsDeleted)
            .OrderBy(x => x.DocumentTypeName)
            .Select(x => new
            {
                x.DocumentTypeId,
                x.DocumentTypeCode,
                x.DocumentTypeName,
                x.Description
            })
            .ToListAsync();

        return Ok(new
        {
            success = true,
            data = new
            {
                phases,
                projectTypes,
                documentTypes
            }
        });
    }

    [HttpGet]
    public async Task<IActionResult> GetTemplates()
    {
        if (await RequireAdminAsync() is IActionResult denied)
            return denied;

        var rows = await (
            from template in _db.DocumentTemplates.AsNoTracking()
            join projectType in _db.ProjectTypes.AsNoTracking()
                on template.ProjectTypeId equals projectType.ProjectTypeId
            where !template.IsDeleted
            orderby projectType.ProjectTypeName, template.TemplateName
            select new
            {
                template.DocumentTemplateId,
                template.ProjectTypeId,
                ProjectTypeName = projectType.ProjectTypeName,
                template.TemplateName,
                template.Description,
                template.VersionNo,
                template.IsActive,
                ItemCount = _db.DocumentTemplateItems.Count(item =>
                    item.DocumentTemplateId == template.DocumentTemplateId &&
                    item.IsActive &&
                    !item.IsDeleted)
            }
        ).ToListAsync();

        return Ok(new
        {
            success = true,
            data = rows
        });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetTemplate(Guid id)
    {
        if (await RequireAdminAsync() is IActionResult denied)
            return denied;

        var template = await _db.DocumentTemplates
            .AsNoTracking()
            .Where(x => x.DocumentTemplateId == id && !x.IsDeleted)
            .Select(x => new
            {
                x.DocumentTemplateId,
                x.ProjectTypeId,
                x.TemplateName,
                x.Description,
                x.VersionNo,
                x.IsActive
            })
            .FirstOrDefaultAsync();

        if (template == null)
        {
            return NotFound(new
            {
                success = false,
                message = "Template tidak ditemukan."
            });
        }

        var items = await (
            from item in _db.DocumentTemplateItems.AsNoTracking()
            join phase in _db.SDLCPhases.AsNoTracking()
                on item.SDLPhaseId equals phase.SDLPhaseId into phaseJoin
            from phase in phaseJoin.DefaultIfEmpty()
            join documentType in _db.DocumentTypes.AsNoTracking()
                on item.DocumentTypeId equals documentType.DocumentTypeId into typeJoin
            from documentType in typeJoin.DefaultIfEmpty()
            where item.DocumentTemplateId == id && !item.IsDeleted
            orderby item.SequenceNo
            select new
            {
                item.DocumentTemplateItemId,
                item.DocumentName,
                item.Description,
                item.IsRequired,
                item.SequenceNo,
                item.IsActive,
                item.SDLPhaseId,
                PhaseName = phase == null ? null : phase.SDLPhaseName,
                item.DocumentTypeId,
                DocumentTypeName = documentType == null
                    ? null
                    : documentType.DocumentTypeName
            }
        ).ToListAsync();

        return Ok(new
        {
            success = true,
            data = new
            {
                template,
                items
            }
        });
    }

    [HttpPost]
    public async Task<IActionResult> CreateTemplate(
        [FromBody] TemplateRequest request)
    {
        if (await RequireAdminAsync() is IActionResult denied)
            return denied;

        if (string.IsNullOrWhiteSpace(request.TemplateName) ||
            request.ProjectTypeId == Guid.Empty)
        {
            return BadRequest(new
            {
                success = false,
                message = "Jenis proyek dan nama template wajib diisi."
            });
        }

        var projectTypeExists = await _db.ProjectTypes.AnyAsync(x =>
            x.ProjectTypeId == request.ProjectTypeId &&
            x.IsActive &&
            !x.IsDeleted);

        if (!projectTypeExists)
        {
            return BadRequest(new
            {
                success = false,
                message = "Jenis proyek tidak valid."
            });
        }

        var templateName = request.TemplateName.Trim();

        var duplicate = await _db.DocumentTemplates.AnyAsync(x =>
            x.ProjectTypeId == request.ProjectTypeId &&
            x.TemplateName == templateName &&
            !x.IsDeleted);

        if (duplicate)
        {
            return Conflict(new
            {
                success = false,
                message = "Nama template sudah digunakan untuk jenis proyek ini."
            });
        }

        var entity = new DocumentTemplate
        {
            DocumentTemplateId = Guid.NewGuid(),
            ProjectTypeId = request.ProjectTypeId,
            TemplateName = templateName,
            Description = request.Description?.Trim(),
            VersionNo = Math.Max(1, request.VersionNo),
            IsActive = true,
            CreatedBy = AuditUser,
            CreatedDt = DateTime.Now
        };

        _db.DocumentTemplates.Add(entity);
        await _db.SaveChangesAsync();

        return CreatedAtAction(
            nameof(GetTemplate),
            new { id = entity.DocumentTemplateId },
            new
            {
                success = true,
                data = new
                {
                    entity.DocumentTemplateId
                }
            });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateTemplate(
        Guid id,
        [FromBody] TemplateRequest request)
    {
        if (await RequireAdminAsync() is IActionResult denied)
            return denied;

        var entity = await _db.DocumentTemplates
            .FirstOrDefaultAsync(x =>
                x.DocumentTemplateId == id &&
                !x.IsDeleted);

        if (entity == null)
        {
            return NotFound(new
            {
                success = false,
                message = "Template tidak ditemukan."
            });
        }

        if (string.IsNullOrWhiteSpace(request.TemplateName))
        {
            return BadRequest(new
            {
                success = false,
                message = "Nama template wajib diisi."
            });
        }

        var projectTypeExists = await _db.ProjectTypes.AnyAsync(x =>
            x.ProjectTypeId == request.ProjectTypeId &&
            x.IsActive &&
            !x.IsDeleted);

        if (!projectTypeExists)
        {
            return BadRequest(new
            {
                success = false,
                message = "Jenis proyek tidak valid."
            });
        }

        var duplicate = await _db.DocumentTemplates.AnyAsync(x =>
            x.DocumentTemplateId != id &&
            x.ProjectTypeId == request.ProjectTypeId &&
            x.TemplateName == request.TemplateName.Trim() &&
            !x.IsDeleted);

        if (duplicate)
        {
            return Conflict(new
            {
                success = false,
                message = "Nama template sudah digunakan untuk jenis proyek ini."
            });
        }

        entity.TemplateName = request.TemplateName.Trim();
        entity.Description = request.Description?.Trim();
        entity.VersionNo = Math.Max(1, request.VersionNo);
        entity.ProjectTypeId = request.ProjectTypeId;
        entity.UpdatedBy = AuditUser;
        entity.UpdatedDt = DateTime.Now;

        await _db.SaveChangesAsync();

        return Ok(new
        {
            success = true,
            message = "Template berhasil diperbarui."
        });
    }

    [HttpPut("{id:guid}/status")]
    public async Task<IActionResult> SetTemplateStatus(
        Guid id,
        [FromBody] StatusRequest request)
    {
        if (await RequireAdminAsync() is IActionResult denied)
            return denied;

        var entity = await _db.DocumentTemplates
            .FirstOrDefaultAsync(x =>
                x.DocumentTemplateId == id &&
                !x.IsDeleted);

        if (entity == null)
        {
            return NotFound(new
            {
                success = false,
                message = "Template tidak ditemukan."
            });
        }

        entity.IsActive = request.IsActive;
        entity.UpdatedBy = AuditUser;
        entity.UpdatedDt = DateTime.Now;

        await _db.SaveChangesAsync();

        return Ok(new
        {
            success = true,
            message = "Status template berhasil diperbarui."
        });
    }

    [HttpPut("{id:guid}/items")]
    public async Task<IActionResult> SaveItems(
        Guid id,
        [FromBody] List<TemplateItemRequest> items)
    {
        if (await RequireAdminAsync() is IActionResult denied)
            return denied;

        var template = await _db.DocumentTemplates
            .FirstOrDefaultAsync(x =>
                x.DocumentTemplateId == id &&
                !x.IsDeleted);

        if (template == null)
        {
            return NotFound(new
            {
                success = false,
                message = "Template tidak ditemukan."
            });
        }

        if (items == null || items.Count == 0)
        {
            return BadRequest(new
            {
                success = false,
                message = "Tambahkan minimal satu dokumen ke template."
            });
        }

        if (items.Any(x =>
                string.IsNullOrWhiteSpace(x.DocumentName) ||
                x.SDLPhaseId == null ||
                x.DocumentTypeId == null))
        {
            return BadRequest(new
            {
                success = false,
                message = "Nama dokumen, fase SDLC, dan jenis dokumen wajib diisi pada setiap item."
            });
        }

        var phaseIds = items
            .Select(x => x.SDLPhaseId!.Value)
            .Distinct()
            .ToList();

        var documentTypeIds = items
            .Select(x => x.DocumentTypeId!.Value)
            .Distinct()
            .ToList();

        var validPhaseCount = await _db.SDLCPhases.CountAsync(x =>
            phaseIds.Contains(x.SDLPhaseId) &&
            x.IsActive &&
            !x.IsDeleted);

        var validDocumentTypeCount = await _db.DocumentTypes.CountAsync(x =>
            documentTypeIds.Contains(x.DocumentTypeId) &&
            x.IsActive &&
            !x.IsDeleted);

        if (validPhaseCount != phaseIds.Count ||
            validDocumentTypeCount != documentTypeIds.Count)
        {
            return BadRequest(new
            {
                success = false,
                message = "Fase SDLC atau jenis dokumen tidak valid/nonaktif."
            });
        }

        await using var transaction =
            await _db.Database.BeginTransactionAsync();

        var existingItems = await _db.DocumentTemplateItems
            .Where(x =>
                x.DocumentTemplateId == id &&
                !x.IsDeleted)
            .ToListAsync();

        foreach (var oldItem in existingItems)
        {
            oldItem.IsDeleted = true;
            oldItem.IsActive = false;
            oldItem.DeletedBy = AuditUser;
            oldItem.DeletedDt = DateTime.Now;
        }

        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];

            _db.DocumentTemplateItems.Add(
                new DocumentTemplateItem
                {
                    DocumentTemplateItemId = Guid.NewGuid(),
                    DocumentTemplateId = id,
                    DocumentName = item.DocumentName.Trim(),
                    Description = item.Description?.Trim(),
                    IsRequired = item.IsRequired,
                    SequenceNo = index + 1,
                    IsActive = true,
                    SDLPhaseId = item.SDLPhaseId,
                    DocumentTypeId = item.DocumentTypeId,
                    CreatedBy = AuditUser,
                    CreatedDt = DateTime.Now
                });
        }

        template.UpdatedBy = AuditUser;
        template.UpdatedDt = DateTime.Now;

        await _db.SaveChangesAsync();
        await transaction.CommitAsync();

        return Ok(new
        {
            success = true,
            message = "Daftar dokumen template berhasil disimpan."
        });
    }

    [HttpGet("phases")]
    public async Task<IActionResult> GetPhases()
    {
        if (await RequireAdminAsync() is IActionResult denied)
            return denied;

        var rows = await _db.SDLCPhases
            .AsNoTracking()
            .Where(x => !x.IsDeleted)
            .OrderBy(x => x.SequenceNo)
            .ToListAsync();

        return Ok(new
        {
            success = true,
            data = rows
        });
    }

    [HttpPost("phases")]
    public async Task<IActionResult> CreatePhase(
        [FromBody] PhaseRequest request)
    {
        if (await RequireAdminAsync() is IActionResult denied)
            return denied;

        if (string.IsNullOrWhiteSpace(request.SDLPhaseName))
        {
            return BadRequest(new
            {
                success = false,
                message = "Nama fase wajib diisi."
            });
        }

        var name = request.SDLPhaseName.Trim();

        var duplicate = await _db.SDLCPhases.AnyAsync(x =>
            x.SDLPhaseName == name &&
            !x.IsDeleted);

        if (duplicate)
        {
            return Conflict(new
            {
                success = false,
                message = "Nama fase sudah digunakan."
            });
        }

        var entity = new SDLCPhase
        {
            SDLPhaseId = Guid.NewGuid(),
            SDLPhaseName = name,
            Description = request.Description?.Trim(),
            SequenceNo = await _db.SDLCPhases.CountAsync(x => !x.IsDeleted) + 1,
            IsActive = true,
            CreatedBy = AuditUser,
            CreatedDt = DateTime.Now,
            IsDeleted = false
        };

        _db.SDLCPhases.Add(entity);
        await _db.SaveChangesAsync();

        return Ok(new
        {
            success = true,
            data = entity
        });
    }

    [HttpPut("phases/{id:guid}")]
    public async Task<IActionResult> UpdatePhase(
        Guid id,
        [FromBody] PhaseRequest request)
    {
        if (await RequireAdminAsync() is IActionResult denied)
            return denied;

        var entity = await _db.SDLCPhases
            .FirstOrDefaultAsync(x =>
                x.SDLPhaseId == id &&
                !x.IsDeleted);

        if (entity == null)
        {
            return NotFound(new
            {
                success = false,
                message = "Fase tidak ditemukan."
            });
        }

        if (string.IsNullOrWhiteSpace(request.SDLPhaseName))
        {
            return BadRequest(new
            {
                success = false,
                message = "Nama fase wajib diisi."
            });
        }

        var name = request.SDLPhaseName.Trim();

        var duplicate = await _db.SDLCPhases.AnyAsync(x =>
            x.SDLPhaseId != id &&
            x.SDLPhaseName == name &&
            !x.IsDeleted);

        if (duplicate)
        {
            return Conflict(new
            {
                success = false,
                message = "Nama fase sudah digunakan."
            });
        }

        entity.SDLPhaseName = name;
        entity.Description = request.Description?.Trim();
        entity.IsActive = request.IsActive;
        entity.UpdatedBy = AuditUser;
        entity.UpdatedDt = DateTime.Now;

        await _db.SaveChangesAsync();

        return Ok(new
        {
            success = true,
            message = "Fase diperbarui."
        });
    }

    [HttpPut("phases/order")]
    public async Task<IActionResult> ReorderPhases(
        [FromBody] ReorderRequest request)
    {
        if (await RequireAdminAsync() is IActionResult denied)
            return denied;

        var rows = await _db.SDLCPhases
            .Where(x =>
                request.Ids.Contains(x.SDLPhaseId) &&
                !x.IsDeleted)
            .ToListAsync();

        var expectedCount = await _db.SDLCPhases
            .CountAsync(x => !x.IsDeleted);

        if (rows.Count != request.Ids.Distinct().Count() ||
            rows.Count != expectedCount)
        {
            return BadRequest(new
            {
                success = false,
                message = "Daftar urutan fase harus memuat seluruh fase aktif/nonaktif."
            });
        }

        for (var index = 0; index < request.Ids.Count; index++)
        {
            var row = rows.Single(x =>
                x.SDLPhaseId == request.Ids[index]);

            row.SequenceNo = index + 1;
            row.UpdatedBy = AuditUser;
            row.UpdatedDt = DateTime.Now;
        }

        await _db.SaveChangesAsync();

        return Ok(new
        {
            success = true,
            message = "Urutan fase diperbarui."
        });
    }

    [HttpPut("phases/{id:guid}/status")]
    public async Task<IActionResult> SetPhaseStatus(
        Guid id,
        [FromBody] StatusRequest request)
    {
        if (await RequireAdminAsync() is IActionResult denied)
            return denied;

        var entity = await _db.SDLCPhases
            .FirstOrDefaultAsync(x =>
                x.SDLPhaseId == id &&
                !x.IsDeleted);

        if (entity == null)
        {
            return NotFound(new
            {
                success = false,
                message = "Fase tidak ditemukan."
            });
        }

        if (!request.IsActive)
        {
            var usedByTemplate = await _db.DocumentTemplateItems.AnyAsync(x =>
                x.SDLPhaseId == id &&
                !x.IsDeleted);

            var usedByProject = await _db.ProjectDocumentRequirements.AnyAsync(x =>
                x.SDLPhaseId == id &&
                !x.IsDeleted);

            if (usedByTemplate || usedByProject)
            {
                return Conflict(new
                {
                    success = false,
                    message = "Fase masih digunakan oleh template atau proyek."
                });
            }
        }

        entity.IsActive = request.IsActive;
        entity.UpdatedBy = AuditUser;
        entity.UpdatedDt = DateTime.Now;

        await _db.SaveChangesAsync();

        return Ok(new
        {
            success = true,
            message = "Status fase diperbarui."
        });
    }

    [HttpPost("document-types")]
    public async Task<IActionResult> CreateDocumentType(
        [FromBody] DocumentTypeRequest request)
    {
        if (await RequireAdminAsync() is IActionResult denied)
            return denied;

        if (string.IsNullOrWhiteSpace(request.DocumentTypeCode) ||
            string.IsNullOrWhiteSpace(request.DocumentTypeName))
        {
            return BadRequest(new
            {
                success = false,
                message = "Kode dan nama jenis dokumen wajib diisi."
            });
        }

        var code = request.DocumentTypeCode.Trim().ToUpperInvariant();
        var name = request.DocumentTypeName.Trim();

        var duplicate = await _db.DocumentTypes.AnyAsync(x =>
            (x.DocumentTypeCode == code ||
             x.DocumentTypeName == name) &&
            !x.IsDeleted);

        if (duplicate)
        {
            return Conflict(new
            {
                success = false,
                message = "Kode atau nama jenis dokumen sudah digunakan."
            });
        }

        var entity = new DocumentType
        {
            DocumentTypeId = Guid.NewGuid(),
            DocumentTypeCode = code,
            DocumentTypeName = name,
            Description = request.Description?.Trim(),
            IsActive = true,
            CreatedBy = AuditUser,
            CreatedDt = DateTime.Now
        };

        _db.DocumentTypes.Add(entity);
        await _db.SaveChangesAsync();

        return Ok(new
        {
            success = true,
            data = entity
        });
    }

    [HttpPut("document-types/{id:guid}")]
    public async Task<IActionResult> UpdateDocumentType(
        Guid id,
        [FromBody] DocumentTypeRequest request)
    {
        if (await RequireAdminAsync() is IActionResult denied)
            return denied;

        var entity = await _db.DocumentTypes
            .FirstOrDefaultAsync(x =>
                x.DocumentTypeId == id &&
                !x.IsDeleted);

        if (entity == null)
        {
            return NotFound(new
            {
                success = false,
                message = "Jenis dokumen tidak ditemukan."
            });
        }

        if (string.IsNullOrWhiteSpace(request.DocumentTypeCode) ||
            string.IsNullOrWhiteSpace(request.DocumentTypeName))
        {
            return BadRequest(new
            {
                success = false,
                message = "Kode dan nama jenis dokumen wajib diisi."
            });
        }

        var code = request.DocumentTypeCode.Trim().ToUpperInvariant();
        var name = request.DocumentTypeName.Trim();

        var duplicate = await _db.DocumentTypes.AnyAsync(x =>
            x.DocumentTypeId != id &&
            (x.DocumentTypeCode == code ||
             x.DocumentTypeName == name) &&
            !x.IsDeleted);

        if (duplicate)
        {
            return Conflict(new
            {
                success = false,
                message = "Kode atau nama jenis dokumen sudah digunakan."
            });
        }

        entity.DocumentTypeCode = code;
        entity.DocumentTypeName = name;
        entity.Description = request.Description?.Trim();
        entity.IsActive = request.IsActive;
        entity.UpdatedBy = AuditUser;
        entity.UpdatedDt = DateTime.Now;

        await _db.SaveChangesAsync();

        return Ok(new
        {
            success = true,
            message = "Jenis dokumen diperbarui."
        });
    }

    public record TemplateRequest(
        Guid ProjectTypeId,
        string TemplateName,
        string? Description,
        int VersionNo = 1);

    public record TemplateItemRequest(
        string DocumentName,
        string? Description,
        bool IsRequired,
        Guid? SDLPhaseId,
        Guid? DocumentTypeId);

    public record PhaseRequest(
        string SDLPhaseName,
        string? Description,
        bool IsActive = true);

    public record DocumentTypeRequest(
        string DocumentTypeCode,
        string DocumentTypeName,
        string? Description,
        bool IsActive = true);

    public record StatusRequest(bool IsActive);

    public record ReorderRequest(List<Guid> Ids);
}