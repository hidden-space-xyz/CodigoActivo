using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodigoActivo.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddResourceTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "resource_types",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    color = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: false),
                    is_external = table.Column<bool>(type: "boolean", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_resource_types", x => x.id);
                });

            migrationBuilder.InsertData(
                table: "resource_types",
                columns: new[] { "id", "color", "is_external", "name", "description" },
                values: new object[,]
                {
                    {
                        new Guid("47833436-b131-4536-87a6-737a9680a423"),
                        "#3B82F6",
                        false,
                        "Interno",
                        "Material propio alojado en la plataforma. Incluye una descripción completa que se consulta desde la propia web."
                    },
                    {
                        new Guid("d4b28595-eed5-4728-ad87-8eaf9d8ce754"),
                        "#F97316",
                        true,
                        "Externo",
                        "Material publicado en otro sitio web. Al abrirlo se redirige directamente al enlace original."
                    }
                });

            migrationBuilder.AddColumn<Guid>(
                name: "resource_type_id",
                table: "resources",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("47833436-b131-4536-87a6-737a9680a423"));

            migrationBuilder.AlterColumn<Guid>(
                name: "resource_type_id",
                table: "resources",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldDefaultValue: new Guid("47833436-b131-4536-87a6-737a9680a423"));

            migrationBuilder.AddColumn<string>(
                name: "url",
                table: "resources",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_resources_resource_type_id",
                table: "resources",
                column: "resource_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_resource_types_name",
                table: "resource_types",
                column: "name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_resources_resource_types_resource_type_id",
                table: "resources",
                column: "resource_type_id",
                principalTable: "resource_types",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_resources_resource_types_resource_type_id",
                table: "resources");

            migrationBuilder.DropTable(
                name: "resource_types");

            migrationBuilder.DropIndex(
                name: "ix_resources_resource_type_id",
                table: "resources");

            migrationBuilder.DropColumn(
                name: "resource_type_id",
                table: "resources");

            migrationBuilder.DropColumn(
                name: "url",
                table: "resources");
        }
    }
}
