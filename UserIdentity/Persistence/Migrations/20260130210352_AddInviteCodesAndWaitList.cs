using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UserIdentity.Persistence.Migrations;

/// <inheritdoc />
public partial class AddInviteCodesAndWaitList : Migration
{
  /// <inheritdoc />
  protected override void Up(MigrationBuilder migrationBuilder)
  {
    migrationBuilder.AddColumn<bool>(
        name: "require_invite_code",
        table: "registered_apps",
        type: "tinyint(1)",
        nullable: false,
        defaultValue: false);

    migrationBuilder.CreateTable(
        name: "invite_codes",
        columns: table => new
        {
          id = table.Column<long>(type: "bigint", nullable: false)
                .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
          invite_code = table.Column<string>(type: "longtext", nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4"),
          user_email = table.Column<string>(type: "longtext", nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4"),
          applied = table.Column<bool>(type: "tinyint(1)", nullable: false),
          app_id = table.Column<int>(type: "int", nullable: false),
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
          table.PrimaryKey("pk_invite_codes", x => x.id);
          table.ForeignKey(
                    name: "fk_invite_codes_registered_apps_app_id",
                    column: x => x.app_id,
                    principalTable: "registered_apps",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
        })
        .Annotation("MySql:CharSet", "utf8mb4");

    migrationBuilder.CreateTable(
        name: "wait_lists",
        columns: table => new
        {
          id = table.Column<long>(type: "bigint", nullable: false)
                .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
          user_email = table.Column<string>(type: "varchar(255)", nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4"),
          app_id = table.Column<int>(type: "int", nullable: false),
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
          table.PrimaryKey("pk_wait_lists", x => x.id);
          table.ForeignKey(
                    name: "fk_wait_lists_registered_apps_app_id",
                    column: x => x.app_id,
                    principalTable: "registered_apps",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
        })
        .Annotation("MySql:CharSet", "utf8mb4");

    migrationBuilder.CreateIndex(
        name: "ix_invite_codes_app_id",
        table: "invite_codes",
        column: "app_id");

    migrationBuilder.CreateIndex(
        name: "ix_wait_lists_app_id",
        table: "wait_lists",
        column: "app_id");

    migrationBuilder.CreateIndex(
        name: "ix_wait_lists_user_email",
        table: "wait_lists",
        column: "user_email");
  }

  /// <inheritdoc />
  protected override void Down(MigrationBuilder migrationBuilder)
  {
    migrationBuilder.DropTable(
        name: "invite_codes");

    migrationBuilder.DropTable(
        name: "wait_lists");

    migrationBuilder.DropColumn(
        name: "require_invite_code",
        table: "registered_apps");
  }
}
