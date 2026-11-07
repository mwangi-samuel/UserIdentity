using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UserIdentity.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ProtectCallbackHeaders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "callback_headers",
                table: "registered_apps",
                type: "longtext",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(800)",
                oldMaxLength: 800,
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "callback_headers",
                table: "registered_apps",
                type: "varchar(800)",
                maxLength: 800,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "longtext",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");
        }
    }
}
