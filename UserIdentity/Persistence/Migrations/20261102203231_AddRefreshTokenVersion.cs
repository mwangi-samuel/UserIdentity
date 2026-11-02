using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UserIdentity.Persistence.Migrations;

/// <inheritdoc />
public partial class AddRefreshTokenVersion : Migration
{
  /// <inheritdoc />
  protected override void Up(MigrationBuilder migrationBuilder)
  {
    migrationBuilder.AddColumn<Guid>(
        name: "version",
        table: "refresh_tokens",
        type: "char(36)",
        nullable: false,
        defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
        collation: "ascii_general_ci");
  }

  /// <inheritdoc />
  protected override void Down(MigrationBuilder migrationBuilder)
  {
    migrationBuilder.DropColumn(
        name: "version",
        table: "refresh_tokens");
  }
}
