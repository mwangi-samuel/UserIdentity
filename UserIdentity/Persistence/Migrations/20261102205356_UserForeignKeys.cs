using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UserIdentity.Persistence.Migrations;

/// <inheritdoc />
public partial class UserForeignKeys : Migration
{
  /// <inheritdoc />
  protected override void Up(MigrationBuilder migrationBuilder)
  {
    migrationBuilder.UpdateData(
        table: "refresh_tokens",
        keyColumn: "user_id",
        keyValue: null,
        column: "user_id",
        value: "");

    migrationBuilder.AlterColumn<string>(
        name: "user_id",
        table: "refresh_tokens",
        type: "varchar(255)",
        nullable: false,
        oldClrType: typeof(string),
        oldType: "varchar(50)",
        oldMaxLength: 50,
        oldNullable: true)
        .Annotation("MySql:CharSet", "utf8mb4")
        .OldAnnotation("MySql:CharSet", "utf8mb4");

    migrationBuilder.AlterColumn<string>(
        name: "user_id",
        table: "password_resets",
        type: "varchar(255)",
        nullable: false,
        oldClrType: typeof(string),
        oldType: "varchar(50)",
        oldMaxLength: 50)
        .Annotation("MySql:CharSet", "utf8mb4")
        .OldAnnotation("MySql:CharSet", "utf8mb4");

    migrationBuilder.AddForeignKey(
        name: "fk_password_resets_asp_net_users_user_id",
        table: "password_resets",
        column: "user_id",
        principalTable: "users",
        principalColumn: "id",
        onDelete: ReferentialAction.Cascade);

    migrationBuilder.AddForeignKey(
        name: "fk_refresh_tokens_asp_net_users_user_id",
        table: "refresh_tokens",
        column: "user_id",
        principalTable: "users",
        principalColumn: "id",
        onDelete: ReferentialAction.Cascade);

    migrationBuilder.AddForeignKey(
        name: "fk_user_details_asp_net_users_id",
        table: "user_details",
        column: "id",
        principalTable: "users",
        principalColumn: "id",
        onDelete: ReferentialAction.Cascade);
  }

  /// <inheritdoc />
  protected override void Down(MigrationBuilder migrationBuilder)
  {
    migrationBuilder.DropForeignKey(
        name: "fk_password_resets_asp_net_users_user_id",
        table: "password_resets");

    migrationBuilder.DropForeignKey(
        name: "fk_refresh_tokens_asp_net_users_user_id",
        table: "refresh_tokens");

    migrationBuilder.DropForeignKey(
        name: "fk_user_details_asp_net_users_id",
        table: "user_details");

    migrationBuilder.AlterColumn<string>(
        name: "user_id",
        table: "refresh_tokens",
        type: "varchar(50)",
        maxLength: 50,
        nullable: true,
        oldClrType: typeof(string),
        oldType: "varchar(255)")
        .Annotation("MySql:CharSet", "utf8mb4")
        .OldAnnotation("MySql:CharSet", "utf8mb4");

    migrationBuilder.AlterColumn<string>(
        name: "user_id",
        table: "password_resets",
        type: "varchar(50)",
        maxLength: 50,
        nullable: false,
        oldClrType: typeof(string),
        oldType: "varchar(255)")
        .Annotation("MySql:CharSet", "utf8mb4")
        .OldAnnotation("MySql:CharSet", "utf8mb4");
  }
}
