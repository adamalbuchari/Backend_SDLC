
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using DocuFlow.Api.Data;
using DocuFlow.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace DocuFlow.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private const string PermissionClaimType = "permission";

    private readonly AppDbContext _context;
    private readonly IConfiguration _configuration;

    public AuthController(
        AppDbContext context,
        IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    // =========================================
    // INITIAL ADMIN SETUP
    // =========================================
    [AllowAnonymous]
    [HttpPost("setup")]
    public async Task<IActionResult> Setup(RegisterRequest request)
    {
        if (await _context.Users.AnyAsync(x => !x.IsDeleted))
        {
            return Conflict(new
            {
                success = false,
                message = "Akun awal sudah dibuat. Pendaftaran setup ditutup."
            });
        }

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

        var email = request.Email.Trim().ToLowerInvariant();

        if (await _context.Users.AnyAsync(x => x.Email == email))
        {
            return Conflict(new
            {
                success = false,
                message = "Email sudah terdaftar."
            });
        }

        var adminRole = await _context.Roles
            .FirstOrDefaultAsync(x =>
                x.RoleName == "System Administrator" &&
                x.IsActive &&
                !x.IsDeleted);

        if (adminRole == null)
        {
            return BadRequest(new
            {
                success = false,
                message = "Role Admin tidak ditemukan atau tidak aktif."
            });
        }

        await using var transaction =
            await _context.Database.BeginTransactionAsync();

        try
        {
            var user = new User
            {
                UserId = Guid.NewGuid(),
                RoleId = adminRole.RoleId,
                FullName = request.FullName.Trim(),
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                Department = request.Department?.Trim() ?? "",
                IsActive = true,
                CreatedBy = "System",
                CreatedDt = DateTime.Now,
                IsDeleted = false
            };

            _context.Users.Add(user);

            // Simpan role utama juga ke tabel multi-role.
            var userRole = new UserRole
            {
                UserId = user.UserId,
                RoleId = adminRole.RoleId,
                IsActive = true,
                CreatedBy = "System",
                CreatedDt = DateTime.Now,
                IsDeleted = false
            };

            _context.UserRoles.Add(userRole);

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return Ok(new
            {
                success = true,
                message = "Akun awal berhasil dibuat. Silakan login.",
                email = user.Email
            });
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    // =========================================
    // LOGIN
    // =========================================
    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var email = request.Email?.Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new
            {
                success = false,
                message = "Email dan password wajib diisi."
            });
        }

        var user = await _context.Users
            .Include(x => x.Role)
            .FirstOrDefaultAsync(x =>
                x.Email == email &&
                !x.IsDeleted);

        if (user == null ||
            !BCrypt.Net.BCrypt.Verify(
                request.Password,
                user.PasswordHash))
        {
            return Unauthorized(new
            {
                success = false,
                message = "Email atau password salah."
            });
        }

        if (!user.IsActive)
        {
            return Unauthorized(new
            {
                success = false,
                message = "Akun tidak aktif."
            });
        }

        // Ambil semua global role aktif dari MD_UserRole.
        var assignedRoles = await (
            from userRole in _context.UserRoles
            join role in _context.Roles
                on userRole.RoleId equals role.RoleId
            where userRole.UserId == user.UserId
                  && userRole.IsActive
                  && !userRole.IsDeleted
                  && role.IsActive
                  && !role.IsDeleted
            select new
            {
                role.RoleId,
                role.RoleName
            }
        ).ToListAsync();

        // Kompatibilitas dengan RoleId lama di MD_User.
        // Jika belum tercatat di MD_UserRole, role utama tetap digunakan.
        if (user.Role != null &&
            user.Role.IsActive &&
            !user.Role.IsDeleted &&
            !assignedRoles.Any(x => x.RoleId == user.RoleId))
        {
            assignedRoles.Add(new
            {
                RoleId = user.Role.RoleId,
                RoleName = user.Role.RoleName
            });
        }

        if (assignedRoles.Count == 0)
        {
            return Unauthorized(new
            {
                success = false,
                message = "User belum memiliki role aktif."
            });
        }

        var roleIds = assignedRoles
            .Select(x => x.RoleId)
            .Distinct()
            .ToList();

        // Ambil permission dari seluruh role aktif user.
        var permissions = await (
            from rolePermission in _context.RolePermissions
            join permission in _context.Permissions
                on rolePermission.PermissionId equals permission.PermissionId
            where roleIds.Contains(rolePermission.RoleId)
                  && !rolePermission.IsDeleted
                  && permission.IsActive
                  && !permission.IsDeleted
            select permission.PermissionCode
        )
        .Distinct()
        .ToListAsync();

        // Pertahankan Role utama untuk kompatibilitas frontend.
        var primaryRole = assignedRoles
            .FirstOrDefault(x => x.RoleId == user.RoleId)
            ?? assignedRoles.First();

        var claims = new List<Claim>
        {
            new Claim(
                ClaimTypes.NameIdentifier,
                user.UserId.ToString()),

            new Claim(
                ClaimTypes.Name,
                user.FullName),

            new Claim(
                ClaimTypes.Email,
                user.Email)
        };

        // Tambahkan semua role sebagai role claims.
        foreach (var role in assignedRoles)
        {
            claims.Add(
                new Claim(ClaimTypes.Role, role.RoleName));
        }

        // Tambahkan permission sebagai custom claims.
        foreach (var permission in permissions)
        {
            claims.Add(
                new Claim(PermissionClaimType, permission));
        }

        var secretKey = _configuration["Jwt:SecretKey"]
            ?? throw new InvalidOperationException(
                "JWT SecretKey belum dikonfigurasi.");

        var issuer = _configuration["Jwt:Issuer"];
        var audience = _configuration["Jwt:Audience"];

        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(secretKey));

        var credentials = new SigningCredentials(
            key,
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(8),
            signingCredentials: credentials
        );

        var tokenString =
            new JwtSecurityTokenHandler().WriteToken(token);

        return Ok(new
        {
            success = true,
            message = "Login berhasil.",
            token = tokenString,

            user = new
            {
                user.UserId,
                user.FullName,
                user.Email,

                // Properti Role lama tetap tersedia.
                Role = primaryRole.RoleName,

                // Informasi tambahan untuk pengembangan berikutnya.
                Roles = assignedRoles
                    .Select(x => x.RoleName)
                    .Distinct()
                    .ToList(),

                Permissions = permissions,

                user.Department
            }
        });
    }
}

// =========================================
// REQUEST MODELS
// =========================================
public class RegisterRequest
{
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Password { get; set; } = "";
    public string? Department { get; set; }
}

public class LoginRequest
{
    public string Email { get; set; } = "";
    public string Password { get; set; } = "";
}
