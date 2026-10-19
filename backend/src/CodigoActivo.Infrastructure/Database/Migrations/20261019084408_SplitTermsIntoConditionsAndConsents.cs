using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodigoActivo.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class SplitTermsIntoConditionsAndConsents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "kind",
                table: "terms_documents",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Conditions");

            migrationBuilder.AddColumn<Guid>(
                name: "decided_by_id",
                table: "event_terms_acceptances",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql("UPDATE event_terms_acceptances SET decided_by_id = user_id;");

            migrationBuilder.AlterColumn<Guid>(
                name: "decided_by_id",
                table: "event_terms_acceptances",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_event_terms_acceptances_decided_by_id",
                table: "event_terms_acceptances",
                column: "decided_by_id");

            migrationBuilder.AddForeignKey(
                name: "fk_event_terms_acceptances_users_decided_by_id",
                table: "event_terms_acceptances",
                column: "decided_by_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_event_terms_acceptances_users_decided_by_id",
                table: "event_terms_acceptances");

            migrationBuilder.DropIndex(
                name: "ix_event_terms_acceptances_decided_by_id",
                table: "event_terms_acceptances");

            migrationBuilder.DropColumn(
                name: "kind",
                table: "terms_documents");

            migrationBuilder.DropColumn(
                name: "decided_by_id",
                table: "event_terms_acceptances");
        }
    }
}
