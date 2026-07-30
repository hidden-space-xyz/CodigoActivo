using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodigoActivo.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddUserGender : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "gender",
                table: "users",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.Sql("UPDATE users SET gender = 'Other' WHERE gender IS NULL;");

            migrationBuilder.AlterColumn<string>(
                name: "gender",
                table: "users",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(16)",
                oldMaxLength: 16,
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "gender",
                table: "users");
        }
    }
}
