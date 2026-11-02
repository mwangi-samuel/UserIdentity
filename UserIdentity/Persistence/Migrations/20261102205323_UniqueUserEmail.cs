using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UserIdentity.Persistence.Migrations;

/// <inheritdoc />
public partial class UniqueUserEmail : Migration
{
  /// <inheritdoc />
  protected override void Up(MigrationBuilder migrationBuilder)
  {
    migrationBuilder.DropIndex(
        name: "EmailIndex",
        table: "users");

    migrationBuilder.CreateIndex(
        name: "EmailIndex",
        table: "users",
        column: "normalized_email",
        unique: true);
  }

  /// <inheritdoc />
  protected override void Down(MigrationBuilder migrationBuilder)
  {
    migrationBuilder.DropIndex(
        name: "EmailIndex",
        table: "users");

    migrationBuilder.CreateIndex(
        name: "EmailIndex",
        table: "users",
        column: "normalized_email");
  }
}
