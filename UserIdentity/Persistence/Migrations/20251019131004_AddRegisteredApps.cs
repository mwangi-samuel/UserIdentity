using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UserIdentity.Persistence.Migrations;

/// <inheritdoc />
public partial class AddRegisteredApps : Migration
{
  /// <inheritdoc />
  protected override void Up(MigrationBuilder migrationBuilder)
  {
    migrationBuilder.AlterColumn<string>(
        name: "updated_by",
        table: "user_details",
        type: "varchar(50)",
        maxLength: 50,
        nullable: true,
        oldClrType: typeof(string),
        oldType: "longtext",
        oldNullable: true)
        .Annotation("MySql:CharSet", "utf8mb4")
        .OldAnnotation("MySql:CharSet", "utf8mb4");

    migrationBuilder.AlterColumn<string>(
        name: "created_by",
        table: "user_details",
        type: "varchar(50)",
        maxLength: 50,
        nullable: true,
        oldClrType: typeof(string),
        oldType: "longtext",
        oldNullable: true)
        .Annotation("MySql:CharSet", "utf8mb4")
        .OldAnnotation("MySql:CharSet", "utf8mb4");

    migrationBuilder.AlterColumn<string>(
        name: "updated_by",
        table: "refresh_tokens",
        type: "varchar(50)",
        maxLength: 50,
        nullable: true,
        oldClrType: typeof(string),
        oldType: "longtext",
        oldNullable: true)
        .Annotation("MySql:CharSet", "utf8mb4")
        .OldAnnotation("MySql:CharSet", "utf8mb4");

    migrationBuilder.AlterColumn<string>(
        name: "created_by",
        table: "refresh_tokens",
        type: "varchar(50)",
        maxLength: 50,
        nullable: true,
        oldClrType: typeof(string),
        oldType: "longtext",
        oldNullable: true)
        .Annotation("MySql:CharSet", "utf8mb4")
        .OldAnnotation("MySql:CharSet", "utf8mb4");

    migrationBuilder.AlterColumn<string>(
        name: "updated_by",
        table: "app_entities",
        type: "varchar(50)",
        maxLength: 50,
        nullable: true,
        oldClrType: typeof(string),
        oldType: "longtext",
        oldNullable: true)
        .Annotation("MySql:CharSet", "utf8mb4")
        .OldAnnotation("MySql:CharSet", "utf8mb4");

    migrationBuilder.AlterColumn<string>(
        name: "created_by",
        table: "app_entities",
        type: "varchar(50)",
        maxLength: 50,
        nullable: true,
        oldClrType: typeof(string),
        oldType: "longtext",
        oldNullable: true)
        .Annotation("MySql:CharSet", "utf8mb4")
        .OldAnnotation("MySql:CharSet", "utf8mb4");

    migrationBuilder.CreateTable(
        name: "registered_apps",
        columns: table => new
        {
          id = table.Column<int>(type: "int", nullable: false)
                .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
          app_name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4"),
          app_secret_key = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4"),
          base_url = table.Column<string>(type: "varchar(600)", maxLength: 600, nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4"),
          callback_url = table.Column<string>(type: "varchar(600)", maxLength: 600, nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4"),
          forward_service_token = table.Column<bool>(type: "tinyint(1)", nullable: false),
          callback_headers = table.Column<string>(type: "varchar(800)", maxLength: 800, nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4"),
          address = table.Column<string>(type: "varchar(400)", maxLength: 400, nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4"),
          primary_color = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4"),
          secondary_color = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4"),
          logo_url = table.Column<string>(type: "varchar(400)", maxLength: 400, nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4"),
          description = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4"),
          preferences_url = table.Column<string>(type: "varchar(400)", maxLength: 400, nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4"),
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
          table.PrimaryKey("pk_registered_apps", x => x.id);
        })
        .Annotation("MySql:CharSet", "utf8mb4");

    migrationBuilder.CreateTable(
        name: "user_registered_apps",
        columns: table => new
        {
          id = table.Column<string>(type: "varchar(255)", nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4"),
          app_id = table.Column<int>(type: "int", nullable: false),
          user_id = table.Column<string>(type: "varchar(255)", nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4"),
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
          table.PrimaryKey("pk_user_registered_apps", x => x.id);
          table.ForeignKey(
                    name: "fk_user_registered_apps_registered_apps_app_id",
                    column: x => x.app_id,
                    principalTable: "registered_apps",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
          table.ForeignKey(
                    name: "fk_user_registered_apps_user_details_user_id",
                    column: x => x.user_id,
                    principalTable: "user_details",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
        })
        .Annotation("MySql:CharSet", "utf8mb4");

    migrationBuilder.CreateIndex(
        name: "ix_registered_apps_app_name",
        table: "registered_apps",
        column: "app_name");

    migrationBuilder.CreateIndex(
        name: "ix_user_registered_apps_app_id",
        table: "user_registered_apps",
        column: "app_id");

    migrationBuilder.CreateIndex(
        name: "ix_user_registered_apps_user_id",
        table: "user_registered_apps",
        column: "user_id");
  }

  /// <inheritdoc />
  protected override void Down(MigrationBuilder migrationBuilder)
  {
    migrationBuilder.DropTable(
        name: "user_registered_apps");

    migrationBuilder.DropTable(
        name: "registered_apps");

    migrationBuilder.AlterColumn<string>(
        name: "updated_by",
        table: "user_details",
        type: "longtext",
        nullable: true,
        oldClrType: typeof(string),
        oldType: "varchar(50)",
        oldMaxLength: 50,
        oldNullable: true)
        .Annotation("MySql:CharSet", "utf8mb4")
        .OldAnnotation("MySql:CharSet", "utf8mb4");

    migrationBuilder.AlterColumn<string>(
        name: "created_by",
        table: "user_details",
        type: "longtext",
        nullable: true,
        oldClrType: typeof(string),
        oldType: "varchar(50)",
        oldMaxLength: 50,
        oldNullable: true)
        .Annotation("MySql:CharSet", "utf8mb4")
        .OldAnnotation("MySql:CharSet", "utf8mb4");

    migrationBuilder.AlterColumn<string>(
        name: "updated_by",
        table: "refresh_tokens",
        type: "longtext",
        nullable: true,
        oldClrType: typeof(string),
        oldType: "varchar(50)",
        oldMaxLength: 50,
        oldNullable: true)
        .Annotation("MySql:CharSet", "utf8mb4")
        .OldAnnotation("MySql:CharSet", "utf8mb4");

    migrationBuilder.AlterColumn<string>(
        name: "created_by",
        table: "refresh_tokens",
        type: "longtext",
        nullable: true,
        oldClrType: typeof(string),
        oldType: "varchar(50)",
        oldMaxLength: 50,
        oldNullable: true)
        .Annotation("MySql:CharSet", "utf8mb4")
        .OldAnnotation("MySql:CharSet", "utf8mb4");

    migrationBuilder.AlterColumn<string>(
        name: "updated_by",
        table: "app_entities",
        type: "longtext",
        nullable: true,
        oldClrType: typeof(string),
        oldType: "varchar(50)",
        oldMaxLength: 50,
        oldNullable: true)
        .Annotation("MySql:CharSet", "utf8mb4")
        .OldAnnotation("MySql:CharSet", "utf8mb4");

    migrationBuilder.AlterColumn<string>(
        name: "created_by",
        table: "app_entities",
        type: "longtext",
        nullable: true,
        oldClrType: typeof(string),
        oldType: "varchar(50)",
        oldMaxLength: 50,
        oldNullable: true)
        .Annotation("MySql:CharSet", "utf8mb4")
        .OldAnnotation("MySql:CharSet", "utf8mb4");
  }
}
