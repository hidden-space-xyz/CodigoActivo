using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodigoActivo.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddPasswordLockout : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "password_failed_attempts",
                table: "users",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "password_locked_at",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "password_failed_attempts",
                table: "users");

            migrationBuilder.DropColumn(
                name: "password_locked_at",
                table: "users");
        }
    }
}
