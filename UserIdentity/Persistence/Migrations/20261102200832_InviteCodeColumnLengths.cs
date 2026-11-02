using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UserIdentity.Persistence.Migrations;

/// <inheritdoc />
public partial class InviteCodeColumnLengths : Migration
{
  /// <inheritdoc />
  protected override void Up(MigrationBuilder migrationBuilder)
  {
    migrationBuilder.AlterColumn<string>(
        name: "invite_code",
        table: "invite_codes",
        type: "varchar(255)",
        maxLength: 255,
        nullable: false,
        oldClrType: typeof(string),
        oldType: "longtext")
        .Annotation("MySql:CharSet", "utf8mb4")
        .OldAnnotation("MySql:CharSet", "utf8mb4");
  }

  /// <inheritdoc />
  protected override void Down(MigrationBuilder migrationBuilder)
  {
    migrationBuilder.AlterColumn<string>(
        name: "invite_code",
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
