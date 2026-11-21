using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UserIdentity.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SessionDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_refresh_tokens_user_id",
                table: "refresh_tokens");

            migrationBuilder.AddColumn<int>(
                name: "app_id",
                table: "refresh_tokens",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "device",
                table: "refresh_tokens",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ip_address",
                table: "refresh_tokens",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_app_id",
                table: "refresh_tokens",
                column: "app_id");

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_user_id_app_id",
                table: "refresh_tokens",
                columns: new[] { "user_id", "app_id" });

            migrationBuilder.AddForeignKey(
                name: "fk_refresh_tokens_registered_app_app_id",
                table: "refresh_tokens",
                column: "app_id",
                principalTable: "registered_apps",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_refresh_tokens_registered_app_app_id",
                table: "refresh_tokens");

            migrationBuilder.DropIndex(
                name: "ix_refresh_tokens_app_id",
                table: "refresh_tokens");

            migrationBuilder.DropIndex(
                name: "ix_refresh_tokens_user_id_app_id",
                table: "refresh_tokens");

            migrationBuilder.DropColumn(
                name: "app_id",
                table: "refresh_tokens");

            migrationBuilder.DropColumn(
                name: "device",
                table: "refresh_tokens");

            migrationBuilder.DropColumn(
                name: "ip_address",
                table: "refresh_tokens");

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_user_id",
                table: "refresh_tokens",
                column: "user_id");
        }
    }
}
