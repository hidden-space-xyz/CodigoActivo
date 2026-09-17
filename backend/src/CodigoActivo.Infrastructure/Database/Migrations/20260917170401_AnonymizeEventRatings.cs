using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodigoActivo.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AnonymizeEventRatings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Create the submission table first, while event_ratings still carries user_id, so the
            //    population step below can read from it.
            migrationBuilder.CreateTable(
                name: "event_rating_submissions",
                columns: table => new
                {
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_event_rating_submissions", x => new { x.event_id, x.user_id });
                    table.ForeignKey(
                        name: "fk_event_rating_submissions_events_event_id",
                        column: x => x.event_id,
                        principalTable: "events",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_event_rating_submissions_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            // 2. Populate the submissions from the existing ratings in a random row order. This is what
            //    breaks the link between a rating's physical insertion order and its author: the
            //    submission table ends up with an unrelated row order, so nothing (including a raw
            //    heap/ctid scan) can pair a submission with "the Nth rating for this event" anymore.
            migrationBuilder.Sql(
                "INSERT INTO event_rating_submissions (event_id, user_id) "
                    + "SELECT event_id, user_id FROM event_ratings ORDER BY random();"
            );

            // 3. Drop the columns and indexes that made event_ratings identifiable and user-specific.
            migrationBuilder.DropForeignKey(
                name: "fk_event_ratings_users_user_id",
                table: "event_ratings");

            migrationBuilder.DropIndex(
                name: "ix_event_ratings_event_id_user_id",
                table: "event_ratings");

            migrationBuilder.DropIndex(
                name: "ix_event_ratings_user_id",
                table: "event_ratings");

            migrationBuilder.DropColumn(
                name: "created_at",
                table: "event_ratings");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "event_ratings");

            migrationBuilder.DropColumn(
                name: "user_id",
                table: "event_ratings");

            migrationBuilder.CreateIndex(
                name: "ix_event_ratings_event_id",
                table: "event_ratings",
                column: "event_id");

            migrationBuilder.CreateIndex(
                name: "ix_event_rating_submissions_user_id",
                table: "event_rating_submissions",
                column: "user_id");

            // 4. DROP COLUMN does not create new tuple versions, so every event_ratings row still
            //    carries the "xmin" (the id of the transaction that produced it) of whatever original
            //    INSERT created it. Historical ratings span many distinct original transactions, so
            //    their xmin values still order them chronologically even after the columns above are
            //    gone. CLUSTER further down does not fix this either: like VACUUM FULL, it copies
            //    existing tuple versions into a new file instead of creating new ones, so it leaves
            //    xmin untouched. A real DELETE + INSERT, run once here inside this migration's
            //    transaction, creates fresh tuple versions for every row stamped with this single
            //    transaction id, so all migrated ratings end up sharing one xmin regardless of when
            //    they were originally submitted. ORDER BY random() additionally scrambles the
            //    intermediate insertion order that CLUSTER would otherwise still have to work from.
            migrationBuilder.Sql(
                """
                WITH moved AS (
                    DELETE FROM event_ratings
                    RETURNING id, event_id, score, most_liked, least_liked, suggestions
                )
                INSERT INTO event_ratings (id, event_id, score, most_liked, least_liked, suggestions)
                SELECT id, event_id, score, most_liked, least_liked, suggestions FROM moved
                ORDER BY random();
                """
            );

            // 5. CLUSTER rewrites each table's physical (heap/ctid) row order according to an index
            //    built from keys that carry no author correlation (event_ratings.id is a random v4
            //    Guid; event_rating_submissions is keyed by event_id/user_id), erasing any remaining
            //    physical ordering trace on top of the xmin rewrite above. Both CLUSTER statements run
            //    inside this migration's transaction.
            migrationBuilder.Sql("CLUSTER event_ratings USING pk_event_ratings;");
            migrationBuilder.Sql("CLUSTER event_rating_submissions USING pk_event_rating_submissions;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The anonymization performed by Up is not reversible: event_ratings no longer stores
            // who submitted each rating, and event_rating_submissions was populated (and later
            // rewritten) in a shuffled, uncorrelated order. Recreating the old columns would only be
            // able to fill them with placeholder values, silently presenting fabricated authorship
            // and timestamps as real data. Failing loudly is safer than a Down migration that runs
            // successfully but destroys the very information it claims to restore.
            throw new NotSupportedException(
                "The AnonymizeEventRatings migration cannot be reverted: it permanently discards the "
                    + "link between a rating and the user who submitted it, so there is no data to "
                    + "restore the previous user_id/created_at/updated_at columns with."
            );
        }
    }
}
