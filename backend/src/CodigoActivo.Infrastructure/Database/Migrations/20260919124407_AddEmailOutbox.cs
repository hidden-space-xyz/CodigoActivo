using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodigoActivo.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailOutbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "email_outbox_content_parts");

            migrationBuilder.DropTable(
                name: "email_outbox_messages");

            migrationBuilder.DropTable(
                name: "email_outbox_contents");
        }
    }
}
