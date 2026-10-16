using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UserIdentity.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UpdateRegisteredAppsPolyzenkit2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_registered_apps_app_name",
                table: "registered_apps");

            migrationBuilder.AlterColumn<string>(
                name: "app_secret_key",
                table: "registered_apps",
                type: "varchar(600)",
                maxLength: 600,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(100)",
                oldMaxLength: 100)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "ix_registered_apps_app_name",
                table: "registered_apps",
                column: "app_name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_registered_apps_app_name",
                table: "registered_apps");

            migrationBuilder.AlterColumn<string>(
                name: "app_secret_key",
                table: "registered_apps",
                type: "varchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(600)",
                oldMaxLength: 600)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "ix_registered_apps_app_name",
                table: "registered_apps",
                column: "app_name");
        }
    }
}
