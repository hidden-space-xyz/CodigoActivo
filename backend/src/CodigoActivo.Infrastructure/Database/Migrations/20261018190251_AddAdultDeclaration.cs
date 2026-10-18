using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodigoActivo.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddAdultDeclaration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "adult_declared_at",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "adult_declared_at",
                table: "users");
        }
    }
}
