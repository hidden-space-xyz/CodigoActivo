using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodigoActivo.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class HardenOtpVerification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "otp_code",
                table: "users");

            migrationBuilder.AddColumn<string>(
                name: "otp_code_hash",
                table: "users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "otp_last_sent_at",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "otp_code_hash",
                table: "users");

            migrationBuilder.DropColumn(
                name: "otp_last_sent_at",
                table: "users");

            migrationBuilder.AddColumn<Guid>(
                name: "otp_code",
                table: "users",
                type: "uuid",
                nullable: true);
        }
    }
}
