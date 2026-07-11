using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodigoActivo.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class SimplifyUserTypeCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "hidden",
                table: "user_types");

            migrationBuilder.DropColumn(
                name: "is_allowed_for_adults",
                table: "user_types");

            migrationBuilder.DropColumn(
                name: "is_allowed_for_minors",
                table: "user_types");

            migrationBuilder.Sql(
                """
                INSERT INTO user_types (id, name, description, color)
                VALUES (
                    '8e0b7dc4-59d3-4c3b-9a71-4f25c6b0de88',
                    'Patrocinador',
                    'Persona o entidad que respalda a la asociación aportando recursos o financiación para que sus actividades sean posibles.',
                    '#EAB308'
                )
                ON CONFLICT (id) DO NOTHING;

                UPDATE users
                SET user_type_id = '8e0b7dc4-59d3-4c3b-9a71-4f25c6b0de88'
                WHERE user_type_id = 'c26c7755-b5db-42fe-b349-e83a84481fea'
                  AND id::text LIKE 'dede0001-0000-0000-0000-%';

                UPDATE users
                SET user_type_id = '1c038ae8-306f-4785-a5f5-b9c25e5cc4aa'
                WHERE user_type_id = 'c26c7755-b5db-42fe-b349-e83a84481fea';

                DELETE FROM user_types
                WHERE id = 'c26c7755-b5db-42fe-b349-e83a84481fea';

                UPDATE user_types
                SET color = '#3B82F6'
                WHERE id = '1c038ae8-306f-4785-a5f5-b9c25e5cc4aa';

                UPDATE user_types
                SET color = '#EF4444'
                WHERE id = 'b0df7ac6-1312-412f-9c2a-88e6cdfb6e1c';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "hidden",
                table: "user_types",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "is_allowed_for_adults",
                table: "user_types",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "is_allowed_for_minors",
                table: "user_types",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql(
                """
                UPDATE user_types
                SET is_allowed_for_adults = true, is_allowed_for_minors = true
                WHERE id = 'b0df7ac6-1312-412f-9c2a-88e6cdfb6e1c';

                UPDATE user_types
                SET is_allowed_for_minors = true, color = '#FFFFFF'
                WHERE id = '1c038ae8-306f-4785-a5f5-b9c25e5cc4aa';

                UPDATE user_types
                SET hidden = true
                WHERE id = '8e0b7dc4-59d3-4c3b-9a71-4f25c6b0de88';
                """);
        }
    }
}
