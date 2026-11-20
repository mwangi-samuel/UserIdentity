using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace UserIdentity.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SignInStepsAndTwoFactor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "callback_headers",
                table: "registered_apps");

            migrationBuilder.DropColumn(
                name: "callback_url",
                table: "registered_apps");

            migrationBuilder.DropColumn(
                name: "forward_service_token",
                table: "registered_apps");

            migrationBuilder.DropColumn(
                name: "verified",
                table: "password_resets");

            migrationBuilder.AddColumn<string>(
                name: "two_factor_method",
                table: "user_details",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "app_secret_key",
                table: "registered_apps",
                type: "character varying(600)",
                maxLength: 600,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(600)",
                oldMaxLength: 600);

            migrationBuilder.AddColumn<int>(
                name: "app_id",
                table: "password_resets",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "changing_method",
                table: "login_tickets",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "code_action",
                table: "login_tickets",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "code_correlation_id",
                table: "login_tickets",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "email_proven",
                table: "login_tickets",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "method",
                table: "login_tickets",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "phone_number",
                table: "login_tickets",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "step",
                table: "login_tickets",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "account_limit_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<string>(type: "text", nullable: false),
                    kind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_account_limit_events", x => x.id);
                    table.ForeignKey(
                        name: "fk_account_limit_events_asp_net_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "app_sign_in_settings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    app_id = table.Column<int>(type: "integer", nullable: false),
                    require_confirmed_account = table.Column<bool>(type: "boolean", nullable: false),
                    require_invite_code = table.Column<bool>(type: "boolean", nullable: false),
                    default_role = table.Column<string>(type: "character varying(155)", maxLength: 155, nullable: true),
                    require_two_factor = table.Column<bool>(type: "boolean", nullable: false),
                    require_two_factor_for_admins = table.Column<bool>(type: "boolean", nullable: false),
                    allow_authenticator_two_factor = table.Column<bool>(type: "boolean", nullable: false),
                    allow_email_two_factor = table.Column<bool>(type: "boolean", nullable: false),
                    allow_sms_two_factor = table.Column<bool>(type: "boolean", nullable: false),
                    created_by = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_app_sign_in_settings", x => x.id);
                    table.ForeignKey(
                        name: "fk_app_sign_in_settings_registered_app_app_id",
                        column: x => x.app_id,
                        principalTable: "registered_apps",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "email_changes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<string>(type: "text", nullable: false),
                    new_email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    expires = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_email_changes", x => x.id);
                    table.ForeignKey(
                        name: "fk_email_changes_asp_net_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "registered_app_backends",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    registered_app_id = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    public_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    next_public_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_registered_app_backends", x => x.id);
                    table.ForeignKey(
                        name: "fk_registered_app_backends_registered_apps_registered_app_id",
                        column: x => x.registered_app_id,
                        principalTable: "registered_apps",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_password_resets_app_id",
                table: "password_resets",
                column: "app_id");

            migrationBuilder.CreateIndex(
                name: "ix_account_limit_events_created_at",
                table: "account_limit_events",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_account_limit_events_user_id_kind_created_at",
                table: "account_limit_events",
                columns: new[] { "user_id", "kind", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_app_sign_in_settings_app_id",
                table: "app_sign_in_settings",
                column: "app_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_email_changes_expires",
                table: "email_changes",
                column: "expires");

            migrationBuilder.CreateIndex(
                name: "ix_email_changes_user_id",
                table: "email_changes",
                column: "user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_registered_app_backends_registered_app_id_name",
                table: "registered_app_backends",
                columns: new[] { "registered_app_id", "name" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_password_resets_registered_apps_app_id",
                table: "password_resets",
                column: "app_id",
                principalTable: "registered_apps",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_password_resets_registered_apps_app_id",
                table: "password_resets");

            migrationBuilder.DropTable(
                name: "account_limit_events");

            migrationBuilder.DropTable(
                name: "app_sign_in_settings");

            migrationBuilder.DropTable(
                name: "email_changes");

            migrationBuilder.DropTable(
                name: "registered_app_backends");

            migrationBuilder.DropIndex(
                name: "ix_password_resets_app_id",
                table: "password_resets");

            migrationBuilder.DropColumn(
                name: "two_factor_method",
                table: "user_details");

            migrationBuilder.DropColumn(
                name: "app_id",
                table: "password_resets");

            migrationBuilder.DropColumn(
                name: "changing_method",
                table: "login_tickets");

            migrationBuilder.DropColumn(
                name: "code_action",
                table: "login_tickets");

            migrationBuilder.DropColumn(
                name: "code_correlation_id",
                table: "login_tickets");

            migrationBuilder.DropColumn(
                name: "email_proven",
                table: "login_tickets");

            migrationBuilder.DropColumn(
                name: "method",
                table: "login_tickets");

            migrationBuilder.DropColumn(
                name: "phone_number",
                table: "login_tickets");

            migrationBuilder.DropColumn(
                name: "step",
                table: "login_tickets");

            migrationBuilder.AlterColumn<string>(
                name: "app_secret_key",
                table: "registered_apps",
                type: "character varying(600)",
                maxLength: 600,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(600)",
                oldMaxLength: 600,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "callback_headers",
                table: "registered_apps",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "callback_url",
                table: "registered_apps",
                type: "character varying(600)",
                maxLength: 600,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "forward_service_token",
                table: "registered_apps",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "verified",
                table: "password_resets",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}
