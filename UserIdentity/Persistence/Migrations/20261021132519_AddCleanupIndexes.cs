using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UserIdentity.Persistence.Migrations
{
  /// <inheritdoc />
  public partial class AddCleanupIndexes : Migration
  {
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
      migrationBuilder.AlterColumn<string>(
          name: "user_email",
          table: "invite_codes",
          type: "varchar(255)",
          maxLength: 255,
          nullable: false,
          oldClrType: typeof(string),
          oldType: "longtext")
          .Annotation("MySql:CharSet", "utf8mb4")
          .OldAnnotation("MySql:CharSet", "utf8mb4");

      migrationBuilder.CreateIndex(
          name: "ix_user_details_created_at",
          table: "user_details",
          column: "created_at");

      migrationBuilder.CreateIndex(
          name: "ix_refresh_tokens_expires",
          table: "refresh_tokens",
          column: "expires");

      migrationBuilder.CreateIndex(
          name: "ix_password_resets_expires",
          table: "password_resets",
          column: "expires");

      migrationBuilder.CreateIndex(
          name: "ix_invite_codes_user_email",
          table: "invite_codes",
          column: "user_email");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
      migrationBuilder.DropIndex(
          name: "ix_user_details_created_at",
          table: "user_details");

      migrationBuilder.DropIndex(
          name: "ix_refresh_tokens_expires",
          table: "refresh_tokens");

      migrationBuilder.DropIndex(
          name: "ix_password_resets_expires",
          table: "password_resets");

      migrationBuilder.DropIndex(
          name: "ix_invite_codes_user_email",
          table: "invite_codes");

      migrationBuilder.AlterColumn<string>(
          name: "user_email",
          table: "invite_codes",
          type: "longtext",
          nullable: false,
          oldClrType: typeof(string),
          oldType: "varchar(255)",
          oldMaxLength: 255)
          .Annotation("MySql:CharSet", "utf8mb4")
          .OldAnnotation("MySql:CharSet", "utf8mb4");
    }
  }
}
