using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UserIdentity.Persistence.Migrations;

/// <inheritdoc />
public partial class RefreshTokenRotation : Migration
{
  /// <inheritdoc />
  protected override void Up(MigrationBuilder migrationBuilder)
  {
    // Refresh tokens are now stored as hashes, so the existing plain text tokens can no longer be used
    migrationBuilder.Sql("DELETE FROM refresh_tokens;");

    migrationBuilder.AddColumn<string>(
        name: "previous_token",
        table: "refresh_tokens",
        type: "varchar(200)",
        maxLength: 200,
        nullable: true)
        .Annotation("MySql:CharSet", "utf8mb4");

    migrationBuilder.CreateIndex(
        name: "ix_refresh_tokens_previous_token",
        table: "refresh_tokens",
        column: "previous_token");
  }

  /// <inheritdoc />
  protected override void Down(MigrationBuilder migrationBuilder)
  {
    migrationBuilder.DropIndex(
        name: "ix_refresh_tokens_previous_token",
        table: "refresh_tokens");

    migrationBuilder.DropColumn(
        name: "previous_token",
        table: "refresh_tokens");
  }
}
