using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodigoActivo.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddListQueryIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_activities_event_id",
                table: "activities");

            migrationBuilder.CreateIndex(
                name: "ix_users_first_name_last_name",
                table: "users",
                columns: new[] { "first_name", "last_name" });

            migrationBuilder.CreateIndex(
                name: "ix_resources_created_at",
                table: "resources",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_partners_tier_from_date",
                table: "partners",
                columns: new[] { "tier", "from_date" });

            migrationBuilder.CreateIndex(
                name: "ix_events_event_ends_at",
                table: "events",
                column: "event_ends_at");

            migrationBuilder.CreateIndex(
                name: "ix_events_event_starts_at",
                table: "events",
                column: "event_starts_at");

            migrationBuilder.CreateIndex(
                name: "ix_announcements_created_at",
                table: "announcements",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_activities_activity_starts_at",
                table: "activities",
                column: "activity_starts_at");

            migrationBuilder.CreateIndex(
                name: "ix_activities_event_id_activity_starts_at",
                table: "activities",
                columns: new[] { "event_id", "activity_starts_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_users_first_name_last_name",
                table: "users");

            migrationBuilder.DropIndex(
                name: "ix_resources_created_at",
                table: "resources");

            migrationBuilder.DropIndex(
                name: "ix_partners_tier_from_date",
                table: "partners");

            migrationBuilder.DropIndex(
                name: "ix_events_event_ends_at",
                table: "events");

            migrationBuilder.DropIndex(
                name: "ix_events_event_starts_at",
                table: "events");

            migrationBuilder.DropIndex(
                name: "ix_announcements_created_at",
                table: "announcements");

            migrationBuilder.DropIndex(
                name: "ix_activities_activity_starts_at",
                table: "activities");

            migrationBuilder.DropIndex(
                name: "ix_activities_event_id_activity_starts_at",
                table: "activities");

            migrationBuilder.CreateIndex(
                name: "ix_activities_event_id",
                table: "activities",
                column: "event_id");
        }
    }
}
