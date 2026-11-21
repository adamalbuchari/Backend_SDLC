
using System.Security.Claims;
using DocuFlow.Api.Data;
using DocuFlow.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DocuFlow.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly AppDbContext _context;

    public UsersController(AppDbContext context)
    {
        _context = context;
    }

    private Guid CurrentUserId =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private string CurrentUserName =>
        User.Identity?.Name ?? "System";

    private bool HasPermission(string permission) =>
        User.HasClaim("permission", permission);

    // =========================================
    // GET: api/users
    // USER_VIEW
    // =========================================
    [HttpGet]
    [Authorize(Policy = "USER_VIEW")]
    public async Task<IActionResult> GetUsers()
    {
        var users = await _context.Users
            .AsNoTracking()
            .Where(x => !x.IsDeleted)
            .OrderBy(x => x.FullName)
            .Select(x => new
            {
                x.UserId,
                x.FullName,
                x.Email,
                x.Department,
                x.IsActive,
                x.CreatedDt,
                x.RoleId,
                PrimaryRole = x.Role != null
                    ? x.Role.RoleName
                    : null
            })
            .ToListAsync();

        var userIds = users.Select(x => x.UserId).ToList();

        var roles = await (
            from ur in _context.UserRoles.AsNoTracking()
            join role in _context.Roles.AsNoTracking()
                on ur.RoleId equals role.RoleId
            where userIds.Contains(ur.UserId)
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

        var result = users.Select(user => new
        {
            user.UserId,
            user.FullName,
            user.Email,
            user.Department,
            user.IsActive,
            user.CreatedDt,
            user.RoleId,
            user.PrimaryRole,
            Roles = roles
                .Where(x => x.UserId == user.UserId)
                .Select(x => x.RoleName)
                .Distinct()
                .ToList()
        });

        return Ok(new
        {
            success = true,
            data = result
        });
    }

    // =========================================
    // GET: api/users/{id}
    // USER_VIEW
    // =========================================
    [HttpGet("{id:guid}")]
    [Authorize(Policy = "USER_VIEW")]
    public async Task<IActionResult> GetUser(Guid id)
    {
        var user = await _context.Users
            .AsNoTracking()
            .Where(x => x.UserId == id && !x.IsDeleted)
            .Select(x => new
            {
                x.UserId,
                x.FullName,
                x.Email,
                x.Department,
                x.IsActive,
                x.CreatedDt,
                x.RoleId,
                PrimaryRole = x.Role != null
                    ? x.Role.RoleName
                    : null
            })
            .FirstOrDefaultAsync();

        if (user == null)
            return NotFound(new
            {
                success = false,
                message = "Pengguna tidak ditemukan."
            });

        var roles = await (
            from ur in _context.UserRoles
            join role in _context.Roles
                on ur.RoleId equals role.RoleId
            where ur.UserId == id
                  && ur.IsActive
                  && !ur.IsDeleted
                  && role.IsActive
                  && !role.IsDeleted
            select new
            {
                role.RoleId,
                role.RoleName
            }
        ).ToListAsync();

        return Ok(new
        {
            success = true,
            data = new
            {
                user.UserId,
                user.FullName,
                user.Email,
                user.Department,
                user.IsActive,
                user.CreatedDt,
                user.RoleId,
                user.PrimaryRole,
                Roles = roles
            }
        });
    }

    // =========================================
    // POST: api/users
    // USER_CREATE + ROLE_MANAGE
    // =========================================
    [HttpPost]
    [Authorize(Policy = "USER_CREATE")]
    public async Task<IActionResult> CreateUser(
        [FromBody] CreateUserRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FullName) ||
            string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new
            {
                success = false,
                message = "Nama, email, dan password wajib diisi."
            });
        }

        if (request.Password.Length < 8)
        {
            return BadRequest(new
            {
                success = false,
                message = "Password minimal 8 karakter."
            });
        }

        if (request.FullName.Trim().Length > 150 ||
            request.Email.Trim().Length > 256 ||
            (request.Department?.Length ?? 0) > 100)
        {
            return BadRequest(new
            {
                success = false,
                message = "Panjang data melebihi batas kolom database."
            });
        }

        if (!HasPermission("ROLE_MANAGE"))
        {
            return Forbid();
        }

        var email = request.Email.Trim().ToLowerInvariant();

        if (await _context.Users.AnyAsync(x => x.Email == email))
        {
            return Conflict(new
            {
                success = false,
                message = "Email sudah terdaftar."
            });
        }

        var roleIds = request.RoleIds?
            .Distinct()
            .ToList() ?? new List<Guid>();

        if (roleIds.Count == 0 ||
            request.PrimaryRoleId == Guid.Empty ||
            !roleIds.Contains(request.PrimaryRoleId))
        {
            return BadRequest(new
            {
                success = false,
                message = "Pilih minimal satu role dan tentukan role utama."
            });
        }

        var validRoles = await _context.Roles
            .Where(x =>
                roleIds.Contains(x.RoleId) &&
                x.IsActive &&
                !x.IsDeleted)
            .ToListAsync();

        if (validRoles.Count != roleIds.Count)
        {
            return BadRequest(new
            {
                success = false,
                message = "Satu atau lebih role tidak valid atau tidak aktif."
            });
        }

        await using var transaction =
            await _context.Database.BeginTransactionAsync();

        try
        {
            var now = DateTime.Now;

            var user = new User
            {
                UserId = Guid.NewGuid(),
                RoleId = request.PrimaryRoleId,
                FullName = request.FullName.Trim(),
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(
                    request.Password),
                Department = request.Department?.Trim() ?? "",
                IsActive = true,
                CreatedBy = CurrentUserName,
                CreatedDt = now,
                IsDeleted = false
            };

            _context.Users.Add(user);

            foreach (var roleId in roleIds)
            {
                _context.UserRoles.Add(new UserRole
                {
                    UserId = user.UserId,
                    RoleId = roleId,
                    IsActive = true,
                    CreatedBy = CurrentUserName,
                    CreatedDt = now,
                    IsDeleted = false
                });
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return CreatedAtAction(
                nameof(GetUser),
                new { id = user.UserId },
                new
                {
                    success = true,
                    message = "Pengguna berhasil dibuat.",
                    userId = user.UserId
                });
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    // =========================================
    // PUT: api/users/{id}
    // USER_UPDATE
    // =========================================
    [HttpPut("{id:guid}")]
    [Authorize(Policy = "USER_UPDATE")]
    public async Task<IActionResult> UpdateUser(
        Guid id,
        [FromBody] UpdateUserRequest request)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(x =>
                x.UserId == id && !x.IsDeleted);

        if (user == null)
        {
            return NotFound(new
            {
                success = false,
                message = "Pengguna tidak ditemukan."
            });
        }

        if (string.IsNullOrWhiteSpace(request.FullName) ||
            string.IsNullOrWhiteSpace(request.Email))
        {
            return BadRequest(new
            {
                success = false,
                message = "Nama dan email wajib diisi."
            });
        }

        var fullName = request.FullName.Trim();
        var email = request.Email.Trim().ToLowerInvariant();
        var department = request.Department?.Trim() ?? "";

        if (fullName.Length > 150 ||
            email.Length > 256 ||
            department.Length > 100)
        {
            return BadRequest(new
            {
                success = false,
                message = "Panjang data melebihi batas kolom database."
            });
        }

        if (await _context.Users.AnyAsync(x =>
            x.UserId != id &&
            x.Email == email &&
            !x.IsDeleted))
        {
            return Conflict(new
            {
                success = false,
                message = "Email sudah digunakan pengguna lain."
            });
        }

        user.FullName = fullName;
        user.Email = email;
        user.Department = department;
        user.UpdatedBy = CurrentUserName;
        user.UpdatedDt = DateTime.Now;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            success = true,
            message = "Data pengguna berhasil diperbarui."
        });
    }

    // =========================================
    // PUT: api/users/{id}/roles
    // ROLE_MANAGE
    // =========================================
    [HttpPut("{id:guid}/roles")]
    [Authorize(Policy = "ROLE_MANAGE")]

    public async Task<IActionResult> UpdateUserRoles(
    Guid id,
    [FromBody] UpdateUserRolesRequest request)
    {
        if (id == CurrentUserId)
        {
            return BadRequest(new
            {
                success = false,
                message = "Anda tidak diperbolehkan mengubah role sendiri."
            });
        }

        var user = await _context.Users
            .FirstOrDefaultAsync(x =>
                x.UserId == id && !x.IsDeleted);

        if (user == null)
        {
            return NotFound(new
            {
                success = false,
                message = "Pengguna tidak ditemukan."
            });
        }

        var roleIds = request.RoleIds?
            .Distinct()
            .ToList() ?? new List<Guid>();

        if (roleIds.Count == 0 ||
            request.PrimaryRoleId == Guid.Empty ||
            !roleIds.Contains(request.PrimaryRoleId))
        {
            return BadRequest(new
            {
                success = false,
                message = "Minimal satu role dan role utama wajib dipilih."
            });
        }

        var validRoles = await _context.Roles
            .Where(x =>
                roleIds.Contains(x.RoleId) &&
                x.IsActive &&
                !x.IsDeleted)
            .ToListAsync();

        if (validRoles.Count != roleIds.Count)
        {
            return BadRequest(new
            {
                success = false,
                message = "Satu atau lebih role tidak valid atau tidak aktif."
            });
        }

        // Lindungi System Administrator aktif yang terakhir.
        var systemAdminRole = await _context.Roles
            .FirstOrDefaultAsync(x =>
                x.RoleName == "System Administrator" &&
                x.IsActive &&
                !x.IsDeleted);

        if (systemAdminRole != null)
        {
            var currentlyHasAdminRole =
                user.RoleId == systemAdminRole.RoleId ||
                await _context.UserRoles.AnyAsync(x =>
                    x.UserId == id &&
                    x.RoleId == systemAdminRole.RoleId &&
                    x.IsActive &&
                    !x.IsDeleted);

            var willHaveAdminRole =
                roleIds.Contains(systemAdminRole.RoleId);

            if (currentlyHasAdminRole && !willHaveAdminRole)
            {
                var otherActiveAdmins = await _context.Users
                    .CountAsync(x =>
                        x.UserId != id &&
                        x.IsActive &&
                        !x.IsDeleted &&
                        (
                            x.RoleId == systemAdminRole.RoleId ||
                            _context.UserRoles.Any(ur =>
                                ur.UserId == x.UserId &&
                                ur.RoleId == systemAdminRole.RoleId &&
                                ur.IsActive &&
                                !ur.IsDeleted)
                        ));

                if (otherActiveAdmins == 0)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Role System Administrator tidak dapat dicabut karena pengguna ini adalah System Administrator aktif yang terakhir."
                    });
                }
            }
        }

        var oldRoles = await _context.UserRoles
            .Where(x => x.UserId == id)
            .ToListAsync();

        var now = DateTime.Now;

        foreach (var oldRole in oldRoles)
        {
            if (roleIds.Contains(oldRole.RoleId))
            {
                oldRole.IsActive = true;
                oldRole.IsDeleted = false;
                oldRole.DeletedBy = null;
                oldRole.DeletedDt = null;
                oldRole.UpdatedBy = CurrentUserName;
                oldRole.UpdatedDt = now;
            }
            else if (oldRole.IsActive && !oldRole.IsDeleted)
            {
                oldRole.IsActive = false;
                oldRole.IsDeleted = true;
                oldRole.DeletedBy = CurrentUserName;
                oldRole.DeletedDt = now;
                oldRole.UpdatedBy = CurrentUserName;
                oldRole.UpdatedDt = now;
            }
        }

        var existingRoleIds = oldRoles
            .Select(x => x.RoleId)
            .ToHashSet();

        foreach (var roleId in roleIds)
        {
            if (!existingRoleIds.Contains(roleId))
            {
                _context.UserRoles.Add(new UserRole
                {
                    UserId = id,
                    RoleId = roleId,
                    IsActive = true,
                    CreatedBy = CurrentUserName,
                    CreatedDt = now,
                    IsDeleted = false
                });
            }
        }

        user.RoleId = request.PrimaryRoleId;
        user.UpdatedBy = CurrentUserName;
        user.UpdatedDt = now;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            success = true,
            message = "Role pengguna berhasil diperbarui."
        });
    }

    // =========================================
    // PUT: api/users/{id}/status
    // USER_UPDATE
    // =========================================
    [HttpPut("{id:guid}/status")]
    [Authorize(Policy = "USER_UPDATE")]
    public async Task<IActionResult> UpdateUserStatus(
        Guid id,
        [FromBody] UpdateUserStatusRequest request)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(x =>
                x.UserId == id && !x.IsDeleted);

        if (user == null)
        {
            return NotFound(new
            {
                success = false,
                message = "Pengguna tidak ditemukan."
            });
        }

        if (id == CurrentUserId && !request.IsActive)
        {
            return BadRequest(new
            {
                success = false,
                message = "Anda tidak dapat menonaktifkan akun sendiri."
            });
        }

        if (!request.IsActive &&
            await IsLastActiveSystemAdministrator(id))
        {
            return BadRequest(new
            {
                success = false,
                message = "Akun System Administrator terakhir tidak boleh dinonaktifkan."
            });
        }

        user.IsActive = request.IsActive;
        user.UpdatedBy = CurrentUserName;
        user.UpdatedDt = DateTime.Now;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            success = true,
            message = request.IsActive
                ? "Pengguna berhasil diaktifkan."
                : "Pengguna berhasil dinonaktifkan."
        });
    }

    // =========================================
    // DELETE: api/users/{id}
    // USER_DELETE
    // SOFT DELETE
    // =========================================
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "USER_DELETE")]
    public async Task<IActionResult> DeleteUser(Guid id)
    {
        if (id == CurrentUserId)
        {
            return BadRequest(new
            {
                success = false,
                message = "Anda tidak dapat menghapus akun sendiri."
            });
        }

        var user = await _context.Users
            .FirstOrDefaultAsync(x =>
                x.UserId == id && !x.IsDeleted);

        if (user == null)
        {
            return NotFound(new
            {
                success = false,
                message = "Pengguna tidak ditemukan."
            });
        }

        if (await IsLastActiveSystemAdministrator(id))
        {
            return BadRequest(new
            {
                success = false,
                message = "Akun System Administrator terakhir tidak boleh dihapus."
            });
        }

        var now = DateTime.Now;

        user.IsActive = false;
        user.IsDeleted = true;
        user.DeletedBy = CurrentUserName;
        user.DeletedDt = now;
        user.UpdatedBy = CurrentUserName;
        user.UpdatedDt = now;

        var userRoles = await _context.UserRoles
            .Where(x =>
                x.UserId == id &&
                x.IsActive &&
                !x.IsDeleted)
            .ToListAsync();

        foreach (var role in userRoles)
        {
            role.IsActive = false;
            role.IsDeleted = true;
            role.DeletedBy = CurrentUserName;
            role.DeletedDt = now;
            role.UpdatedBy = CurrentUserName;
            role.UpdatedDt = now;
        }

        await _context.SaveChangesAsync();

        return Ok(new
        {
            success = true,
            message = "Pengguna berhasil dihapus secara soft delete."
        });
    }

    // =========================================
    // GET: api/users/available-roles
    // ROLE_VIEW
    // =========================================
    [HttpGet("available-roles")]
    [Authorize(Policy = "ROLE_VIEW")]
    public async Task<IActionResult> GetAvailableRoles()
    {
        var roles = await _context.Roles
            .AsNoTracking()
            .Where(x => x.IsActive && !x.IsDeleted)
            .OrderBy(x => x.RoleName)
            .Select(x => new
            {
                x.RoleId,
                x.RoleName,
                x.Description
            })
            .ToListAsync();

        return Ok(new
        {
            success = true,
            data = roles
        });
    }

    // =========================================
    // HELPER:
    // CHECK LAST SYSTEM ADMINISTRATOR
    // =========================================
    private async Task<bool> IsLastActiveSystemAdministrator(
        Guid userId)
    {
        var systemAdminRole = await _context.Roles
            .FirstOrDefaultAsync(x =>
                x.RoleName == "System Administrator" &&
                x.IsActive &&
                !x.IsDeleted);

        if (systemAdminRole == null)
            return false;

        var activeAdminCount = await _context.Users
            .Where(x =>
                x.IsActive &&
                !x.IsDeleted &&
                x.UserId != userId)
            .CountAsync(x =>
                x.RoleId == systemAdminRole.RoleId ||
                _context.UserRoles.Any(ur =>
                    ur.UserId == x.UserId &&
                    ur.RoleId == systemAdminRole.RoleId &&
                    ur.IsActive &&
                    !ur.IsDeleted));

        var targetIsAdmin = await _context.Users
            .Where(x =>
                x.UserId == userId &&
                x.IsActive &&
                !x.IsDeleted)
            .AnyAsync(x =>
                x.RoleId == systemAdminRole.RoleId ||
                _context.UserRoles.Any(ur =>
                    ur.UserId == x.UserId &&
                    ur.RoleId == systemAdminRole.RoleId &&
                    ur.IsActive &&
                    !ur.IsDeleted));

        return targetIsAdmin && activeAdminCount == 0;
    }
}

// =========================================
// REQUEST DTOs
// =========================================

public class CreateUserRequest
{
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Password { get; set; } = "";
    public string? Department { get; set; }

    public List<Guid> RoleIds { get; set; } = new();
    public Guid PrimaryRoleId { get; set; }
}

public class UpdateUserRequest
{
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public string? Department { get; set; }
}

public class UpdateUserRolesRequest
{
    public List<Guid> RoleIds { get; set; } = new();
    public Guid PrimaryRoleId { get; set; }
}

public class UpdateUserStatusRequest
{
    public bool IsActive { get; set; }
}
