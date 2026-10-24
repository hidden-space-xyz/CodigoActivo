using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CodigoActivo.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:pg_trgm", ",,");

            migrationBuilder.CreateTable(
                name: "activity_modality_types",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_activity_modality_types", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "activity_role_types",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_activity_role_types", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "assignment_status_types",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    color = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: false),
                    name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_assignment_status_types", x => x.id);
                });

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

            migrationBuilder.CreateTable(
                name: "deleted_accounts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    data = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_deleted_accounts", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "deleted_participations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    data = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_deleted_participations", x => x.id);
                });

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

            migrationBuilder.CreateTable(
                name: "disposable_email_domains",
                columns: table => new
                {
                    domain = table.Column<string>(type: "character varying(253)", maxLength: 253, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_disposable_email_domains", x => x.domain);
                });

            migrationBuilder.CreateTable(
                name: "email_outbox_contents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    subject = table.Column<byte[]>(type: "bytea", nullable: false),
                    html_body = table.Column<byte[]>(type: "bytea", nullable: false),
                    text_body = table.Column<byte[]>(type: "bytea", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_email_outbox_contents", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "event_category_types",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    color = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_event_category_types", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "legal_policies",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    content = table.Column<string>(type: "jsonb", nullable: false),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_legal_policies", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "resource_types",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    color = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: false),
                    is_external = table.Column<bool>(type: "boolean", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_resource_types", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "terms_documents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "jsonb", nullable: false),
                    requires_signature = table.Column<bool>(type: "boolean", nullable: false),
                    kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_terms_documents", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "user_status_types",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    color = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: false),
                    name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_status_types", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "user_types",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    color = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: false),
                    name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_types", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "email_outbox_content_parts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    content_id = table.Column<Guid>(type: "uuid", nullable: false),
                    disposition = table.Column<string>(type: "text", nullable: false),
                    file_name = table.Column<string>(type: "text", nullable: false),
                    content_type = table.Column<string>(type: "text", nullable: false),
                    inline_content_id = table.Column<string>(type: "text", nullable: true),
                    payload = table.Column<byte[]>(type: "bytea", nullable: false),
                    display_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_email_outbox_content_parts", x => x.id);
                    table.ForeignKey(
                        name: "fk_email_outbox_content_parts_email_outbox_contents_content_id",
                        column: x => x.content_id,
                        principalTable: "email_outbox_contents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "email_outbox_messages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    content_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "text", nullable: false),
                    priority = table.Column<int>(type: "integer", nullable: false),
                    to_address = table.Column<string>(type: "text", nullable: false),
                    to_name = table.Column<string>(type: "text", nullable: false),
                    unsubscribe_url = table.Column<string>(type: "text", nullable: true),
                    one_click_unsubscribe_url = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    attempt_count = table.Column<int>(type: "integer", nullable: false),
                    next_attempt_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    locked_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_error = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_email_outbox_messages", x => x.id);
                    table.ForeignKey(
                        name: "fk_email_outbox_messages_email_outbox_contents_content_id",
                        column: x => x.content_id,
                        principalTable: "email_outbox_contents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "legal_policy_versions",
                columns: table => new
                {
                    legal_policy_id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<int>(type: "integer", nullable: false),
                    content = table.Column<string>(type: "jsonb", nullable: false),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_legal_policy_versions", x => new { x.legal_policy_id, x.number });
                    table.ForeignKey(
                        name: "fk_legal_policy_versions_legal_policies_legal_policy_id",
                        column: x => x.legal_policy_id,
                        principalTable: "legal_policies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "terms_document_versions",
                columns: table => new
                {
                    terms_document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "jsonb", nullable: false),
                    requires_signature = table.Column<bool>(type: "boolean", nullable: false),
                    kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_terms_document_versions", x => new { x.terms_document_id, x.number });
                    table.ForeignKey(
                        name: "fk_terms_document_versions_terms_documents_terms_document_id",
                        column: x => x.terms_document_id,
                        principalTable: "terms_documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    first_name = table.Column<string>(type: "text", nullable: false),
                    last_name = table.Column<string>(type: "text", nullable: false),
                    email = table.Column<string>(type: "text", nullable: true),
                    phone = table.Column<string>(type: "text", nullable: true),
                    secondary_phone = table.Column<string>(type: "text", nullable: true),
                    password_hash = table.Column<string>(type: "text", nullable: true),
                    birth_date = table.Column<DateOnly>(type: "date", nullable: true),
                    national_id = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: true),
                    news_consent = table.Column<bool>(type: "boolean", nullable: false),
                    gender = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    last_login_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    adult_declared_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    guardianship_declared_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    coming_of_age_notice_sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    parent_id = table.Column<Guid>(type: "uuid", nullable: true),
                    user_status_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_admin = table.Column<bool>(type: "boolean", nullable: false),
                    two_factor_method = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    authenticator_key = table.Column<string>(type: "text", nullable: true),
                    authenticator_last_used_step = table.Column<long>(type: "bigint", nullable: true),
                    pending_authenticator_expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    pending_authenticator_key = table.Column<string>(type: "text", nullable: true),
                    email_change_code_hash = table.Column<string>(type: "text", nullable: true),
                    email_change_expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    pending_email = table.Column<string>(type: "text", nullable: true),
                    login_challenge_id = table.Column<Guid>(type: "uuid", nullable: true),
                    login_challenge_started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    login_code_expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    login_code_hash = table.Column<string>(type: "text", nullable: true),
                    login_code_last_sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    password_failed_attempts = table.Column<int>(type: "integer", nullable: false),
                    password_locked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    password_reset_expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    password_reset_code_hash = table.Column<string>(type: "text", nullable: true),
                    password_reset_last_sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    two_factor_failed_attempts = table.Column<int>(type: "integer", nullable: false),
                    two_factor_locked_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    otp_expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    otp_code_hash = table.Column<string>(type: "text", nullable: true),
                    otp_last_sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_users", x => x.id);
                    table.ForeignKey(
                        name: "fk_users_user_status_types_user_status_type_id",
                        column: x => x.user_status_type_id,
                        principalTable: "user_status_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_users_user_types_user_type_id",
                        column: x => x.user_type_id,
                        principalTable: "user_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_users_users_parent_id",
                        column: x => x.parent_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "files",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    extension = table.Column<string>(type: "text", nullable: false),
                    uploaded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    uploaded_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_files", x => x.id);
                    table.ForeignKey(
                        name: "fk_files_users_uploaded_by",
                        column: x => x.uploaded_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "news_consent_changes",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    granted = table.Column<bool>(type: "boolean", nullable: false),
                    source = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_news_consent_changes", x => x.id);
                    table.ForeignKey(
                        name: "fk_news_consent_changes_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_sessions", x => x.id);
                    table.ForeignKey(
                        name: "fk_user_sessions_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    subtitle = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "jsonb", nullable: false),
                    featured = table.Column<bool>(type: "boolean", nullable: false),
                    thumbnail_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_starts_at = table.Column<DateOnly>(type: "date", nullable: false),
                    event_ends_at = table.Column<DateOnly>(type: "date", nullable: false),
                    early_signup_starts_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    signup_starts_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    signup_ends_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_events", x => x.id);
                    table.ForeignKey(
                        name: "fk_events_files_thumbnail_id",
                        column: x => x.thumbnail_id,
                        principalTable: "files",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_events_users_created_by",
                        column: x => x.created_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_events_users_updated_by",
                        column: x => x.updated_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "news",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    subtitle = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "jsonb", nullable: false),
                    featured = table.Column<bool>(type: "boolean", nullable: false),
                    thumbnail_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_news", x => x.id);
                    table.ForeignKey(
                        name: "fk_news_files_thumbnail_id",
                        column: x => x.thumbnail_id,
                        principalTable: "files",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_news_users_created_by",
                        column: x => x.created_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_news_users_updated_by",
                        column: x => x.updated_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "partners",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    from_date = table.Column<DateOnly>(type: "date", nullable: false),
                    tier = table.Column<int>(type: "integer", nullable: false),
                    web = table.Column<string>(type: "text", nullable: true),
                    thumbnail_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_partners", x => x.id);
                    table.ForeignKey(
                        name: "fk_partners_files_thumbnail_id",
                        column: x => x.thumbnail_id,
                        principalTable: "files",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_partners_users_created_by",
                        column: x => x.created_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_partners_users_updated_by",
                        column: x => x.updated_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "resources",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    subtitle = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "jsonb", nullable: false),
                    url = table.Column<string>(type: "text", nullable: true),
                    resource_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    thumbnail_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_resources", x => x.id);
                    table.ForeignKey(
                        name: "fk_resources_files_thumbnail_id",
                        column: x => x.thumbnail_id,
                        principalTable: "files",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_resources_resource_types_resource_type_id",
                        column: x => x.resource_type_id,
                        principalTable: "resource_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_resources_users_created_by",
                        column: x => x.created_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_resources_users_updated_by",
                        column: x => x.updated_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "activities",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    location = table.Column<string>(type: "text", nullable: false),
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    activity_modality_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    thumbnail_id = table.Column<Guid>(type: "uuid", nullable: false),
                    activity_starts_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    activity_ends_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_activities", x => x.id);
                    table.ForeignKey(
                        name: "fk_activities_activity_modality_types_activity_modality_type_id",
                        column: x => x.activity_modality_type_id,
                        principalTable: "activity_modality_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_activities_events_event_id",
                        column: x => x.event_id,
                        principalTable: "events",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_activities_files_thumbnail_id",
                        column: x => x.thumbnail_id,
                        principalTable: "files",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_activities_users_created_by",
                        column: x => x.created_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_activities_users_updated_by",
                        column: x => x.updated_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "event_categories",
                columns: table => new
                {
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_category_type_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_event_categories", x => new { x.event_id, x.event_category_type_id });
                    table.ForeignKey(
                        name: "fk_event_categories_event_category_types_event_category_type_id",
                        column: x => x.event_category_type_id,
                        principalTable: "event_category_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_event_categories_events_event_id",
                        column: x => x.event_id,
                        principalTable: "events",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "event_ratings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    score = table.Column<int>(type: "integer", nullable: true),
                    most_liked = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    least_liked = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    suggestions = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_event_ratings", x => x.id);
                    table.ForeignKey(
                        name: "fk_event_ratings_events_event_id",
                        column: x => x.event_id,
                        principalTable: "events",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_event_ratings_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "event_terms_acceptances",
                columns: table => new
                {
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    terms_document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    decided_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    terms_document_version = table.Column<int>(type: "integer", nullable: false),
                    accepted = table.Column<bool>(type: "boolean", nullable: false),
                    decided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    signature = table.Column<byte[]>(type: "bytea", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_event_terms_acceptances", x => new { x.event_id, x.user_id, x.terms_document_id });
                    table.ForeignKey(
                        name: "fk_event_terms_acceptances_events_event_id",
                        column: x => x.event_id,
                        principalTable: "events",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_event_terms_acceptances_terms_documents_terms_document_id",
                        column: x => x.terms_document_id,
                        principalTable: "terms_documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_event_terms_acceptances_users_decided_by_id",
                        column: x => x.decided_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_event_terms_acceptances_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "event_terms_documents",
                columns: table => new
                {
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    terms_document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_required = table.Column<bool>(type: "boolean", nullable: false),
                    display_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_event_terms_documents", x => new { x.event_id, x.terms_document_id });
                    table.ForeignKey(
                        name: "fk_event_terms_documents_events_event_id",
                        column: x => x.event_id,
                        principalTable: "events",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_event_terms_documents_terms_documents_terms_document_id",
                        column: x => x.terms_document_id,
                        principalTable: "terms_documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "activity_role_capacities",
                columns: table => new
                {
                    activity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    activity_role_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    desired_count = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_activity_role_capacities", x => new { x.activity_id, x.activity_role_type_id });
                    table.ForeignKey(
                        name: "fk_activity_role_capacities_activities_activity_id",
                        column: x => x.activity_id,
                        principalTable: "activities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_activity_role_capacities_activity_role_types_activity_role_",
                        column: x => x.activity_role_type_id,
                        principalTable: "activity_role_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "assignments",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    activity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    activity_role_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assignment_status_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_assignments", x => new { x.user_id, x.activity_id });
                    table.ForeignKey(
                        name: "fk_assignments_activities_activity_id",
                        column: x => x.activity_id,
                        principalTable: "activities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_assignments_activity_role_types_activity_role_type_id",
                        column: x => x.activity_role_type_id,
                        principalTable: "activity_role_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_assignments_assignment_status_types_assignment_status_id",
                        column: x => x.assignment_status_id,
                        principalTable: "assignment_status_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_assignments_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "terms_decision_changes",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    terms_document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    terms_document_version = table.Column<int>(type: "integer", nullable: false),
                    accepted = table.Column<bool>(type: "boolean", nullable: false),
                    decided_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recorded_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    signature = table.Column<byte[]>(type: "bytea", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_terms_decision_changes", x => x.id);
                    table.ForeignKey(
                        name: "fk_terms_decision_changes_event_terms_acceptances_event_id_use",
                        columns: x => new { x.event_id, x.user_id, x.terms_document_id },
                        principalTable: "event_terms_acceptances",
                        principalColumns: new[] { "event_id", "user_id", "terms_document_id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_terms_decision_changes_users_decided_by_id",
                        column: x => x.decided_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_terms_decision_changes_users_recorded_by_id",
                        column: x => x.recorded_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_activities_activity_modality_type_id",
                table: "activities",
                column: "activity_modality_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_activities_activity_starts_at",
                table: "activities",
                column: "activity_starts_at");

            migrationBuilder.CreateIndex(
                name: "ix_activities_created_by",
                table: "activities",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "ix_activities_event_id_activity_starts_at",
                table: "activities",
                columns: new[] { "event_id", "activity_starts_at" });

            migrationBuilder.CreateIndex(
                name: "ix_activities_thumbnail_id",
                table: "activities",
                column: "thumbnail_id");

            migrationBuilder.CreateIndex(
                name: "ix_activities_updated_by",
                table: "activities",
                column: "updated_by");

            migrationBuilder.CreateIndex(
                name: "ix_activity_modality_types_name",
                table: "activity_modality_types",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_activity_role_capacities_activity_role_type_id",
                table: "activity_role_capacities",
                column: "activity_role_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_activity_role_types_name",
                table: "activity_role_types",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_assignment_status_types_name",
                table: "assignment_status_types",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_assignments_activity_id",
                table: "assignments",
                column: "activity_id");

            migrationBuilder.CreateIndex(
                name: "ix_assignments_activity_role_type_id",
                table: "assignments",
                column: "activity_role_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_assignments_assignment_status_id",
                table: "assignments",
                column: "assignment_status_id");

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
                name: "ix_deleted_accounts_deleted_at",
                table: "deleted_accounts",
                column: "deleted_at");

            migrationBuilder.CreateIndex(
                name: "ix_deleted_participations_deleted_at",
                table: "deleted_participations",
                column: "deleted_at");

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

            migrationBuilder.CreateIndex(
                name: "ix_email_outbox_content_parts_content_id",
                table: "email_outbox_content_parts",
                column: "content_id");

            migrationBuilder.CreateIndex(
                name: "ix_email_outbox_messages_content_id",
                table: "email_outbox_messages",
                column: "content_id");

            migrationBuilder.CreateIndex(
                name: "ix_email_outbox_messages_priority_next_attempt_at",
                table: "email_outbox_messages",
                columns: new[] { "priority", "next_attempt_at" });

            migrationBuilder.CreateIndex(
                name: "ix_event_categories_event_category_type_id",
                table: "event_categories",
                column: "event_category_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_event_category_types_name",
                table: "event_category_types",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_event_ratings_event_id_user_id",
                table: "event_ratings",
                columns: new[] { "event_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_event_ratings_user_id",
                table: "event_ratings",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_event_terms_acceptances_decided_by_id",
                table: "event_terms_acceptances",
                column: "decided_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_event_terms_acceptances_terms_document_id",
                table: "event_terms_acceptances",
                column: "terms_document_id");

            migrationBuilder.CreateIndex(
                name: "ix_event_terms_acceptances_user_id",
                table: "event_terms_acceptances",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_event_terms_documents_terms_document_id",
                table: "event_terms_documents",
                column: "terms_document_id");

            migrationBuilder.CreateIndex(
                name: "ix_events_created_by",
                table: "events",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "ix_events_event_ends_at",
                table: "events",
                column: "event_ends_at");

            migrationBuilder.CreateIndex(
                name: "ix_events_event_starts_at",
                table: "events",
                column: "event_starts_at");

            migrationBuilder.CreateIndex(
                name: "ix_events_featured",
                table: "events",
                column: "featured",
                unique: true,
                filter: "featured");

            migrationBuilder.CreateIndex(
                name: "ix_events_thumbnail_id",
                table: "events",
                column: "thumbnail_id");

            migrationBuilder.CreateIndex(
                name: "ix_events_updated_by",
                table: "events",
                column: "updated_by");

            migrationBuilder.CreateIndex(
                name: "ix_files_uploaded_by",
                table: "files",
                column: "uploaded_by");

            migrationBuilder.CreateIndex(
                name: "ix_legal_policies_kind",
                table: "legal_policies",
                column: "kind",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_news_created_at",
                table: "news",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_news_created_by",
                table: "news",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "ix_news_featured",
                table: "news",
                column: "featured",
                unique: true,
                filter: "featured");

            migrationBuilder.CreateIndex(
                name: "ix_news_thumbnail_id",
                table: "news",
                column: "thumbnail_id");

            migrationBuilder.CreateIndex(
                name: "ix_news_updated_by",
                table: "news",
                column: "updated_by");

            migrationBuilder.CreateIndex(
                name: "ix_news_consent_changes_user_id",
                table: "news_consent_changes",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_partners_created_by",
                table: "partners",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "ix_partners_thumbnail_id",
                table: "partners",
                column: "thumbnail_id");

            migrationBuilder.CreateIndex(
                name: "ix_partners_tier_from_date",
                table: "partners",
                columns: new[] { "tier", "from_date" });

            migrationBuilder.CreateIndex(
                name: "ix_partners_updated_by",
                table: "partners",
                column: "updated_by");

            migrationBuilder.CreateIndex(
                name: "ix_resource_types_name",
                table: "resource_types",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_resources_created_at",
                table: "resources",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_resources_created_by",
                table: "resources",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "ix_resources_resource_type_id",
                table: "resources",
                column: "resource_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_resources_thumbnail_id",
                table: "resources",
                column: "thumbnail_id");

            migrationBuilder.CreateIndex(
                name: "ix_resources_updated_by",
                table: "resources",
                column: "updated_by");

            migrationBuilder.CreateIndex(
                name: "ix_terms_decision_changes_decided_by_id",
                table: "terms_decision_changes",
                column: "decided_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_terms_decision_changes_event_id_user_id_terms_document_id",
                table: "terms_decision_changes",
                columns: new[] { "event_id", "user_id", "terms_document_id" });

            migrationBuilder.CreateIndex(
                name: "ix_terms_decision_changes_recorded_by_id",
                table: "terms_decision_changes",
                column: "recorded_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_terms_documents_name",
                table: "terms_documents",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_sessions_user_id",
                table: "user_sessions",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_status_types_name",
                table: "user_status_types",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_types_name",
                table: "user_types",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_users_email",
                table: "users",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_users_first_name_last_name",
                table: "users",
                columns: new[] { "first_name", "last_name" });

            migrationBuilder.CreateIndex(
                name: "ix_users_parent_id",
                table: "users",
                column: "parent_id");

            migrationBuilder.CreateIndex(
                name: "ix_users_user_status_type_id",
                table: "users",
                column: "user_status_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_users_user_type_id",
                table: "users",
                column: "user_type_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "activity_role_capacities");

            migrationBuilder.DropTable(
                name: "assignments");

            migrationBuilder.DropTable(
                name: "audit_log_entries");

            migrationBuilder.DropTable(
                name: "deleted_accounts");

            migrationBuilder.DropTable(
                name: "deleted_participations");

            migrationBuilder.DropTable(
                name: "diagnostic_log_entries");

            migrationBuilder.DropTable(
                name: "disposable_email_domains");

            migrationBuilder.DropTable(
                name: "email_outbox_content_parts");

            migrationBuilder.DropTable(
                name: "email_outbox_messages");

            migrationBuilder.DropTable(
                name: "event_categories");

            migrationBuilder.DropTable(
                name: "event_ratings");

            migrationBuilder.DropTable(
                name: "event_terms_documents");

            migrationBuilder.DropTable(
                name: "legal_policy_versions");

            migrationBuilder.DropTable(
                name: "news");

            migrationBuilder.DropTable(
                name: "news_consent_changes");

            migrationBuilder.DropTable(
                name: "partners");

            migrationBuilder.DropTable(
                name: "resources");

            migrationBuilder.DropTable(
                name: "terms_decision_changes");

            migrationBuilder.DropTable(
                name: "terms_document_versions");

            migrationBuilder.DropTable(
                name: "user_sessions");

            migrationBuilder.DropTable(
                name: "activities");

            migrationBuilder.DropTable(
                name: "activity_role_types");

            migrationBuilder.DropTable(
                name: "assignment_status_types");

            migrationBuilder.DropTable(
                name: "email_outbox_contents");

            migrationBuilder.DropTable(
                name: "event_category_types");

            migrationBuilder.DropTable(
                name: "legal_policies");

            migrationBuilder.DropTable(
                name: "resource_types");

            migrationBuilder.DropTable(
                name: "event_terms_acceptances");

            migrationBuilder.DropTable(
                name: "activity_modality_types");

            migrationBuilder.DropTable(
                name: "events");

            migrationBuilder.DropTable(
                name: "terms_documents");

            migrationBuilder.DropTable(
                name: "files");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "user_status_types");

            migrationBuilder.DropTable(
                name: "user_types");
        }
    }
}
