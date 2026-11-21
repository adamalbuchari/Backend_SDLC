
using System.Security.Claims;
using DocuFlow.Api.Data;
using DocuFlow.Api.DTOs.ReviewApproval;
using DocuFlow.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DocuFlow.Api.Controllers;

[ApiController]
[Route("api/review-approvals")]
[Authorize]
public class ReviewApprovalController : ControllerBase
{
    private readonly AppDbContext _db;

    public ReviewApprovalController(AppDbContext db)
    {
        _db = db;
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

    private async Task<bool> IsSystemAdministratorAsync(Guid userId)
    {
        return await (
            from ur in _db.UserRoles
            join role in _db.Roles on ur.RoleId equals role.RoleId
            where ur.UserId == userId
                  && ur.IsActive
                  && !ur.IsDeleted
                  && role.IsActive
                  && !role.IsDeleted
                  && role.RoleName == "System Administrator"
            select ur
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

    private async Task AddHistoryAsync(
        DocumentWorkflow workflow,
        Guid? stageId,
        string action,
        string? previousStatus,
        string? newStatus,
        string? comment)
    {
        _db.DocumentWorkflowHistories.Add(
            new DocumentWorkflowHistory
            {
                DocumentWorkflowHistoryId = Guid.NewGuid(),
                DocumentWorkflowId = workflow.DocumentWorkflowId,
                ApprovalWorkflowStageId = stageId,
                ActorUserId = ActorId!.Value,
                Action = action,
                PreviousStatus = previousStatus,
                NewStatus = newStatus,
                Comment = comment,
                ActionAt = DateTime.UtcNow
            });
    }

    // =========================================
    // GET: api/review-approvals/configuration
    // =========================================
    [HttpGet("configuration")]
    [Authorize(Policy = "DOCUMENT_VIEW")]
    public async Task<IActionResult> GetConfiguration(
        [FromQuery] Guid projectId,
        [FromQuery] Guid documentTypeId)
    {
        if (ActorId == null)
            return Unauthorized();

        if (projectId == Guid.Empty ||
            documentTypeId == Guid.Empty)
        {
            return BadRequest(new
            {
                success = false,
                message = "Project dan Document Type wajib dipilih."
            });
        }

        if (!await CanAccessProjectAsync(projectId, ActorId.Value))
            return Forbid();

        // Workflow global maupun khusus project/type.
        var workflows = await _db.ApprovalWorkflows
            .AsNoTracking()
            .Include(x => x.Stages)
            .Where(x =>
                x.IsActive &&
                (x.ProjectId == null || x.ProjectId == projectId) &&
                (x.DocumentTypeId == null ||
                 x.DocumentTypeId == documentTypeId))
            .OrderByDescending(x => x.ProjectId == projectId)
            .ThenByDescending(x => x.DocumentTypeId == documentTypeId)
            .ThenBy(x => x.WorkflowName)
            .ToListAsync();

        var workflowData = workflows.Select(w => new
        {
            w.ApprovalWorkflowId,
            w.WorkflowName,
            w.Description,
            w.ProjectId,
            w.DocumentTypeId,
            Stages = w.Stages
                .OrderBy(s => s.SequenceNo)
                .Select(s => new
                {
                    s.ApprovalWorkflowStageId,
                    s.StageName,
                    s.StageType,
                    s.SequenceNo,
                    s.ApprovalMode,
                    s.ApprovalRule,
                    s.IsRequired
                })
                .ToList()
        }).ToList();

        // Kandidat harus menjadi anggota project.
        var projectMemberIds = await _db.ProjectMembers
            .AsNoTracking()
            .Where(x =>
                x.ProjectId == projectId &&
                x.IsActive &&
                !x.IsDeleted)
            .Select(x => x.UserId)
            .Distinct()
            .ToListAsync();

        // Ambil role aktif dari UserRoles.
        var userRoles = await (
            from ur in _db.UserRoles.AsNoTracking()
            join role in _db.Roles.AsNoTracking()
                on ur.RoleId equals role.RoleId
            where projectMemberIds.Contains(ur.UserId)
                  && ur.IsActive
                  && !ur.IsDeleted
                  && role.IsActive
                  && !role.IsDeleted
                  && (role.RoleName == "Reviewer" ||
                      role.RoleName == "Approver")
            select new
            {
                ur.UserId,
                role.RoleName
            }
        ).ToListAsync();

        var candidateUserIds = userRoles
            .Select(x => x.UserId)
            .Distinct()
            .ToList();

        var candidateUsers = await _db.Users
            .AsNoTracking()
            .Where(x =>
                candidateUserIds.Contains(x.UserId) &&
                x.IsActive &&
                !x.IsDeleted)
            .OrderBy(x => x.FullName)
            .Select(x => new
            {
                x.UserId,
                x.FullName,
                x.Email,
                x.Department
            })
            .ToListAsync();

        var reviewers = candidateUsers
            .Where(u => userRoles.Any(r =>
                r.UserId == u.UserId &&
                r.RoleName == "Reviewer"))
            .Select(u => new
            {
                u.UserId,
                u.FullName,
                u.Email,
                u.Department
            })
            .ToList();

        var approvers = candidateUsers
            .Where(u => userRoles.Any(r =>
                r.UserId == u.UserId &&
                r.RoleName == "Approver"))
            .Select(u => new
            {
                u.UserId,
                u.FullName,
                u.Email,
                u.Department
            })
            .ToList();

        return Ok(new
        {
            success = true,
            data = new
            {
                projectId,
                documentTypeId,
                workflows = workflowData,
                reviewers,
                approvers
            }
        });
    }

    // =========================================
    // POST: api/review-approvals/submit
    // =========================================
    [HttpPost("submit")]
    [Authorize(Policy = "DOCUMENT_VIEW")]
    public async Task<IActionResult> Submit(
        [FromBody] SubmitDocumentRequest request)
    {
        if (ActorId == null)
            return Unauthorized();

        if (request.DocumentId == Guid.Empty ||
            request.ApprovalWorkflowId == Guid.Empty)
            return BadRequest(new
            {
                message = "Document dan workflow wajib dipilih."
            });

        var document = await _db.Documents.FirstOrDefaultAsync(x =>
            x.DocumentId == request.DocumentId &&
            !x.IsDeleted);

        if (document == null)
            return NotFound(new { message = "Dokumen tidak ditemukan." });

        if (!await CanAccessProjectAsync(document.ProjectId, ActorId.Value))
            return Forbid();

        if (document.DocumentOwnerId != ActorId.Value &&
            !await IsSystemAdministratorAsync(ActorId.Value))
            return Forbid();

        if (document.Status != "Draft")
            return Conflict(new
            {
                message = "Hanya dokumen berstatus Draft yang dapat disubmit."
            });

        var activeWorkflow = await _db.ApprovalWorkflows
            .Include(x => x.Stages)
            .FirstOrDefaultAsync(x =>
                x.ApprovalWorkflowId == request.ApprovalWorkflowId &&
                x.IsActive &&
                (x.ProjectId == null || x.ProjectId == document.ProjectId) &&
                (x.DocumentTypeId == null ||
                 x.DocumentTypeId == document.DocumentTypeId));

        if (activeWorkflow == null)
            return BadRequest(new
            {
                message = "Workflow aktif yang sesuai tidak ditemukan."
            });

        var stages = activeWorkflow.Stages
            .OrderBy(x => x.SequenceNo)
            .ToList();

        if (stages.Count == 0)
            return BadRequest(new
            {
                message = "Workflow belum memiliki tahapan."
            });

        if (request.Assignments == null ||
            request.Assignments.Count == 0)
            return BadRequest(new
            {
                message = "Penugasan reviewer dan approver wajib diisi."
            });

        foreach (var stage in stages)
        {
            var stageAssignments = request.Assignments
                .Where(x => x.ApprovalWorkflowStageId ==
                            stage.ApprovalWorkflowStageId)
                .ToList();

            if (stage.IsRequired && stageAssignments.Count == 0)
                return BadRequest(new
                {
                    message = $"Penugasan untuk tahap {stage.StageName} wajib diisi."
                });

            var expectedType = stage.StageType.Equals(
                "Approval",
                StringComparison.OrdinalIgnoreCase)
                ? "Approver"
                : "Reviewer";

            if (stageAssignments.Any(x =>
                !x.AssignmentType.Equals(
                    expectedType,
                    StringComparison.OrdinalIgnoreCase)))
                return BadRequest(new
                {
                    message = $"Jenis penugasan tahap {stage.StageName} harus {expectedType}."
                });
        }

        if (request.Assignments.Any(x =>
            !stages.Any(s =>
                s.ApprovalWorkflowStageId == x.ApprovalWorkflowStageId)))
            return BadRequest(new
            {
                message = "Ada penugasan yang bukan bagian dari workflow."
            });

        var assignedUserIds = request.Assignments
            .Select(x => x.AssignedUserId)
            .Distinct()
            .ToList();

        var validUserIds = await _db.Users
            .Where(x =>
                assignedUserIds.Contains(x.UserId) &&
                x.IsActive &&
                !x.IsDeleted)
            .Select(x => x.UserId)
            .ToListAsync();

        if (validUserIds.Count != assignedUserIds.Count)
            return BadRequest(new
            {
                message = "Ada reviewer atau approver yang tidak aktif atau tidak valid."
            });

        // Pastikan user yang ditugaskan mempunyai role yang sesuai
        // dan merupakan anggota project.
        var validAssignments = await (
            from ur in _db.UserRoles
            join role in _db.Roles on ur.RoleId equals role.RoleId
            join member in _db.ProjectMembers
                on ur.UserId equals member.UserId
            where assignedUserIds.Contains(ur.UserId)
                  && member.ProjectId == document.ProjectId
                  && member.IsActive
                  && !member.IsDeleted
                  && ur.IsActive
                  && !ur.IsDeleted
                  && role.IsActive
                  && !role.IsDeleted
            select new
            {
                ur.UserId,
                role.RoleName
            }
        ).ToListAsync();

        foreach (var item in request.Assignments)
        {
            var expectedRole = item.AssignmentType;

            if (!validAssignments.Any(x =>
                x.UserId == item.AssignedUserId &&
                x.RoleName == expectedRole))
            {
                return BadRequest(new
                {
                    message = $"User yang dipilih untuk tugas {expectedRole} tidak memiliki role aktif atau bukan anggota project."
                });
            }
        }

        var now = DateTime.UtcNow;

        await using var transaction =
            await _db.Database.BeginTransactionAsync();

        try
        {
            var firstStage = stages.First();

            var workflow = new DocumentWorkflow
            {
                DocumentWorkflowId = Guid.NewGuid(),
                DocumentId = document.DocumentId,
                ApprovalWorkflowId = activeWorkflow.ApprovalWorkflowId,
                WorkflowStatus = "Submitted",
                CurrentStageNo = firstStage.SequenceNo,
                SubmittedAt = now,
                CreatedBy = ActorName,
                CreatedDt = now
            };

            _db.DocumentWorkflows.Add(workflow);

            foreach (var item in request.Assignments)
            {
                var stage = stages.First(x =>
                    x.ApprovalWorkflowStageId ==
                    item.ApprovalWorkflowStageId);

                var isFirstStage = stage.SequenceNo == firstStage.SequenceNo;

                _db.DocumentWorkflowAssignments.Add(
                    new DocumentWorkflowAssignment
                    {
                        DocumentWorkflowAssignmentId = Guid.NewGuid(),
                        DocumentWorkflowId = workflow.DocumentWorkflowId,
                        ApprovalWorkflowStageId =
                            item.ApprovalWorkflowStageId,
                        AssignedUserId = item.AssignedUserId,
                        AssignmentType = item.AssignmentType,
                        AssignmentStatus = isFirstStage
                            ? "Pending"
                            : "Waiting",
                        AssignedAt = isFirstStage ? now : null,
                        DueDate = item.DueDate ?? request.DueDate
                    });
            }

            document.Status = "Submitted";
            document.ApprovalStatus = "Pending";
            document.UpdatedBy = ActorName;
            document.UpdatedDt = now;

            await AddHistoryAsync(
                workflow,
                firstStage.ApprovalWorkflowStageId,
                "Submitted",
                "Draft",
                "Submitted",
                request.Comment);

            await _db.SaveChangesAsync();
            await transaction.CommitAsync();

            return Ok(new
            {
                success = true,
                message = "Dokumen berhasil disubmit untuk review.",
                documentWorkflowId = workflow.DocumentWorkflowId,
                currentStage = firstStage.StageName
            });
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    // =========================================
    // GET: api/review-approvals/my-tasks
    // =========================================
    [HttpGet("my-tasks")]
    [Authorize]
    public async Task<IActionResult> MyTasks()
    {
        if (ActorId == null)
            return Unauthorized();

        var userId = ActorId.Value;

        var tasks = await _db.DocumentWorkflowAssignments
            .AsNoTracking()
            .Where(x =>
                x.AssignedUserId == userId &&
                (x.AssignmentStatus == "Pending" ||
                 x.AssignmentStatus == "Waiting"))
            .Select(x => new
            {
                x.DocumentWorkflowAssignmentId,
                x.AssignmentType,
                x.AssignmentStatus,
                x.AssignedAt,
                x.DueDate,
                StageName = x.ApprovalWorkflowStage!.StageName,
                StageType = x.ApprovalWorkflowStage.StageType,
                WorkflowStatus = x.DocumentWorkflow!.WorkflowStatus,
                x.DocumentWorkflow.Document!.DocumentId,
                x.DocumentWorkflow.Document.DocumentTitle,
                x.DocumentWorkflow.Document.ProjectId,
                x.DocumentWorkflow.Document.Status
            })
            .Where(x => x.Status != "Archived")
            .ToListAsync();

        var accessibleTasks = new List<object>();

        foreach (var task in tasks)
        {
            if (await CanAccessProjectAsync(task.ProjectId, userId))
                accessibleTasks.Add(task);
        }

        return Ok(new
        {
            success = true,
            data = accessibleTasks
        });
    }

    // =========================================
    // POST: api/review-approvals/{id}/review
    // =========================================
    [HttpPost("{id:guid}/review")]
    [Authorize(Policy = "DOCUMENT_REVIEW")]
    public async Task<IActionResult> Review(
        Guid id,
        [FromBody] ReviewDecisionRequest request)
    {
        if (ActorId == null)
            return Unauthorized();

        var decision = request.Decision.Trim();

        if (decision != "Complete" &&
            decision != "Revision Required")
            return BadRequest(new
            {
                message = "Keputusan review harus Complete atau Revision Required."
            });

        if (decision == "Revision Required" &&
            string.IsNullOrWhiteSpace(request.Comment))
            return BadRequest(new
            {
                message = "Komentar wajib diisi saat meminta revisi."
            });

        var assignment = await _db.DocumentWorkflowAssignments
            .Include(x => x.DocumentWorkflow)!
                .ThenInclude(x => x!.Document)
            .Include(x => x.ApprovalWorkflowStage)
            .FirstOrDefaultAsync(x =>
                x.DocumentWorkflowAssignmentId == id &&
                x.AssignedUserId == ActorId.Value &&
                x.AssignmentType == "Reviewer" &&
                x.AssignmentStatus == "Pending");

        if (assignment == null)
            return NotFound(new
            {
                message = "Tugas review aktif tidak ditemukan."
            });

        var workflow = assignment.DocumentWorkflow!;
        var document = workflow.Document!;
        var stage = assignment.ApprovalWorkflowStage!;

        if (!await CanAccessProjectAsync(document.ProjectId, ActorId.Value))
            return Forbid();

        if (stage.StageType != "Review" ||
            workflow.WorkflowStatus == "Revision Required" ||
            workflow.WorkflowStatus == "Approved" ||
            workflow.WorkflowStatus == "Rejected" ||
            workflow.WorkflowStatus == "Cancelled")
            return Conflict(new
            {
                message = "Workflow tidak berada pada tahap review aktif."
            });

        var now = DateTime.UtcNow;
        var previousStatus = workflow.WorkflowStatus;

        assignment.AssignmentStatus = decision == "Complete"
            ? "Completed"
            : "Revision Required";
        assignment.CompletedAt = now;

        if (decision == "Revision Required")
        {
            workflow.WorkflowStatus = "Revision Required";
            workflow.CompletedAt = now;

            document.Status = "Draft";
            document.ApprovalStatus = "Revision Required";

            await AddHistoryAsync(
                workflow,
                stage.ApprovalWorkflowStageId,
                "Revision Required",
                previousStatus,
                workflow.WorkflowStatus,
                request.Comment);
        }
        else
        {
            await AddHistoryAsync(
                workflow,
                stage.ApprovalWorkflowStageId,
                "Review Completed",
                previousStatus,
                previousStatus,
                request.Comment);

            await AdvanceIfStageCompletedAsync(
                workflow,
                stage,
                now);
        }

        document.UpdatedBy = ActorName;
        document.UpdatedDt = now;

        await _db.SaveChangesAsync();

        return Ok(new
        {
            success = true,
            message = decision == "Complete"
                ? "Review berhasil diselesaikan."
                : "Dokumen dikembalikan untuk revisi.",
            workflowStatus = workflow.WorkflowStatus
        });
    }

    // =========================================
    // POST: api/review-approvals/{id}/approve
    // =========================================
    [HttpPost("{id:guid}/approve")]
    [Authorize(Policy = "DOCUMENT_APPROVE")]
    public async Task<IActionResult> Approve(
        Guid id,
        [FromBody] ApprovalDecisionRequest request)
    {
        if (ActorId == null)
            return Unauthorized();

        var decision = request.Decision.Trim();

        if (decision != "Approve" && decision != "Reject")
            return BadRequest(new
            {
                message = "Keputusan harus Approve atau Reject."
            });

        if (decision == "Reject" &&
            string.IsNullOrWhiteSpace(request.Comment))
            return BadRequest(new
            {
                message = "Komentar wajib diisi saat menolak dokumen."
            });

        var assignment = await _db.DocumentWorkflowAssignments
            .Include(x => x.DocumentWorkflow)!
                .ThenInclude(x => x!.Document)
            .Include(x => x.ApprovalWorkflowStage)
            .FirstOrDefaultAsync(x =>
                x.DocumentWorkflowAssignmentId == id &&
                x.AssignedUserId == ActorId.Value &&
                x.AssignmentType == "Approver" &&
                x.AssignmentStatus == "Pending");

        if (assignment == null)
            return NotFound(new
            {
                message = "Tugas approval aktif tidak ditemukan."
            });

        var workflow = assignment.DocumentWorkflow!;
        var document = workflow.Document!;
        var stage = assignment.ApprovalWorkflowStage!;

        if (!await CanAccessProjectAsync(document.ProjectId, ActorId.Value))
            return Forbid();

        if (stage.StageType != "Approval" ||
            workflow.WorkflowStatus == "Revision Required" ||
            workflow.WorkflowStatus == "Approved" ||
            workflow.WorkflowStatus == "Rejected" ||
            workflow.WorkflowStatus == "Cancelled")
            return Conflict(new
            {
                message = "Workflow tidak berada pada tahap approval aktif."
            });

        var now = DateTime.UtcNow;
        var previousStatus = workflow.WorkflowStatus;

        assignment.AssignmentStatus = decision == "Approve"
            ? "Completed"
            : "Rejected";
        assignment.CompletedAt = now;

        if (decision == "Reject")
        {
            workflow.WorkflowStatus = "Rejected";
            workflow.CompletedAt = now;

            document.Status = "Rejected";
            document.ApprovalStatus = "Rejected";

            await AddHistoryAsync(
                workflow,
                stage.ApprovalWorkflowStageId,
                "Rejected",
                previousStatus,
                workflow.WorkflowStatus,
                request.Comment);
        }
        else
        {
            await AddHistoryAsync(
                workflow,
                stage.ApprovalWorkflowStageId,
                "Approved",
                previousStatus,
                previousStatus,
                request.Comment);

            await AdvanceIfStageCompletedAsync(
                workflow,
                stage,
                now);
        }

        document.UpdatedBy = ActorName;
        document.UpdatedDt = now;

        await _db.SaveChangesAsync();

        return Ok(new
        {
            success = true,
            message = decision == "Approve"
                ? "Approval berhasil diberikan."
                : "Dokumen berhasil ditolak.",
            workflowStatus = workflow.WorkflowStatus
        });
    }

    // =========================================
    // GET: api/review-approvals/documents/{documentId}/history
    // =========================================
    [HttpGet("documents/{documentId:guid}/history")]
    [Authorize(Policy = "DOCUMENT_VIEW")]
    public async Task<IActionResult> GetHistory(Guid documentId)
    {
        if (ActorId == null)
            return Unauthorized();

        var workflow = await _db.DocumentWorkflows
            .AsNoTracking()
            .Include(x => x.Document)
            .Where(x => x.DocumentId == documentId)
            .OrderByDescending(x => x.SubmittedAt)
            .FirstOrDefaultAsync();

        if (workflow == null)
            return NotFound(new
            {
                message = "Riwayat workflow belum tersedia."
            });

        if (!await CanAccessProjectAsync(
            workflow.Document!.ProjectId,
            ActorId.Value))
            return Forbid();

        var history = await _db.DocumentWorkflowHistories
            .AsNoTracking()
            .Where(x => x.DocumentWorkflowId ==
                        workflow.DocumentWorkflowId)
            .OrderBy(x => x.ActionAt)
            .Select(x => new
            {
                x.DocumentWorkflowHistoryId,
                x.Action,
                x.PreviousStatus,
                x.NewStatus,
                x.Comment,
                x.ActionAt,
                x.ActorUserId,
                ActorName = x.ActorUser!.FullName,
                StageName = x.ApprovalWorkflowStage != null
                    ? x.ApprovalWorkflowStage.StageName
                    : null
            })
            .ToListAsync();

        return Ok(new
        {
            success = true,
            data = history
        });
    }

    // =========================================
    // ADVANCE WORKFLOW STAGE
    // =========================================
    private async Task AdvanceIfStageCompletedAsync(
        DocumentWorkflow workflow,
        ApprovalWorkflowStage currentStage,
        DateTime now)
    {
        var assignments = await _db.DocumentWorkflowAssignments
            .Where(x =>
                x.DocumentWorkflowId == workflow.DocumentWorkflowId &&
                x.ApprovalWorkflowStageId ==
                    currentStage.ApprovalWorkflowStageId)
            .ToListAsync();

        var completedCount = assignments.Count(x =>
            x.AssignmentStatus == "Completed");

        var requiredCount = assignments.Count;

        var stageCompleted =
            currentStage.ApprovalRule == "Any"
                ? completedCount >= 1
                : completedCount == requiredCount;

        if (!stageCompleted)
            return;

        var nextStage = await _db.ApprovalWorkflowStages
            .Where(x =>
                x.ApprovalWorkflowId ==
                    workflow.ApprovalWorkflowId &&
                x.SequenceNo > currentStage.SequenceNo)
            .OrderBy(x => x.SequenceNo)
            .FirstOrDefaultAsync();

        if (nextStage == null)
        {
            workflow.WorkflowStatus = "Approved";
            workflow.CompletedAt = now;

            var document = await _db.Documents
                .FirstAsync(x => x.DocumentId == workflow.DocumentId);

            document.Status = "Approved";
            document.ApprovalStatus = "Approved";

            await AddHistoryAsync(
                workflow,
                currentStage.ApprovalWorkflowStageId,
                "Workflow Completed",
                currentStage.StageName,
                "Approved",
                "Seluruh tahapan workflow telah selesai.");

            return;
        }

        workflow.CurrentStageNo = nextStage.SequenceNo;
        workflow.WorkflowStatus = nextStage.StageType == "Approval"
            ? "Pending Approval"
            : "Under Review";

        var nextAssignments = await _db.DocumentWorkflowAssignments
            .Where(x =>
                x.DocumentWorkflowId == workflow.DocumentWorkflowId &&
                x.ApprovalWorkflowStageId ==
                    nextStage.ApprovalWorkflowStageId)
            .ToListAsync();

        foreach (var item in nextAssignments)
        {
            if (item.AssignmentStatus == "Waiting")
            {
                item.AssignmentStatus = "Pending";
                item.AssignedAt = now;
            }
        }

        await AddHistoryAsync(
            workflow,
            nextStage.ApprovalWorkflowStageId,
            "Stage Activated",
            currentStage.StageName,
            workflow.WorkflowStatus,
            $"Tahap {nextStage.StageName} dimulai.");
    }
}
