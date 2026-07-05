using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodigoActivo.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceUserTypeAssignmentsWithIsAdminAndSingleType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_type_assignments");

            migrationBuilder.AddColumn<bool>(
                name: "is_admin",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "user_type_id",
                table: "users",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "ix_users_user_type_id",
                table: "users",
                column: "user_type_id");

            migrationBuilder.AddForeignKey(
                name: "fk_users_user_types_user_type_id",
                table: "users",
                column: "user_type_id",
                principalTable: "user_types",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_users_user_types_user_type_id",
                table: "users");

            migrationBuilder.DropIndex(
                name: "ix_users_user_type_id",
                table: "users");

            migrationBuilder.DropColumn(
                name: "is_admin",
                table: "users");

            migrationBuilder.DropColumn(
                name: "user_type_id",
                table: "users");

            migrationBuilder.CreateTable(
                name: "user_type_assignments",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assigned_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_type_assignments", x => new { x.user_id, x.user_type_id });
                    table.ForeignKey(
                        name: "fk_user_type_assignments_user_types_user_type_id",
                        column: x => x.user_type_id,
                        principalTable: "user_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_user_type_assignments_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_user_type_assignments_user_type_id",
                table: "user_type_assignments",
                column: "user_type_id");
        }
    }
}
