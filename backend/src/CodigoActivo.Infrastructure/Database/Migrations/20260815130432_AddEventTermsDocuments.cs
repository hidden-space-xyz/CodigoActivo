using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodigoActivo.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddEventTermsDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "terms_document_id",
                table: "events",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "terms_documents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_terms_documents", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "event_terms_acceptances",
                columns: table => new
                {
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    terms_document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    accepted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_event_terms_acceptances", x => new { x.event_id, x.user_id });
                    table.ForeignKey(
                        name: "fk_event_terms_acceptances_events_event_id",
                        column: x => x.event_id,
                        principalTable: "events",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_event_terms_acceptances_terms_documents_terms_document_id",
                        column: x => x.terms_document_id,
                        principalTable: "terms_documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_event_terms_acceptances_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_events_terms_document_id",
                table: "events",
                column: "terms_document_id");

            migrationBuilder.CreateIndex(
                name: "ix_event_terms_acceptances_terms_document_id",
                table: "event_terms_acceptances",
                column: "terms_document_id");

            migrationBuilder.CreateIndex(
                name: "ix_event_terms_acceptances_user_id",
                table: "event_terms_acceptances",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_terms_documents_name",
                table: "terms_documents",
                column: "name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_events_terms_documents_terms_document_id",
                table: "events",
                column: "terms_document_id",
                principalTable: "terms_documents",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_events_terms_documents_terms_document_id",
                table: "events");

            migrationBuilder.DropTable(
                name: "event_terms_acceptances");

            migrationBuilder.DropTable(
                name: "terms_documents");

            migrationBuilder.DropIndex(
                name: "ix_events_terms_document_id",
                table: "events");

            migrationBuilder.DropColumn(
                name: "terms_document_id",
                table: "events");
        }
    }
}
