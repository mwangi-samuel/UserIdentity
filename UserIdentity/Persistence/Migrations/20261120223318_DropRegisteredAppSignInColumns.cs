using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UserIdentity.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DropRegisteredAppSignInColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "default_role",
                table: "registered_apps");

            migrationBuilder.DropColumn(
                name: "require_confirmed_account",
                table: "registered_apps");

            migrationBuilder.DropColumn(
                name: "require_invite_code",
                table: "registered_apps");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "default_role",
                table: "registered_apps",
                type: "character varying(155)",
                maxLength: 155,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "require_confirmed_account",
                table: "registered_apps",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "require_invite_code",
                table: "registered_apps",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}
