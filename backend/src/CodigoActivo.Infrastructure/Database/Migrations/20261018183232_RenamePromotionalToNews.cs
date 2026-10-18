using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodigoActivo.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class RenamePromotionalToNews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "promotional_consent",
                table: "users",
                newName: "news_consent");

            migrationBuilder.RenameTable(
                name: "promotional_consent_changes",
                newName: "news_consent_changes");

            migrationBuilder.RenameIndex(
                name: "ix_promotional_consent_changes_user_id",
                table: "news_consent_changes",
                newName: "ix_news_consent_changes_user_id");

            migrationBuilder.Sql(
                """
                ALTER TABLE news_consent_changes RENAME CONSTRAINT pk_promotional_consent_changes TO pk_news_consent_changes;
                ALTER TABLE news_consent_changes RENAME CONSTRAINT fk_promotional_consent_changes_users_user_id TO fk_news_consent_changes_users_user_id;
                ALTER SEQUENCE promotional_consent_changes_id_seq RENAME TO news_consent_changes_id_seq;
                UPDATE deleted_accounts
                SET data = replace(replace(data::text, '"promotionalConsentChanges":', '"newsConsentChanges":'), '"promotionalConsent":', '"newsConsent":')::jsonb
                WHERE data::text LIKE '%"promotionalConsent%';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE deleted_accounts
                SET data = replace(replace(data::text, '"newsConsentChanges":', '"promotionalConsentChanges":'), '"newsConsent":', '"promotionalConsent":')::jsonb
                WHERE data::text LIKE '%"newsConsent%';
                ALTER SEQUENCE news_consent_changes_id_seq RENAME TO promotional_consent_changes_id_seq;
                ALTER TABLE news_consent_changes RENAME CONSTRAINT fk_news_consent_changes_users_user_id TO fk_promotional_consent_changes_users_user_id;
                ALTER TABLE news_consent_changes RENAME CONSTRAINT pk_news_consent_changes TO pk_promotional_consent_changes;
                """);

            migrationBuilder.RenameIndex(
                name: "ix_news_consent_changes_user_id",
                table: "news_consent_changes",
                newName: "ix_promotional_consent_changes_user_id");

            migrationBuilder.RenameTable(
                name: "news_consent_changes",
                newName: "promotional_consent_changes");

            migrationBuilder.RenameColumn(
                name: "news_consent",
                table: "users",
                newName: "promotional_consent");
        }
    }
}
