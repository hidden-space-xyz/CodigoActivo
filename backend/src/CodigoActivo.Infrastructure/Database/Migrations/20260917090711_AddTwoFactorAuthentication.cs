using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodigoActivo.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddTwoFactorAuthentication : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "authenticator_key",
                table: "users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "authenticator_last_used_step",
                table: "users",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "login_code_expires_at",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "login_code_hash",
                table: "users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "login_code_last_sent_at",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "pending_authenticator_expires_at",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "pending_authenticator_key",
                table: "users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "two_factor_failed_attempts",
                table: "users",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "two_factor_locked_until",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "two_factor_method",
                table: "users",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Email");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "authenticator_key",
                table: "users");

            migrationBuilder.DropColumn(
                name: "authenticator_last_used_step",
                table: "users");

            migrationBuilder.DropColumn(
                name: "login_code_expires_at",
                table: "users");

            migrationBuilder.DropColumn(
                name: "login_code_hash",
                table: "users");

            migrationBuilder.DropColumn(
                name: "login_code_last_sent_at",
                table: "users");

            migrationBuilder.DropColumn(
                name: "pending_authenticator_expires_at",
                table: "users");

            migrationBuilder.DropColumn(
                name: "pending_authenticator_key",
                table: "users");

            migrationBuilder.DropColumn(
                name: "two_factor_failed_attempts",
                table: "users");

            migrationBuilder.DropColumn(
                name: "two_factor_locked_until",
                table: "users");

            migrationBuilder.DropColumn(
                name: "two_factor_method",
                table: "users");
        }
    }
}
