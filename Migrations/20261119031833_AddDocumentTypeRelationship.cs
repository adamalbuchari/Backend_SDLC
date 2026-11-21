
using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocuFlow.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentTypeRelationship : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Tabel MD_DocumentType sudah tersedia di database.
            // Karena itu, migration ini hanya menambahkan index
            // dan foreign key pada MD_ApprovalWorkflow.

            migrationBuilder.CreateIndex(
                name: "IX_MD_ApprovalWorkflow_DocumentTypeId",
                table: "MD_ApprovalWorkflow",
                column: "DocumentTypeId");

            migrationBuilder.AddForeignKey(
                name: "FK_MD_ApprovalWorkflow_MD_DocumentType_DocumentTypeId",
                table: "MD_ApprovalWorkflow",
                column: "DocumentTypeId",
                principalTable: "MD_DocumentType",
                principalColumn: "DocumentTypeId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MD_ApprovalWorkflow_MD_DocumentType_DocumentTypeId",
                table: "MD_ApprovalWorkflow");

            migrationBuilder.DropIndex(
                name: "IX_MD_ApprovalWorkflow_DocumentTypeId",
                table: "MD_ApprovalWorkflow");
        }
    }
}
