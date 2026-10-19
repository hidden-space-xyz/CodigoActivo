using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodigoActivo.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class VersionTermsDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "version",
                table: "terms_documents",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "terms_document_version",
                table: "terms_decision_changes",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "terms_document_version",
                table: "event_terms_acceptances",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "terms_document_versions",
                columns: table => new
                {
                    terms_document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "jsonb", nullable: false),
                    requires_signature = table.Column<bool>(type: "boolean", nullable: false),
                    kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_terms_document_versions", x => new { x.terms_document_id, x.number });
                    table.ForeignKey(
                        name: "fk_terms_document_versions_terms_documents_terms_document_id",
                        column: x => x.terms_document_id,
                        principalTable: "terms_documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "terms_document_versions");

            migrationBuilder.DropColumn(
                name: "version",
                table: "terms_documents");

            migrationBuilder.DropColumn(
                name: "terms_document_version",
                table: "terms_decision_changes");

            migrationBuilder.DropColumn(
                name: "terms_document_version",
                table: "event_terms_acceptances");
        }
    }
}
