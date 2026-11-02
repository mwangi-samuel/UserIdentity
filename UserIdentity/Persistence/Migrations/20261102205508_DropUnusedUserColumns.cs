using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UserIdentity.Persistence.Migrations;

/// <inheritdoc />
public partial class DropUnusedUserColumns : Migration
{
  /// <inheritdoc />
  protected override void Up(MigrationBuilder migrationBuilder)
  {
    migrationBuilder.DropColumn(
        name: "email_confirmation_token",
        table: "user_details");

    migrationBuilder.DropColumn(
        name: "remote_ip_address",
        table: "refresh_tokens");
  }

  /// <inheritdoc />
  protected override void Down(MigrationBuilder migrationBuilder)
  {
    migrationBuilder.AddColumn<string>(
        name: "email_confirmation_token",
        table: "user_details",
        type: "varchar(600)",
        maxLength: 600,
        nullable: true)
        .Annotation("MySql:CharSet", "utf8mb4");

    migrationBuilder.AddColumn<string>(
        name: "remote_ip_address",
        table: "refresh_tokens",
        type: "varchar(20)",
        maxLength: 20,
        nullable: true)
        .Annotation("MySql:CharSet", "utf8mb4");
  }
}
