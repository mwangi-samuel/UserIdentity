using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UserIdentity.Persistence.Migrations
{
  /// <inheritdoc />
  public partial class ScopeInviteCodesAndWaitListsByApp : Migration
  {
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
      // Create before dropping: MySQL won't drop the app_id indexes while the foreign keys have no other index to use
      migrationBuilder.CreateIndex(
          name: "ix_wait_lists_app_id_user_email",
          table: "wait_lists",
          columns: new[] { "app_id", "user_email" },
          unique: true);

      migrationBuilder.CreateIndex(
          name: "ix_invite_codes_app_id_user_email",
          table: "invite_codes",
          columns: new[] { "app_id", "user_email" },
          unique: true);

      migrationBuilder.DropIndex(
          name: "ix_wait_lists_app_id",
          table: "wait_lists");

      migrationBuilder.DropIndex(
          name: "ix_wait_lists_user_email",
          table: "wait_lists");

      migrationBuilder.DropIndex(
          name: "ix_invite_codes_app_id",
          table: "invite_codes");

      migrationBuilder.DropIndex(
          name: "ix_invite_codes_user_email",
          table: "invite_codes");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
      migrationBuilder.CreateIndex(
          name: "ix_wait_lists_app_id",
          table: "wait_lists",
          column: "app_id");

      migrationBuilder.CreateIndex(
          name: "ix_wait_lists_user_email",
          table: "wait_lists",
          column: "user_email");

      migrationBuilder.CreateIndex(
          name: "ix_invite_codes_app_id",
          table: "invite_codes",
          column: "app_id");

      migrationBuilder.CreateIndex(
          name: "ix_invite_codes_user_email",
          table: "invite_codes",
          column: "user_email");

      migrationBuilder.DropIndex(
          name: "ix_wait_lists_app_id_user_email",
          table: "wait_lists");

      migrationBuilder.DropIndex(
          name: "ix_invite_codes_app_id_user_email",
          table: "invite_codes");
    }
  }
}
