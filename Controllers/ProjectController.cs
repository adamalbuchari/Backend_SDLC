
using System.Security.Claims;
using DocuFlow.Api.Data;
using DocuFlow.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DocuFlow.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProjectsController : ControllerBase
{
    private readonly AppDbContext _context;

    public ProjectsController(AppDbContext context)
    {
        _context = context;
    }

    private Guid CurrentUserId =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id
            : Guid.Empty;

    private bool HasPermission(string permission) =>
        User.Claims.Any(x =>
            x.Type == "permission" &&
            x.Value.Equals(permission, StringComparison.OrdinalIgnoreCase));

    private async Task<bool> IsSystemAdministratorAsync()
    {
        if (CurrentUserId == Guid.Empty) return false;

        return await (
            from ur in _context.UserRoles
            join r in _context.Roles on ur.RoleId equals r.RoleId
            where ur.UserId == CurrentUserId &&
                  ur.IsActive && !ur.IsDeleted &&
                  r.RoleName == "System Administrator" &&
                  r.IsActive && !r.IsDeleted
            select ur.UserId
        ).AnyAsync()
        || await _context.Users.AnyAsync(x =>
            x.UserId == CurrentUserId &&
            x.Role != null &&
            x.Role.RoleName == "System Administrator" &&
            x.IsActive && !x.IsDeleted);
    }

    private async Task<bool> IsProjectMemberAsync(Guid projectId)
    {
        return await _context.ProjectMembers.AnyAsync(x =>
            x.ProjectId == projectId &&
            x.UserId == CurrentUserId &&
            x.IsActive &&
            !x.IsDeleted);
    }

    private async Task<bool> CanAccessProjectAsync(Guid projectId)
    {
        if (await IsSystemAdministratorAsync())
            return true;

        return await IsProjectMemberAsync(projectId);
    }

    private async Task<bool> CanManageMembersAsync(Guid projectId)
    {
        if (await IsSystemAdministratorAsync())
            return true;

        if (!HasPermission("PROJECT_MEMBER_MANAGE"))
            return false;

        return await (
            from pm in _context.ProjectMembers
            join pr in _context.ProjectRoles
                on pm.ProjectRoleId equals pr.ProjectRoleId
            where pm.ProjectId == projectId &&
                  pm.UserId == CurrentUserId &&
                  pm.IsActive && !pm.IsDeleted &&
                  pr.ProjectRoleName == "Project Manager" &&
                  pr.IsActive && !pr.IsDeleted
            select pm.ProjectMemberId
        ).AnyAsync();
    }

    private async Task AddOrReactivateMemberAsync(
        Guid projectId,
        Guid userId,
        Guid projectRoleId,
        string auditUser)
    {
        var member = await _context.ProjectMembers
            .FirstOrDefaultAsync(x =>
                x.ProjectId == projectId &&
                x.UserId == userId);

        if (member == null)
        {
            _context.ProjectMembers.Add(new ProjectMember
            {
                ProjectMemberId = Guid.NewGuid(),
                ProjectId = projectId,
                UserId = userId,
                ProjectRoleId = projectRoleId,
                IsActive = true,
                CreatedBy = auditUser,
                CreatedDt = DateTime.Now,
                IsDeleted = false
            });

            return;
        }

        member.ProjectRoleId = projectRoleId;
        member.IsActive = true;
        member.IsDeleted = false;
        member.DeletedBy = null;
        member.DeletedDt = null;
        member.UpdatedBy = auditUser;
        member.UpdatedDt = DateTime.Now;
    }

    // =========================================
    // GET PROJECT LIST
    // =========================================

    [HttpGet]
    public async Task<IActionResult> GetProjects()
    {
        var isAdmin = await IsSystemAdministratorAsync();

        var query = _context.Projects
            .AsNoTracking()
            .Where(x => !x.IsDeleted);

        if (!isAdmin)
        {
            query = query.Where(p =>
                _context.ProjectMembers.Any(m =>
                    m.ProjectId == p.ProjectId &&
                    m.UserId == CurrentUserId &&
                    m.IsActive &&
                    !m.IsDeleted));
        }

        var projectData = await query
            .OrderByDescending(x => x.CreatedDt)
            .Select(x => new
            {
                x.ProjectId,
                x.ProjectCode,
                x.ProjectName,
                x.Description,
                x.StartDate,
                x.EndDate,
                ProjectType = x.ProjectType!.ProjectTypeName,
                ProjectStatus = x.ProjectStatus!.ProjectStatusName,
                ProjectManager = x.ProjectManager!.FullName,

                RequiredDocumentCount =
                    _context.ProjectDocumentRequirements.Count(r =>
                        r.ProjectId == x.ProjectId &&
                        r.IsActive &&
                        !r.IsDeleted &&
                        r.IsRequired),

                UploadedRequiredDocumentCount =
                    _context.ProjectDocumentRequirements.Count(r =>
                        r.ProjectId == x.ProjectId &&
                        r.IsActive &&
                        !r.IsDeleted &&
                        r.IsRequired &&
                        _context.Documents.Any(d =>
                            d.ProjectDocumentRequirementId ==
                                r.ProjectDocumentRequirementId &&
                            d.ProjectId == x.ProjectId &&
                            !d.IsDeleted)),

                UploadedDocumentCount =
                    _context.Documents.Count(d =>
                        d.ProjectId == x.ProjectId &&
                        !d.IsDeleted)
            })
            .ToListAsync();

        var projects = projectData.Select(x => new
        {
            x.ProjectId,
            x.ProjectCode,
            x.ProjectName,
            x.Description,
            x.StartDate,
            x.EndDate,
            x.ProjectType,
            x.ProjectStatus,
            x.ProjectManager,
            x.RequiredDocumentCount,
            x.UploadedRequiredDocumentCount,
            x.UploadedDocumentCount,

            ProgressPercentage =
                x.RequiredDocumentCount == 0
                    ? 0
                    : (int)Math.Round(
                        (double)x.UploadedRequiredDocumentCount /
                        x.RequiredDocumentCount * 100,
                        MidpointRounding.AwayFromZero)
        }).ToList();

        return Ok(new { success = true, data = projects });
    }

    // =========================================
    // PROJECT FORM DATA
    // =========================================
    [HttpGet("form-data")]
    public async Task<IActionResult> GetFormData()
    {
        var projectTypes = await _context.ProjectTypes
            .AsNoTracking()
            .Where(x => x.IsActive && !x.IsDeleted)
            .Select(x => new
            {
                x.ProjectTypeId,
                x.ProjectTypeName
            })
            .ToListAsync();

        var projectStatuses = await _context.ProjectStatuses
            .AsNoTracking()
            .Where(x => x.IsActive && !x.IsDeleted)
            .Select(x => new
            {
                x.ProjectStatusId,
                x.ProjectStatusName
            })
            .ToListAsync();

        var projectManagers = await _context.Users
            .AsNoTracking()
            .Where(x => x.IsActive && !x.IsDeleted)
            .Select(x => new
            {
                x.UserId,
                x.FullName
            })
            .ToListAsync();

        var documentTemplates = await _context.DocumentTemplates
            .AsNoTracking()
            .Where(x => x.IsActive && !x.IsDeleted)
            .Select(x => new
            {
                x.DocumentTemplateId,
                x.ProjectTypeId,
                x.TemplateName,
                x.VersionNo
            })
            .OrderBy(x => x.TemplateName)
            .ToListAsync();

        return Ok(new
        {
            success = true,
            projectTypes,
            projectStatuses,
            projectManagers,
            documentTemplates
        });
    }

    // =========================================
    // CREATE PROJECT
    // =========================================
    [HttpPost]
    public async Task<IActionResult> CreateProject(
        CreateProjectRequest request)
    {
        if (!HasPermission("PROJECT_CREATE"))
            return Forbid();

        if (CurrentUserId == Guid.Empty)
            return Unauthorized();

        if (string.IsNullOrWhiteSpace(request.ProjectCode) ||
            string.IsNullOrWhiteSpace(request.ProjectName))
        {
            return BadRequest(new
            {
                success = false,
                message = "Kode dan nama proyek wajib diisi."
            });
        }

        if (request.EndDate < request.StartDate)
        {
            return BadRequest(new
            {
                success = false,
                message = "Tanggal selesai tidak boleh sebelum tanggal mulai."
            });
        }

        var duplicateCode = await _context.Projects.AnyAsync(x =>
            x.ProjectCode == request.ProjectCode.Trim() &&
            !x.IsDeleted);

        if (duplicateCode)
        {
            return Conflict(new
            {
                success = false,
                message = "Kode proyek sudah digunakan."
            });
        }

        var projectType = await _context.ProjectTypes
            .FirstOrDefaultAsync(x =>
                x.ProjectTypeId == request.ProjectTypeId &&
                x.IsActive && !x.IsDeleted);

        var projectStatus = await _context.ProjectStatuses
            .FirstOrDefaultAsync(x =>
                x.ProjectStatusId == request.ProjectStatusId &&
                x.IsActive && !x.IsDeleted);

        var projectManager = await _context.Users
            .FirstOrDefaultAsync(x =>
                x.UserId == request.ProjectManagerId &&
                x.IsActive && !x.IsDeleted);

        if (projectType == null ||
            projectStatus == null ||
            projectManager == null)
        {
            return BadRequest(new
            {
                success = false,
                message = "Jenis proyek, status, atau project manager tidak valid."
            });
        }

        var documentTemplate = await _context.DocumentTemplates
            .FirstOrDefaultAsync(x =>
                x.DocumentTemplateId == request.DocumentTemplateId &&
                x.ProjectTypeId == request.ProjectTypeId &&
                x.IsActive && !x.IsDeleted);

        if (documentTemplate == null)
        {
            return BadRequest(new
            {
                success = false,
                message = "Document template tidak valid atau tidak sesuai dengan jenis proyek."
            });
        }

        var templateItems = await _context.DocumentTemplateItems
            .Where(x =>
                x.DocumentTemplateId == request.DocumentTemplateId &&
                x.IsActive && !x.IsDeleted)
            .OrderBy(x => x.SequenceNo)
            .ToListAsync();

        if (templateItems.Count == 0)
        {
            return BadRequest(new
            {
                success = false,
                message = "Document template belum memiliki item dokumen aktif."
            });
        }

        if (templateItems.Any(x =>
            x.DocumentTypeId == null || x.SDLPhaseId == null))
        {
            return BadRequest(new
            {
                success = false,
                message = "Terdapat item template yang belum memiliki Document Type atau SDLC Phase."
            });
        }

        var projectManagerRole = await _context.ProjectRoles
            .FirstOrDefaultAsync(x =>
                x.ProjectRoleName == "Project Manager" &&
                x.IsActive && !x.IsDeleted);

        if (projectManagerRole == null)
        {
            return BadRequest(new
            {
                success = false,
                message = "Project Role 'Project Manager' belum tersedia."
            });
        }

        var project = new Project
        {
            ProjectId = Guid.NewGuid(),
            ProjectCode = request.ProjectCode.Trim(),
            ProjectName = request.ProjectName.Trim(),
            Description = request.Description?.Trim(),
            ProjectTypeId = request.ProjectTypeId,
            ProjectStatusId = request.ProjectStatusId,
            ProjectManagerId = request.ProjectManagerId,
            DocumentTemplateId = request.DocumentTemplateId,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            CreatedBy = CurrentUserId.ToString(),
            CreatedDt = DateTime.Now,
            IsDeleted = false
        };

        await using var transaction =
            await _context.Database.BeginTransactionAsync();

        try
        {
            _context.Projects.Add(project);
            await _context.SaveChangesAsync();

            var requirements = templateItems.Select(item =>
                new ProjectDocumentRequirement
                {
                    ProjectDocumentRequirementId = Guid.NewGuid(),
                    ProjectId = project.ProjectId,
                    DocumentTemplateItemId = item.DocumentTemplateItemId,
                    DocumentTypeId = item.DocumentTypeId!.Value,
                    DocumentName = item.DocumentName,
                    SDLPhaseId = item.SDLPhaseId!.Value,
                    IsRequired = item.IsRequired,
                    SequenceNo = item.SequenceNo,
                    IsActive = true,
                    CreatedBy = CurrentUserId.ToString(),
                    CreatedDt = DateTime.Now,
                    IsDeleted = false
                }).ToList();

            _context.ProjectDocumentRequirements.AddRange(requirements);

            await AddOrReactivateMemberAsync(
                project.ProjectId,
                request.ProjectManagerId,
                projectManagerRole.ProjectRoleId,
                CurrentUserId.ToString());

            // Pembuat proyek juga menjadi anggota apabila berbeda
            // dari Project Manager yang ditunjuk.
            if (CurrentUserId != request.ProjectManagerId)
            {
                await AddOrReactivateMemberAsync(
                    project.ProjectId,
                    CurrentUserId,
                    projectManagerRole.ProjectRoleId,
                    CurrentUserId.ToString());
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }

        return CreatedAtAction(
            nameof(GetProjects),
            new { id = project.ProjectId },
            new
            {
                success = true,
                message = "Proyek berhasil dibuat.",
                projectId = project.ProjectId
            });
    }

    // =========================================
    // GET PROJECT MEMBERS
    // =========================================
    [HttpGet("{projectId:guid}/members")]
    public async Task<IActionResult> GetProjectMembers(Guid projectId)
    {
        var projectExists = await _context.Projects.AnyAsync(x =>
            x.ProjectId == projectId && !x.IsDeleted);

        if (!projectExists)
            return NotFound(new { success = false, message = "Proyek tidak ditemukan." });

        if (!await CanAccessProjectAsync(projectId))
            return Forbid();

        var members = await (
            from pm in _context.ProjectMembers.AsNoTracking()
            join u in _context.Users on pm.UserId equals u.UserId
            join pr in _context.ProjectRoles on pm.ProjectRoleId equals pr.ProjectRoleId
            where pm.ProjectId == projectId &&
                  pm.IsActive && !pm.IsDeleted &&
                  u.IsActive && !u.IsDeleted &&
                  pr.IsActive && !pr.IsDeleted
            orderby pr.ProjectRoleName, u.FullName
            select new
            {
                pm.ProjectMemberId,
                pm.UserId,
                u.FullName,
                u.Email,
                u.Department,
                pm.ProjectRoleId,
                pr.ProjectRoleName,
                pm.CreatedDt
            }
        ).ToListAsync();

        return Ok(new { success = true, data = members });
    }

    // =========================================
    // PROJECT MEMBER FORM DATA
    // =========================================
    [HttpGet("member-form-data")]
    public async Task<IActionResult> GetMemberFormData()
    {
        if (!await IsSystemAdministratorAsync())
            return Forbid();

        var users = await _context.Users
            .AsNoTracking()
            .Where(x => x.IsActive && !x.IsDeleted)
            .OrderBy(x => x.FullName)
            .Select(x => new
            {
                x.UserId,
                x.FullName,
                x.Email,
                x.Department
            })
            .ToListAsync();

        var projectRoles = await _context.ProjectRoles
            .AsNoTracking()
            .Where(x => x.IsActive && !x.IsDeleted)
            .OrderBy(x => x.ProjectRoleName)
            .Select(x => new
            {
                x.ProjectRoleId,
                x.ProjectRoleName,
                x.Description
            })
            .ToListAsync();

        return Ok(new
        {
            success = true,
            data = new
            {
                users,
                projectRoles
            }
        });
    }

    // =========================================
    // ADD PROJECT MEMBER
    // =========================================
    [HttpPost("{projectId:guid}/members")]
    public async Task<IActionResult> AddProjectMember(
        Guid projectId,
        AddProjectMemberRequest request)
    {
        if (!await CanManageMembersAsync(projectId))
            return Forbid();

        var projectExists = await _context.Projects.AnyAsync(x =>
            x.ProjectId == projectId && !x.IsDeleted);

        if (!projectExists)
            return NotFound(new { success = false, message = "Proyek tidak ditemukan." });

        var user = await _context.Users.FirstOrDefaultAsync(x =>
            x.UserId == request.UserId &&
            x.IsActive && !x.IsDeleted);

        if (user == null)
        {
            return BadRequest(new
            {
                success = false,
                message = "Pengguna tidak ditemukan atau tidak aktif."
            });
        }

        var projectRole = await _context.ProjectRoles.FirstOrDefaultAsync(x =>
            x.ProjectRoleId == request.ProjectRoleId &&
            x.IsActive && !x.IsDeleted);

        if (projectRole == null)
        {
            return BadRequest(new
            {
                success = false,
                message = "Project Role tidak valid atau tidak aktif."
            });
        }

        var existing = await _context.ProjectMembers
            .FirstOrDefaultAsync(x =>
                x.ProjectId == projectId &&
                x.UserId == request.UserId);

        if (existing != null && existing.IsActive && !existing.IsDeleted)
        {
            return Conflict(new
            {
                success = false,
                message = "Pengguna sudah menjadi anggota proyek."
            });
        }

        await AddOrReactivateMemberAsync(
            projectId,
            request.UserId,
            request.ProjectRoleId,
            CurrentUserId.ToString());

        await _context.SaveChangesAsync();

        return Ok(new
        {
            success = true,
            message = "Anggota proyek berhasil ditambahkan."
        });
    }

    // =========================================
    // UPDATE PROJECT MEMBER ROLE
    // =========================================
    [HttpPut("{projectId:guid}/members/{userId:guid}")]
    public async Task<IActionResult> UpdateProjectMember(
        Guid projectId,
        Guid userId,
        UpdateProjectMemberRequest request)
    {
        if (!await CanManageMembersAsync(projectId))
            return Forbid();

        var member = await _context.ProjectMembers.FirstOrDefaultAsync(x =>
            x.ProjectId == projectId &&
            x.UserId == userId &&
            x.IsActive && !x.IsDeleted);

        if (member == null)
            return NotFound(new { success = false, message = "Anggota proyek tidak ditemukan." });

        var newRole = await _context.ProjectRoles.FirstOrDefaultAsync(x =>
            x.ProjectRoleId == request.ProjectRoleId &&
            x.IsActive && !x.IsDeleted);

        if (newRole == null)
        {
            return BadRequest(new
            {
                success = false,
                message = "Project Role tidak valid atau tidak aktif."
            });
        }

        var oldRole = await _context.ProjectRoles
            .FirstOrDefaultAsync(x => x.ProjectRoleId == member.ProjectRoleId);

        if (oldRole?.ProjectRoleName == "Project Manager" &&
            newRole.ProjectRoleName != "Project Manager")
        {
            var otherManagers = await (
                from pm in _context.ProjectMembers
                join pr in _context.ProjectRoles
                    on pm.ProjectRoleId equals pr.ProjectRoleId
                where pm.ProjectId == projectId &&
                      pm.UserId != userId &&
                      pm.IsActive && !pm.IsDeleted &&
                      pr.ProjectRoleName == "Project Manager" &&
                      pr.IsActive && !pr.IsDeleted
                select pm.ProjectMemberId
            ).CountAsync();

            if (otherManagers == 0)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Proyek harus memiliki minimal satu anggota dengan Project Role Project Manager."
                });
            }
        }

        member.ProjectRoleId = request.ProjectRoleId;
        member.UpdatedBy = CurrentUserId.ToString();
        member.UpdatedDt = DateTime.Now;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            success = true,
            message = "Project Role anggota berhasil diperbarui."
        });
    }

    // =========================================
    // REMOVE PROJECT MEMBER (SOFT DELETE)
    // =========================================
    [HttpDelete("{projectId:guid}/members/{userId:guid}")]
    public async Task<IActionResult> RemoveProjectMember(
        Guid projectId,
        Guid userId)
    {
        if (!await CanManageMembersAsync(projectId))
            return Forbid();

        var member = await _context.ProjectMembers.FirstOrDefaultAsync(x =>
            x.ProjectId == projectId &&
            x.UserId == userId &&
            x.IsActive && !x.IsDeleted);

        if (member == null)
            return NotFound(new { success = false, message = "Anggota proyek tidak ditemukan." });

        var role = await _context.ProjectRoles
            .FirstOrDefaultAsync(x => x.ProjectRoleId == member.ProjectRoleId);

        if (role?.ProjectRoleName == "Project Manager")
        {
            var otherManagers = await (
                from pm in _context.ProjectMembers
                join pr in _context.ProjectRoles
                    on pm.ProjectRoleId equals pr.ProjectRoleId
                where pm.ProjectId == projectId &&
                      pm.UserId != userId &&
                      pm.IsActive && !pm.IsDeleted &&
                      pr.ProjectRoleName == "Project Manager" &&
                      pr.IsActive && !pr.IsDeleted
                select pm.ProjectMemberId
            ).CountAsync();

            if (otherManagers == 0)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Project Manager terakhir tidak dapat dikeluarkan dari proyek."
                });
            }
        }

        member.IsActive = false;
        member.IsDeleted = true;
        member.DeletedBy = CurrentUserId.ToString();
        member.DeletedDt = DateTime.Now;
        member.UpdatedBy = CurrentUserId.ToString();
        member.UpdatedDt = DateTime.Now;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            success = true,
            message = "Anggota berhasil dikeluarkan dari proyek."
        });
    }
}

// =========================================
// REQUEST MODELS
// =========================================
public class CreateProjectRequest
{
    public string ProjectCode { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public string? Description { get; set; }

    public Guid ProjectTypeId { get; set; }
    public Guid ProjectStatusId { get; set; }
    public Guid ProjectManagerId { get; set; }
    public Guid DocumentTemplateId { get; set; }

    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
}

public class AddProjectMemberRequest
{
    public Guid UserId { get; set; }
    public Guid ProjectRoleId { get; set; }
}

public class UpdateProjectMemberRequest
{
    public Guid ProjectRoleId { get; set; }
}
