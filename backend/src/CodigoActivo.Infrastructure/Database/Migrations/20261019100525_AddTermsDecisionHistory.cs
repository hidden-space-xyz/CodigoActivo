using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CodigoActivo.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddTermsDecisionHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "terms_decision_changes",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    terms_document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    accepted = table.Column<bool>(type: "boolean", nullable: false),
                    decided_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recorded_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    signature = table.Column<byte[]>(type: "bytea", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_terms_decision_changes", x => x.id);
                    table.ForeignKey(
                        name: "fk_terms_decision_changes_event_terms_acceptances_event_id_use",
                        columns: x => new { x.event_id, x.user_id, x.terms_document_id },
                        principalTable: "event_terms_acceptances",
                        principalColumns: new[] { "event_id", "user_id", "terms_document_id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_terms_decision_changes_users_decided_by_id",
                        column: x => x.decided_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_terms_decision_changes_users_recorded_by_id",
                        column: x => x.recorded_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_terms_decision_changes_decided_by_id",
                table: "terms_decision_changes",
                column: "decided_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_terms_decision_changes_event_id_user_id_terms_document_id",
                table: "terms_decision_changes",
                columns: new[] { "event_id", "user_id", "terms_document_id" });

            migrationBuilder.CreateIndex(
                name: "ix_terms_decision_changes_recorded_by_id",
                table: "terms_decision_changes",
                column: "recorded_by_id");

            migrationBuilder.Sql(
                """
                INSERT INTO terms_decision_changes
                    (event_id, user_id, terms_document_id, accepted, decided_by_id, recorded_by_id, changed_at, signature)
                SELECT event_id, user_id, terms_document_id, accepted, decided_by_id, decided_by_id, decided_at, signature
                FROM event_terms_acceptances
                ORDER BY decided_at;
                """
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "terms_decision_changes");
        }
    }
}
