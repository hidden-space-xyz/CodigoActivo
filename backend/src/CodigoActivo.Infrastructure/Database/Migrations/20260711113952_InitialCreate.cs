using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodigoActivo.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
                    name = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false)
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
                    color = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_assignment_status_types", x => x.id);
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

            migrationBuilder.CreateTable(
                name: "terms_documents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "jsonb", nullable: false)
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
                    color = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false)
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
                    color = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false)
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
                    promotional_consent = table.Column<bool>(type: "boolean", nullable: false),
                    gender = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    last_login_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    parent_id = table.Column<Guid>(type: "uuid", nullable: true),
                    user_status_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_admin = table.Column<bool>(type: "boolean", nullable: false),
                    otp_code_hash = table.Column<string>(type: "text", nullable: true),
                    otp_expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    otp_last_sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    password_reset_code_hash = table.Column<string>(type: "text", nullable: true),
                    password_reset_expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    password_reset_last_sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    two_factor_method = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    authenticator_key = table.Column<string>(type: "text", nullable: true),
                    authenticator_last_used_step = table.Column<long>(type: "bigint", nullable: true),
                    pending_authenticator_key = table.Column<string>(type: "text", nullable: true),
                    pending_authenticator_expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    login_code_hash = table.Column<string>(type: "text", nullable: true),
                    login_code_expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    login_code_last_sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    login_challenge_id = table.Column<Guid>(type: "uuid", nullable: true),
                    two_factor_failed_attempts = table.Column<int>(type: "integer", nullable: false),
                    two_factor_locked_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    password_failed_attempts = table.Column<int>(type: "integer", nullable: false),
                    password_locked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
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
                name: "announcements",
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
                    table.PrimaryKey("pk_announcements", x => x.id);
                    table.ForeignKey(
                        name: "fk_announcements_files_thumbnail_id",
                        column: x => x.thumbnail_id,
                        principalTable: "files",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_announcements_users_created_by",
                        column: x => x.created_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_announcements_users_updated_by",
                        column: x => x.updated_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    subtitle = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "jsonb", nullable: false),
                    event_starts_at = table.Column<DateOnly>(type: "date", nullable: false),
                    event_ends_at = table.Column<DateOnly>(type: "date", nullable: false),
                    early_signup_starts_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    signup_starts_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    signup_ends_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    featured = table.Column<bool>(type: "boolean", nullable: false),
                    thumbnail_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    activity_starts_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    activity_ends_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    activity_modality_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    thumbnail_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                        onDelete: ReferentialAction.Cascade);
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
                    score = table.Column<int>(type: "integer", nullable: false),
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
                });

            migrationBuilder.CreateTable(
                name: "event_terms_acceptances",
                columns: table => new
                {
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    terms_document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    accepted = table.Column<bool>(type: "boolean", nullable: false),
                    decided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
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
                name: "activity_user_role_assignments",
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
                    table.PrimaryKey("pk_activity_user_role_assignments", x => new { x.user_id, x.activity_id, x.activity_role_type_id });
                    table.ForeignKey(
                        name: "fk_activity_user_role_assignments_activities_activity_id",
                        column: x => x.activity_id,
                        principalTable: "activities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_activity_user_role_assignments_activity_role_types_activity",
                        column: x => x.activity_role_type_id,
                        principalTable: "activity_role_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_activity_user_role_assignments_assignment_status_types_assi",
                        column: x => x.assignment_status_id,
                        principalTable: "assignment_status_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_activity_user_role_assignments_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
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
                name: "ix_activity_user_role_assignments_activity_id",
                table: "activity_user_role_assignments",
                column: "activity_id");

            migrationBuilder.CreateIndex(
                name: "ix_activity_user_role_assignments_activity_role_type_id",
                table: "activity_user_role_assignments",
                column: "activity_role_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_activity_user_role_assignments_assignment_status_id",
                table: "activity_user_role_assignments",
                column: "assignment_status_id");

            migrationBuilder.CreateIndex(
                name: "ix_announcements_created_at",
                table: "announcements",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_announcements_created_by",
                table: "announcements",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "ix_announcements_thumbnail_id",
                table: "announcements",
                column: "thumbnail_id");

            migrationBuilder.CreateIndex(
                name: "ix_announcements_updated_by",
                table: "announcements",
                column: "updated_by");

            migrationBuilder.CreateIndex(
                name: "ix_assignment_status_types_name",
                table: "assignment_status_types",
                column: "name",
                unique: true);

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
                name: "ix_event_ratings_event_id",
                table: "event_ratings",
                column: "event_id");

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
                name: "activity_user_role_assignments");

            migrationBuilder.DropTable(
                name: "announcements");

            migrationBuilder.DropTable(
                name: "email_outbox_content_parts");

            migrationBuilder.DropTable(
                name: "email_outbox_messages");

            migrationBuilder.DropTable(
                name: "event_categories");

            migrationBuilder.DropTable(
                name: "event_ratings");

            migrationBuilder.DropTable(
                name: "event_terms_acceptances");

            migrationBuilder.DropTable(
                name: "event_terms_documents");

            migrationBuilder.DropTable(
                name: "partners");

            migrationBuilder.DropTable(
                name: "resources");

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
                name: "terms_documents");

            migrationBuilder.DropTable(
                name: "resource_types");

            migrationBuilder.DropTable(
                name: "activity_modality_types");

            migrationBuilder.DropTable(
                name: "events");

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
