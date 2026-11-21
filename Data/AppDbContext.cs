
using Microsoft.EntityFrameworkCore;
using DocuFlow.Api.Models;

namespace DocuFlow.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<ProjectRole> ProjectRoles => Set<ProjectRole>();
    public DbSet<ProjectMember> ProjectMembers => Set<ProjectMember>();

    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectType> ProjectTypes => Set<ProjectType>();
    public DbSet<ProjectStatus> ProjectStatuses => Set<ProjectStatus>();
    public DbSet<DocumentTemplate> DocumentTemplates => Set<DocumentTemplate>();
    public DbSet<DocumentTemplateItem> DocumentTemplateItems => Set<DocumentTemplateItem>();
    public DbSet<ProjectDocumentRequirement> ProjectDocumentRequirements => Set<ProjectDocumentRequirement>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<DocumentVersion> DocumentVersions => Set<DocumentVersion>();

    public DbSet<ApprovalWorkflow> ApprovalWorkflows
    => Set<ApprovalWorkflow>();

    public DbSet<ApprovalWorkflowStage> ApprovalWorkflowStages
        => Set<ApprovalWorkflowStage>();

    public DbSet<DocumentWorkflow> DocumentWorkflows
        => Set<DocumentWorkflow>();

    public DbSet<DocumentWorkflowAssignment> DocumentWorkflowAssignments
        => Set<DocumentWorkflowAssignment>();

    public DbSet<DocumentWorkflowHistory> DocumentWorkflowHistories
        => Set<DocumentWorkflowHistory>();

    public DbSet<DocumentType> DocumentTypes => Set<DocumentType>();

    public DbSet<SDLCPhase> SDLCPhases => Set<SDLCPhase>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // =========================================
        // MASTER ROLE
        // =========================================
        modelBuilder.Entity<Role>(entity =>
        {
            entity.ToTable("MD_Role");
            entity.HasKey(x => x.RoleId);

            entity.Property(x => x.RoleName)
                .HasMaxLength(100)
                .IsRequired();

            entity.HasIndex(x => x.RoleName)
                .IsUnique();
        });

        // =========================================
        // MASTER USER
        // =========================================

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("MD_User");
            entity.HasKey(x => x.UserId);

            entity.Property(x => x.FullName)
                .HasColumnType("nvarchar(150)")
                .HasMaxLength(150)
                .IsRequired();

            entity.Property(x => x.Email)
                .HasColumnType("nvarchar(256)")
                .HasMaxLength(256)
                .IsRequired();

            entity.Property(x => x.PasswordHash)
                .HasColumnType("nvarchar(MAX)")
                .IsRequired();

            entity.Property(x => x.Department)
                .HasColumnType("nvarchar(100)")
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(x => x.CreatedBy)
                .HasColumnType("varchar(100)")
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(x => x.UpdatedBy)
                .HasColumnType("varchar(100)")
                .HasMaxLength(100);

            entity.Property(x => x.DeletedBy)
                .HasColumnType("varchar(100)")
                .HasMaxLength(100);

            entity.HasIndex(x => x.Email)
                .IsUnique();

            entity.HasOne(x => x.Role)
                .WithMany(x => x.Users)
                .HasForeignKey(x => x.RoleId)
                .OnDelete(DeleteBehavior.Restrict);
        });


        // =========================================
        // GLOBAL USER MULTI-ROLE
        // MD_UserRole
        // =========================================
        modelBuilder.Entity<UserRole>(entity =>
        {
            entity.ToTable("MD_UserRole");

            entity.HasKey(x => new { x.UserId, x.RoleId })
                .HasName("PK_MD_UserRole");

            entity.HasIndex(x => new { x.UserId, x.RoleId })
                .IsUnique()
                .HasDatabaseName("UX_MD_UserRole_User_Role");

            entity.Property(x => x.CreatedBy)
                .HasColumnType("varchar(100)")
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(x => x.UpdatedBy)
                .HasColumnType("varchar(100)")
                .HasMaxLength(100);

            entity.Property(x => x.DeletedBy)
                .HasColumnType("varchar(100)")
                .HasMaxLength(100);

            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<Role>()
                .WithMany()
                .HasForeignKey(x => x.RoleId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // =========================================
        // MASTER PERMISSION
        // MD_Permission
        // =========================================
        modelBuilder.Entity<Permission>(entity =>
        {
            entity.ToTable("MD_Permission");

            entity.HasKey(x => x.PermissionId)
                .HasName("PK_MD_Permission");

            entity.HasIndex(x => x.PermissionCode)
                .IsUnique()
                .HasDatabaseName("UX_MD_Permission_Code");

            entity.Property(x => x.PermissionCode)
                .HasColumnType("varchar(100)")
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(x => x.PermissionName)
                .HasColumnType("nvarchar(200)")
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(x => x.Description)
                .HasColumnType("nvarchar(500)")
                .HasMaxLength(500);

            entity.Property(x => x.CreatedBy)
                .HasColumnType("varchar(100)")
                .HasMaxLength(100)
                .IsRequired();
        });

        // =========================================
        // ROLE PERMISSION
        // MD_RolePermission
        // =========================================
        modelBuilder.Entity<RolePermission>(entity =>
        {
            entity.ToTable("MD_RolePermission");

            entity.HasKey(x => x.RolePermissionId)
                .HasName("PK_MD_RolePermission");

            entity.HasIndex(x => new { x.RoleId, x.PermissionId })
                .IsUnique()
                .HasDatabaseName("UX_MD_RolePermission_Role_Permission");

            entity.Property(x => x.CreatedBy)
                .HasColumnType("varchar(100)")
                .HasMaxLength(100)
                .IsRequired();

            entity.HasOne<Role>()
                .WithMany()
                .HasForeignKey(x => x.RoleId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<Permission>()
                .WithMany()
                .HasForeignKey(x => x.PermissionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // =========================================
        // MASTER PROJECT ROLE
        // MD_ProjectRole
        // =========================================
        modelBuilder.Entity<ProjectRole>(entity =>
        {
            entity.ToTable("MD_ProjectRole");

            entity.HasKey(x => x.ProjectRoleId)
                .HasName("PK_MD_ProjectRole");

            entity.HasIndex(x => x.ProjectRoleName)
                .IsUnique()
                .HasDatabaseName("UX_MD_ProjectRole_Name");

            entity.Property(x => x.ProjectRoleName)
                .HasColumnType("nvarchar(200)")
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(x => x.Description)
                .HasColumnType("nvarchar(500)")
                .HasMaxLength(500);

            entity.Property(x => x.CreatedBy)
                .HasColumnType("varchar(100)")
                .HasMaxLength(100)
                .IsRequired();
        });

        // =========================================
        // TRANSACTION PROJECT MEMBER
        // TR_ProjectMember
        // =========================================
        modelBuilder.Entity<ProjectMember>(entity =>
        {
            entity.ToTable("TR_ProjectMember");

            entity.HasKey(x => x.ProjectMemberId)
                .HasName("PK_TR_ProjectMember");

            entity.HasIndex(x => new { x.ProjectId, x.UserId })
                .IsUnique()
                .HasDatabaseName("UX_TR_ProjectMember_Project_User");

            entity.Property(x => x.CreatedBy)
                .HasColumnType("varchar(100)")
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(x => x.UpdatedBy)
                .HasColumnType("varchar(100)")
                .HasMaxLength(100);

            entity.Property(x => x.DeletedBy)
                .HasColumnType("varchar(100)")
                .HasMaxLength(100);

            entity.HasOne<Project>()
                .WithMany()
                .HasForeignKey(x => x.ProjectId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<ProjectRole>()
                .WithMany()
                .HasForeignKey(x => x.ProjectRoleId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // =========================================
        // MASTER PROJECT TYPE
        // =========================================
        modelBuilder.Entity<ProjectType>(entity =>
        {
            entity.ToTable("MD_ProjectType");
            entity.HasKey(x => x.ProjectTypeId);

            entity.Property(x => x.ProjectTypeName)
                .HasMaxLength(100)
                .IsRequired();

            entity.HasIndex(x => x.ProjectTypeName)
                .IsUnique();
        });

        // =========================================
        // MASTER PROJECT STATUS
        // =========================================
        modelBuilder.Entity<ProjectStatus>(entity =>
        {
            entity.ToTable("MD_ProjectStatus");
            entity.HasKey(x => x.ProjectStatusId);

            entity.Property(x => x.ProjectStatusName)
                .HasMaxLength(100)
                .IsRequired();

            entity.HasIndex(x => x.ProjectStatusName)
                .IsUnique();
        });

        // =========================================
        // MASTER DOCUMENT TEMPLATE
        // =========================================
        modelBuilder.Entity<DocumentTemplate>(entity =>
        {
            entity.ToTable("MD_DocumentTemplate");
            entity.HasKey(x => x.DocumentTemplateId);

            entity.Property(x => x.TemplateName)
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(x => x.Description)
                .HasMaxLength(1000);

            entity.Property(x => x.VersionNo)
                .IsRequired();

            entity.Property(x => x.CreatedBy)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(x => x.UpdatedBy)
                .HasMaxLength(100);

            entity.Property(x => x.DeletedBy)
                .HasMaxLength(100);
        });

        // =========================================
        // MASTER DOCUMENT TEMPLATE ITEM
        // =========================================
        modelBuilder.Entity<DocumentTemplateItem>(entity =>
        {
            entity.ToTable("MD_DocumentTemplateItem");
            entity.HasKey(x => x.DocumentTemplateItemId);

            entity.Property(x => x.DocumentName)
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(x => x.Description)
                .HasMaxLength(1000);

            entity.Property(x => x.CreatedBy)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(x => x.UpdatedBy)
                .HasMaxLength(100);

            entity.Property(x => x.DeletedBy)
                .HasMaxLength(100);

            entity.HasOne<DocumentTemplate>()
                .WithMany()
                .HasForeignKey(x => x.DocumentTemplateId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // =========================================
        // TRANSACTION PROJECT DOCUMENT REQUIREMENT
        // =========================================
        modelBuilder.Entity<ProjectDocumentRequirement>(entity =>
        {
            entity.ToTable("TR_ProjectDocumentRequirement");
            entity.HasKey(x => x.ProjectDocumentRequirementId);

            entity.Property(x => x.DocumentName)
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(x => x.CreatedBy)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(x => x.UpdatedBy)
                .HasMaxLength(100);

            entity.Property(x => x.DeletedBy)
                .HasMaxLength(100);

            entity.HasOne<Project>()
                .WithMany()
                .HasForeignKey(x => x.ProjectId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<DocumentTemplateItem>()
                .WithMany()
                .HasForeignKey(x => x.DocumentTemplateItemId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // =========================================
        // TRANSACTION PROJECT
        // =========================================
        modelBuilder.Entity<Project>(entity =>
        {
            entity.ToTable("TR_Project");
            entity.HasKey(x => x.ProjectId);

            entity.Property(x => x.ProjectCode)
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(x => x.ProjectName)
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(x => x.Description)
                .HasMaxLength(1000);

            entity.HasIndex(x => x.ProjectCode)
                .IsUnique();

            entity.HasOne(x => x.ProjectType)
                .WithMany(x => x.Projects)
                .HasForeignKey(x => x.ProjectTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.ProjectStatus)
                .WithMany(x => x.Projects)
                .HasForeignKey(x => x.ProjectStatusId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.ProjectManager)
                .WithMany()
                .HasForeignKey(x => x.ProjectManagerId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.DocumentTemplate)
                .WithMany()
                .HasForeignKey(x => x.DocumentTemplateId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // =========================================
        // TRANSACTION DOCUMENT
        // TR_Document
        // =========================================
        modelBuilder.Entity<Document>(entity =>
        {
            entity.ToTable("TR_Document");

            entity.HasKey(x => x.DocumentId);

            entity.Property(x => x.DocumentTitle)
                .HasColumnType("nvarchar(200)")
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(x => x.Description)
                .HasColumnType("nvarchar(1000)")
                .HasMaxLength(1000);

            entity.Property(x => x.Status)
                .HasColumnType("varchar(30)")
                .HasMaxLength(30)
                .IsRequired();

            entity.Property(x => x.ApprovalStatus)
                .HasColumnType("varchar(30)")
                .HasMaxLength(30)
                .IsRequired();

            entity.Property(x => x.CreatedBy)
                .HasColumnType("varchar(100)")
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(x => x.UpdatedBy)
                .HasColumnType("varchar(100)")
                .HasMaxLength(100);

            entity.Property(x => x.DeletedBy)
                .HasColumnType("varchar(100)")
                .HasMaxLength(100);

            entity.HasOne<Project>()
                .WithMany()
                .HasForeignKey(x => x.ProjectId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<ProjectDocumentRequirement>()
                .WithMany()
                .HasForeignKey(x => x.ProjectDocumentRequirementId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(x => x.DocumentOwnerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // =========================================
        // TRANSACTION DOCUMENT VERSION
        // TR_DocumentVersion
        // =========================================
        modelBuilder.Entity<DocumentVersion>(entity =>
        {
            entity.ToTable("TR_DocumentVersion");

            entity.HasKey(x => x.DocumentVersionId);

            entity.HasIndex(x => new { x.DocumentId, x.VersionNo })
                .IsUnique()
                .HasDatabaseName("UQ_TR_DocumentVersion_Document_Version");

            entity.Property(x => x.OriginalFileName)
                .HasColumnType("nvarchar(255)")
                .HasMaxLength(255)
                .IsRequired();

            entity.Property(x => x.StoredFileName)
                .HasColumnType("nvarchar(255)")
                .HasMaxLength(255)
                .IsRequired();

            entity.Property(x => x.FilePath)
                .HasColumnType("nvarchar(1000)")
                .HasMaxLength(1000)
                .IsRequired();

            entity.Property(x => x.ContentType)
                .HasColumnType("varchar(150)")
                .HasMaxLength(150)
                .IsRequired();

            entity.Property(x => x.ChangeDescription)
                .HasColumnType("nvarchar(1000)")
                .HasMaxLength(1000);

            entity.Property(x => x.CreatedBy)
                .HasColumnType("varchar(100)")
                .HasMaxLength(100)
                .IsRequired();

            entity.HasOne(x => x.Document)
                .WithMany(x => x.Versions)
                .HasForeignKey(x => x.DocumentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(x => x.UploadedBy)
                .OnDelete(DeleteBehavior.Restrict);
        });


        // =========================================
        // MASTER DOCUMENT TYPE
        // MD_DocumentType
        // =========================================
        modelBuilder.Entity<DocumentType>(entity =>
        {
            entity.ToTable("MD_DocumentType");

            entity.HasKey(x => x.DocumentTypeId)
                .HasName("PK_MD_DocumentType");

            entity.Property(x => x.DocumentTypeCode)
                .HasColumnType("varchar(30)")
                .HasMaxLength(30)
                .IsRequired();

            entity.Property(x => x.DocumentTypeName)
                .HasColumnType("nvarchar(200)")
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(x => x.Description)
                .HasColumnType("nvarchar(1000)")
                .HasMaxLength(1000);

            entity.Property(x => x.CreatedBy)
                .HasColumnType("varchar(100)")
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(x => x.UpdatedBy)
                .HasColumnType("varchar(100)")
                .HasMaxLength(100);

            entity.Property(x => x.DeletedBy)
                .HasColumnType("varchar(100)")
                .HasMaxLength(100);

            entity.HasIndex(x => x.DocumentTypeCode)
                .IsUnique()
                .HasDatabaseName("UX_MD_DocumentType_Code");

            entity.HasIndex(x => x.DocumentTypeName)
                .IsUnique()
                .HasDatabaseName("UX_MD_DocumentType_Name");
        });


        // =========================================
        // MASTER APPROVAL WORKFLOW
        // =========================================
        modelBuilder.Entity<ApprovalWorkflow>(entity =>
        {
            entity.ToTable("MD_ApprovalWorkflow");
            entity.HasKey(x => x.ApprovalWorkflowId);

            entity.Property(x => x.WorkflowName)
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(x => x.Description)
                .HasMaxLength(1000);

            entity.Property(x => x.CreatedBy)
                .HasMaxLength(100)
                .IsRequired();

            entity.HasOne<Project>()
                .WithMany()
                .HasForeignKey(x => x.ProjectId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.DocumentType)
                .WithMany()
                .HasForeignKey(x => x.DocumentTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => new
            {
                x.ProjectId,
                x.DocumentTypeId,
                x.IsActive
            });
        });

        // =========================================
        // MASTER APPROVAL WORKFLOW STAGE
        // =========================================
        modelBuilder.Entity<ApprovalWorkflowStage>(entity =>
        {
            entity.ToTable("MD_ApprovalWorkflowStage");
            entity.HasKey(x => x.ApprovalWorkflowStageId);

            entity.Property(x => x.StageName)
                .HasMaxLength(150)
                .IsRequired();

            entity.Property(x => x.StageType)
                .HasMaxLength(30)
                .IsRequired();

            entity.Property(x => x.ApprovalMode)
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(x => x.ApprovalRule)
                .HasMaxLength(20)
                .IsRequired();

            entity.HasIndex(x => new
            {
                x.ApprovalWorkflowId,
                x.SequenceNo
            }).IsUnique();

            entity.HasOne(x => x.ApprovalWorkflow)
                .WithMany(x => x.Stages)
                .HasForeignKey(x => x.ApprovalWorkflowId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // =========================================
        // TRANSACTION DOCUMENT WORKFLOW
        // =========================================
        modelBuilder.Entity<DocumentWorkflow>(entity =>
        {
            entity.ToTable("TR_DocumentWorkflow");
            entity.HasKey(x => x.DocumentWorkflowId);

            entity.Property(x => x.WorkflowStatus)
                .HasMaxLength(30)
                .IsRequired();

            entity.Property(x => x.CreatedBy)
                .HasMaxLength(100)
                .IsRequired();

            entity.HasIndex(x => x.DocumentId);

            entity.HasOne(x => x.Document)
                .WithMany()
                .HasForeignKey(x => x.DocumentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.ApprovalWorkflow)
                .WithMany()
                .HasForeignKey(x => x.ApprovalWorkflowId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // =========================================
        // TRANSACTION DOCUMENT WORKFLOW ASSIGNMENT
        // =========================================
        modelBuilder.Entity<DocumentWorkflowAssignment>(entity =>
        {
            entity.ToTable("TR_DocumentWorkflowAssignment");
            entity.HasKey(x => x.DocumentWorkflowAssignmentId);

            entity.Property(x => x.AssignmentType)
                .HasMaxLength(30)
                .IsRequired();

            entity.Property(x => x.AssignmentStatus)
                .HasMaxLength(30)
                .IsRequired();

            entity.HasOne(x => x.DocumentWorkflow)
                .WithMany(x => x.Assignments)
                .HasForeignKey(x => x.DocumentWorkflowId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.ApprovalWorkflowStage)
                .WithMany()
                .HasForeignKey(x => x.ApprovalWorkflowStageId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.AssignedUser)
                .WithMany()
                .HasForeignKey(x => x.AssignedUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => new
            {
                x.DocumentWorkflowId,
                x.ApprovalWorkflowStageId,
                x.AssignedUserId
            });
        });

        // =========================================
        // TRANSACTION DOCUMENT WORKFLOW HISTORY
        // =========================================
        modelBuilder.Entity<DocumentWorkflowHistory>(entity =>
        {
            entity.ToTable("TR_DocumentWorkflowHistory");
            entity.HasKey(x => x.DocumentWorkflowHistoryId);

            entity.Property(x => x.Action)
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(x => x.PreviousStatus)
                .HasMaxLength(30);

            entity.Property(x => x.NewStatus)
                .HasMaxLength(30);

            entity.Property(x => x.Comment)
                .HasMaxLength(2000);

            entity.HasOne(x => x.DocumentWorkflow)
                .WithMany(x => x.History)
                .HasForeignKey(x => x.DocumentWorkflowId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.ApprovalWorkflowStage)
                .WithMany()
                .HasForeignKey(x => x.ApprovalWorkflowStageId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.ActorUser)
                .WithMany()
                .HasForeignKey(x => x.ActorUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => new
            {
                x.DocumentWorkflowId,
                x.ActionAt
            });
        });

        modelBuilder.Entity<SDLCPhase>(entity =>
{
    entity.ToTable("MD_SDLCPhase");

    entity.HasKey(e => e.SDLPhaseId);

    entity.Property(e => e.SDLPhaseName)
        .HasMaxLength(200)
        .IsRequired();

    entity.Property(e => e.Description)
        .HasMaxLength(1000);

    entity.Property(e => e.SequenceNo)
        .IsRequired();
});

    }
}
