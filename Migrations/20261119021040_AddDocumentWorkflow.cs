
using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocuFlow.Api.Migrations
{
    public partial class AddDocumentWorkflow : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MD_ApprovalWorkflow",
                columns: table => new
                {
                    ApprovalWorkflowId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkflowName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DocumentTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedDt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedDt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MD_ApprovalWorkflow", x => x.ApprovalWorkflowId);
                    table.ForeignKey(
                        name: "FK_MD_ApprovalWorkflow_TR_Project_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "TR_Project",
                        principalColumn: "ProjectId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MD_ApprovalWorkflowStage",
                columns: table => new
                {
                    ApprovalWorkflowStageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApprovalWorkflowId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StageName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    StageType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    SequenceNo = table.Column<int>(type: "int", nullable: false),
                    ApprovalMode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ApprovalRule = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsRequired = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MD_ApprovalWorkflowStage", x => x.ApprovalWorkflowStageId);
                    table.ForeignKey(
                        name: "FK_MD_ApprovalWorkflowStage_MD_ApprovalWorkflow_ApprovalWorkflowId",
                        column: x => x.ApprovalWorkflowId,
                        principalTable: "MD_ApprovalWorkflow",
                        principalColumn: "ApprovalWorkflowId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TR_DocumentWorkflow",
                columns: table => new
                {
                    DocumentWorkflowId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApprovalWorkflowId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkflowStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    CurrentStageNo = table.Column<int>(type: "int", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedDt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TR_DocumentWorkflow", x => x.DocumentWorkflowId);
                    table.ForeignKey(
                        name: "FK_TR_DocumentWorkflow_MD_ApprovalWorkflow_ApprovalWorkflowId",
                        column: x => x.ApprovalWorkflowId,
                        principalTable: "MD_ApprovalWorkflow",
                        principalColumn: "ApprovalWorkflowId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TR_DocumentWorkflow_TR_Document_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "TR_Document",
                        principalColumn: "DocumentId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TR_DocumentWorkflowAssignment",
                columns: table => new
                {
                    DocumentWorkflowAssignmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentWorkflowId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApprovalWorkflowStageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssignedUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssignmentType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    AssignmentStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TR_DocumentWorkflowAssignment", x => x.DocumentWorkflowAssignmentId);
                    table.ForeignKey(
                        name: "FK_TR_DocumentWorkflowAssignment_MD_ApprovalWorkflowStage_ApprovalWorkflowStageId",
                        column: x => x.ApprovalWorkflowStageId,
                        principalTable: "MD_ApprovalWorkflowStage",
                        principalColumn: "ApprovalWorkflowStageId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TR_DocumentWorkflowAssignment_MD_User_AssignedUserId",
                        column: x => x.AssignedUserId,
                        principalTable: "MD_User",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TR_DocumentWorkflowAssignment_TR_DocumentWorkflow_DocumentWorkflowId",
                        column: x => x.DocumentWorkflowId,
                        principalTable: "TR_DocumentWorkflow",
                        principalColumn: "DocumentWorkflowId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TR_DocumentWorkflowHistory",
                columns: table => new
                {
                    DocumentWorkflowHistoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentWorkflowId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApprovalWorkflowStageId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PreviousStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    NewStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Comment = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ActionAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TR_DocumentWorkflowHistory", x => x.DocumentWorkflowHistoryId);
                    table.ForeignKey(
                        name: "FK_TR_DocumentWorkflowHistory_MD_ApprovalWorkflowStage_ApprovalWorkflowStageId",
                        column: x => x.ApprovalWorkflowStageId,
                        principalTable: "MD_ApprovalWorkflowStage",
                        principalColumn: "ApprovalWorkflowStageId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TR_DocumentWorkflowHistory_MD_User_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "MD_User",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TR_DocumentWorkflowHistory_TR_DocumentWorkflow_DocumentWorkflowId",
                        column: x => x.DocumentWorkflowId,
                        principalTable: "TR_DocumentWorkflow",
                        principalColumn: "DocumentWorkflowId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MD_ApprovalWorkflow_ProjectId_DocumentTypeId_IsActive",
                table: "MD_ApprovalWorkflow",
                columns: new[] { "ProjectId", "DocumentTypeId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_MD_ApprovalWorkflowStage_ApprovalWorkflowId_SequenceNo",
                table: "MD_ApprovalWorkflowStage",
                columns: new[] { "ApprovalWorkflowId", "SequenceNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TR_DocumentWorkflow_ApprovalWorkflowId",
                table: "TR_DocumentWorkflow",
                column: "ApprovalWorkflowId");

            migrationBuilder.CreateIndex(
                name: "IX_TR_DocumentWorkflow_DocumentId",
                table: "TR_DocumentWorkflow",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_TR_DocumentWorkflowAssignment_ApprovalWorkflowStageId",
                table: "TR_DocumentWorkflowAssignment",
                column: "ApprovalWorkflowStageId");

            migrationBuilder.CreateIndex(
                name: "IX_TR_DocumentWorkflowAssignment_AssignedUserId",
                table: "TR_DocumentWorkflowAssignment",
                column: "AssignedUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TR_DocumentWorkflowAssignment_DocumentWorkflowId_ApprovalWorkflowStageId_AssignedUserId",
                table: "TR_DocumentWorkflowAssignment",
                columns: new[] { "DocumentWorkflowId", "ApprovalWorkflowStageId", "AssignedUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_TR_DocumentWorkflowHistory_ActorUserId",
                table: "TR_DocumentWorkflowHistory",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TR_DocumentWorkflowHistory_ApprovalWorkflowStageId",
                table: "TR_DocumentWorkflowHistory",
                column: "ApprovalWorkflowStageId");

            migrationBuilder.CreateIndex(
                name: "IX_TR_DocumentWorkflowHistory_DocumentWorkflowId_ActionAt",
                table: "TR_DocumentWorkflowHistory",
                columns: new[] { "DocumentWorkflowId", "ActionAt" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "TR_DocumentWorkflowAssignment");
            migrationBuilder.DropTable(name: "TR_DocumentWorkflowHistory");
            migrationBuilder.DropTable(name: "TR_DocumentWorkflow");
            migrationBuilder.DropTable(name: "MD_ApprovalWorkflowStage");
            migrationBuilder.DropTable(name: "MD_ApprovalWorkflow");
        }
    }
}
