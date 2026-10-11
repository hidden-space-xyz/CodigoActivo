using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodigoActivo.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class RestrictEventCategoryTypeDeletion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_event_categories_event_category_types_event_category_type_id",
                table: "event_categories");

            migrationBuilder.AddForeignKey(
                name: "fk_event_categories_event_category_types_event_category_type_id",
                table: "event_categories",
                column: "event_category_type_id",
                principalTable: "event_category_types",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_event_categories_event_category_types_event_category_type_id",
                table: "event_categories");

            migrationBuilder.AddForeignKey(
                name: "fk_event_categories_event_category_types_event_category_type_id",
                table: "event_categories",
                column: "event_category_type_id",
                principalTable: "event_category_types",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
