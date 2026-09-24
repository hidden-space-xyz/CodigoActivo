using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodigoActivo.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class RemoveUniquePhoneAndNationalId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_users_national_id",
                table: "users");

            migrationBuilder.DropIndex(
                name: "ix_users_phone",
                table: "users");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_users_national_id",
                table: "users",
                column: "national_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_users_phone",
                table: "users",
                column: "phone",
                unique: true);
        }
    }
}
