
using System.Text;
using DocuFlow.Api.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// =========================================
// CONTROLLERS
// =========================================
builder.Services.AddControllers();

// =========================================
// DATABASE SQL SERVER
// =========================================
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")
    )
);

// =========================================
// JWT AUTHENTICATION
// =========================================
var jwtKey = builder.Configuration["Jwt:SecretKey"]
    ?? throw new InvalidOperationException(
        "JWT SecretKey belum dikonfigurasi."
    );

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters =
            new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,

                ValidIssuer = builder.Configuration["Jwt:Issuer"],
                ValidAudience = builder.Configuration["Jwt:Audience"],

                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(jwtKey)
                ),

                ClockSkew = TimeSpan.FromMinutes(1)
            };
    });

// =========================================
// RBAC AUTHORIZATION POLICIES
// =========================================
var permissionCodes = new[]
{
    "AUDIT_VIEW",

    "DOCUMENT_APPROVE",
    "DOCUMENT_DELETE",
    "DOCUMENT_REVIEW",
    "DOCUMENT_UPDATE",
    "DOCUMENT_UPLOAD",
    "DOCUMENT_VIEW",

    "PERMISSION_MANAGE",

    "PROJECT_CREATE",
    "PROJECT_DELETE",
    "PROJECT_MEMBER_MANAGE",
    "PROJECT_MEMBER_VIEW",
    "PROJECT_UPDATE",
    "PROJECT_VIEW",

    "REPORT_VIEW",

    "ROLE_MANAGE",
    "ROLE_VIEW",

    "USER_CREATE",
    "USER_DELETE",
    "USER_UPDATE",
    "USER_VIEW"
};

builder.Services.AddAuthorization(options =>
{
    foreach (var permissionCode in permissionCodes)
    {
        options.AddPolicy(permissionCode, policy =>
        {
            policy.RequireAuthenticatedUser();

            policy.RequireClaim(
                "permission",
                permissionCode
            );
        });
    }
});

// =========================================
// CORS UNTUK REACT
// =========================================
builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendPolicy", policy =>
    {
        policy.WithOrigins(
                "http://localhost:5173",
                "http://127.0.0.1:5173",
                "http://localhost:8443",
                "https://localhost:8443"
            )
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// =========================================
// OPENAPI
// =========================================
builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// =========================================
// BUILD APPLICATION
// =========================================
var app = builder.Build();
app.UseSwagger();
app.UseSwaggerUI();

// =========================================
// DEVELOPMENT
// =========================================
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// =========================================
// MIDDLEWARE
// =========================================
app.UseHttpsRedirection();

app.UseCors("FrontendPolicy");

app.UseAuthentication();

app.UseAuthorization();

// =========================================
// ROUTES
// =========================================
app.MapControllers();

app.Run();
