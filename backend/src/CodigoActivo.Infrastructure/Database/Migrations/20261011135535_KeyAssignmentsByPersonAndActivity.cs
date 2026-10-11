using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodigoActivo.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class KeyAssignmentsByPersonAndActivity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "pk_assignments",
                table: "assignments");

            migrationBuilder.DropIndex(
                name: "ix_assignments_user_id_activity_id",
                table: "assignments");

            migrationBuilder.AddPrimaryKey(
                name: "pk_assignments",
                table: "assignments",
                columns: new[] { "user_id", "activity_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "pk_assignments",
                table: "assignments");

            migrationBuilder.AddPrimaryKey(
                name: "pk_assignments",
                table: "assignments",
                columns: new[] { "user_id", "activity_id", "activity_role_type_id" });

            migrationBuilder.CreateIndex(
                name: "ix_assignments_user_id_activity_id",
                table: "assignments",
                columns: new[] { "user_id", "activity_id" },
                unique: true);
        }
    }
}
