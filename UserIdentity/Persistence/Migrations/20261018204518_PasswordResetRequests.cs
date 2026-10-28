using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UserIdentity.Persistence.Migrations;

/// <inheritdoc />
public partial class PasswordResetRequests : Migration
{
  /// <inheritdoc />
  protected override void Up(MigrationBuilder migrationBuilder)
  {
    migrationBuilder.DropColumn(
        name: "forgot_password_token",
        table: "user_details");

    migrationBuilder.CreateTable(
        name: "password_resets",
        columns: table => new
        {
          id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
          request_id = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4"),
          user_id = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4"),
          expires = table.Column<DateTime>(type: "datetime(6)", nullable: false),
          verified = table.Column<bool>(type: "tinyint(1)", nullable: false),
          applied = table.Column<bool>(type: "tinyint(1)", nullable: false),
          created_by = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4"),
          created_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
          updated_by = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4"),
          updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
          is_deleted = table.Column<bool>(type: "tinyint(1)", nullable: false)
        },
        constraints: table =>
        {
          table.PrimaryKey("pk_password_resets", x => x.id);
        })
        .Annotation("MySql:CharSet", "utf8mb4");

    migrationBuilder.CreateIndex(
        name: "ix_password_resets_request_id",
        table: "password_resets",
        column: "request_id",
        unique: true);

    migrationBuilder.CreateIndex(
        name: "ix_password_resets_user_id",
        table: "password_resets",
        column: "user_id");
  }

  /// <inheritdoc />
  protected override void Down(MigrationBuilder migrationBuilder)
  {
    migrationBuilder.DropTable(
        name: "password_resets");

    migrationBuilder.AddColumn<string>(
        name: "forgot_password_token",
        table: "user_details",
        type: "varchar(600)",
        maxLength: 600,
        nullable: true)
        .Annotation("MySql:CharSet", "utf8mb4");
  }
}
