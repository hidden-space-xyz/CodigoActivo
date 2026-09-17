using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodigoActivo.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddMultipleEventTermsDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Create the bridge table first, while events.terms_document_id still exists, so the
            //    backfill below can read from it.
            migrationBuilder.CreateTable(
                name: "event_terms_documents",
                columns: table => new
                {
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    terms_document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_required = table.Column<bool>(type: "boolean", nullable: false),
                    display_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_event_terms_documents", x => new { x.event_id, x.terms_document_id });
                    table.ForeignKey(
                        name: "fk_event_terms_documents_events_event_id",
                        column: x => x.event_id,
                        principalTable: "events",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_event_terms_documents_terms_documents_terms_document_id",
                        column: x => x.terms_document_id,
                        principalTable: "terms_documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_event_terms_documents_terms_document_id",
                table: "event_terms_documents",
                column: "terms_document_id");

            // 2. Backfill: every event that already had a single terms document becomes a required,
            //    first-position (display_order 0) link in the bridge table.
            migrationBuilder.Sql(
                "INSERT INTO event_terms_documents (event_id, terms_document_id, is_required, display_order) "
                    + "SELECT id, terms_document_id, true, 0 FROM events WHERE terms_document_id IS NOT NULL;"
            );

            // 3. The single-document column and its supporting foreign key/index are no longer
            //    needed once the bridge table carries the same information.
            migrationBuilder.DropForeignKey(
                name: "fk_events_terms_documents_terms_document_id",
                table: "events");

            migrationBuilder.DropIndex(
                name: "ix_events_terms_document_id",
                table: "events");

            migrationBuilder.DropColumn(
                name: "terms_document_id",
                table: "events");

            // 4. Widen event_terms_acceptances to carry one row per decided document instead of one
            //    row per event. Existing rows only ever recorded an acceptance (there was no
            //    rejection concept before), so they backfill as accepted = true.
            migrationBuilder.DropPrimaryKey(
                name: "pk_event_terms_acceptances",
                table: "event_terms_acceptances");

            migrationBuilder.Sql(
                "ALTER TABLE event_terms_acceptances ADD COLUMN accepted boolean NOT NULL DEFAULT true;"
            );
            migrationBuilder.Sql(
                "ALTER TABLE event_terms_acceptances ALTER COLUMN accepted DROP DEFAULT;"
            );
            migrationBuilder.RenameColumn(
                name: "accepted_at",
                table: "event_terms_acceptances",
                newName: "decided_at");

            // The primary key widens to (event_id, user_id, terms_document_id); this does not
            // collide with any existing row because (event_id, user_id) was already unique.
            migrationBuilder.AddPrimaryKey(
                name: "pk_event_terms_acceptances",
                table: "event_terms_acceptances",
                columns: new[] { "event_id", "user_id", "terms_document_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The Down migration loses data: a user's multiple decisions per event (including any
            // rejections) collapse back into a single accepted/rejected marker, and an event's
            // multiple linked documents collapse into the single one that was required first. See
            // DEPLOYMENT.md for the required backup before reverting this migration in production.
            migrationBuilder.Sql("DELETE FROM event_terms_acceptances WHERE accepted = false;");
            migrationBuilder.Sql(
                """
                DELETE FROM event_terms_acceptances a USING (
                    SELECT event_id, user_id, min((decided_at, terms_document_id)) AS keep
                    FROM event_terms_acceptances GROUP BY event_id, user_id
                ) k
                WHERE a.event_id = k.event_id AND a.user_id = k.user_id
                    AND (a.decided_at, a.terms_document_id) <> k.keep;
                """
            );

            migrationBuilder.DropPrimaryKey(
                name: "pk_event_terms_acceptances",
                table: "event_terms_acceptances");

            migrationBuilder.RenameColumn(
                name: "decided_at",
                table: "event_terms_acceptances",
                newName: "accepted_at");

            migrationBuilder.DropColumn(
                name: "accepted",
                table: "event_terms_acceptances");

            migrationBuilder.AddPrimaryKey(
                name: "pk_event_terms_acceptances",
                table: "event_terms_acceptances",
                columns: new[] { "event_id", "user_id" });

            migrationBuilder.AddColumn<Guid>(
                name: "terms_document_id",
                table: "events",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE events e SET terms_document_id = (
                    SELECT d.terms_document_id FROM event_terms_documents d WHERE d.event_id = e.id
                    ORDER BY d.is_required DESC, d.display_order, d.terms_document_id LIMIT 1
                );
                """
            );

            migrationBuilder.CreateIndex(
                name: "ix_events_terms_document_id",
                table: "events",
                column: "terms_document_id");

            migrationBuilder.AddForeignKey(
                name: "fk_events_terms_documents_terms_document_id",
                table: "events",
                column: "terms_document_id",
                principalTable: "terms_documents",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.DropTable(
                name: "event_terms_documents");
        }
    }
}
