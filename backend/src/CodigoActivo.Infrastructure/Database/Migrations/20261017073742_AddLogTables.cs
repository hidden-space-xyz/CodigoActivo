using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodigoActivo.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddLogTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:pg_trgm", ",,");

            migrationBuilder.Sql(
                """
                CREATE TABLE audit_log_entries (
                    id uuid NOT NULL,
                    occurred_at timestamp with time zone NOT NULL,
                    actor_type character varying(100) NOT NULL,
                    actor_id uuid NULL,
                    client_ip inet NULL,
                    action character varying(100) NOT NULL,
                    entity_type character varying(100) NULL,
                    entity_id uuid NULL,
                    related jsonb NULL,
                    outcome character varying(100) NOT NULL,
                    error_code character varying(100) NULL,
                    trace_id character varying(32) NULL,
                    CONSTRAINT pk_audit_log_entries PRIMARY KEY (occurred_at, id)
                ) PARTITION BY RANGE (occurred_at);
                """);

            migrationBuilder.Sql(
                """
                CREATE TABLE diagnostic_log_entries (
                    id uuid NOT NULL,
                    occurred_at timestamp with time zone NOT NULL,
                    level smallint NOT NULL,
                    category character varying(256) NOT NULL,
                    event_name character varying(256) NULL,
                    message character varying(4000) NOT NULL,
                    exception character varying(32000) NULL,
                    trace_id character varying(32) NULL,
                    properties jsonb NULL,
                    CONSTRAINT pk_diagnostic_log_entries PRIMARY KEY (occurred_at, id)
                ) PARTITION BY RANGE (occurred_at);
                """);

            migrationBuilder.CreateIndex(
                name: "ix_audit_log_entries_action_occurred_at",
                table: "audit_log_entries",
                columns: new[] { "action", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_log_entries_actor_id_occurred_at",
                table: "audit_log_entries",
                columns: new[] { "actor_id", "occurred_at" },
                filter: "actor_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_audit_log_entries_client_ip_occurred_at",
                table: "audit_log_entries",
                columns: new[] { "client_ip", "occurred_at" },
                filter: "client_ip IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_audit_log_entries_entity_id_occurred_at",
                table: "audit_log_entries",
                columns: new[] { "entity_id", "occurred_at" },
                filter: "entity_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_audit_log_entries_related",
                table: "audit_log_entries",
                column: "related")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "jsonb_path_ops" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_log_entries_trace_id",
                table: "audit_log_entries",
                column: "trace_id",
                filter: "trace_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_diagnostic_log_entries_level_occurred_at",
                table: "diagnostic_log_entries",
                columns: new[] { "level", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_diagnostic_log_entries_message",
                table: "diagnostic_log_entries",
                column: "message")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "ix_diagnostic_log_entries_trace_id",
                table: "diagnostic_log_entries",
                column: "trace_id",
                filter: "trace_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_log_entries");

            migrationBuilder.DropTable(
                name: "diagnostic_log_entries");

            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:PostgresExtension:pg_trgm", ",,");
        }
    }
}
