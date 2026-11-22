using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UserIdentity.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class StepUps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "step_ups",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<string>(type: "text", nullable: false),
                    app_id = table.Column<int>(type: "integer", nullable: false),
                    ticket = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    expires = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_step_ups", x => x.id);
                    table.ForeignKey(
                        name: "fk_step_ups_asp_net_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_step_ups_registered_apps_app_id",
                        column: x => x.app_id,
                        principalTable: "registered_apps",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_step_ups_app_id",
                table: "step_ups",
                column: "app_id");

            migrationBuilder.CreateIndex(
                name: "ix_step_ups_expires",
                table: "step_ups",
                column: "expires");

            migrationBuilder.CreateIndex(
                name: "ix_step_ups_ticket",
                table: "step_ups",
                column: "ticket",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_step_ups_user_id_app_id",
                table: "step_ups",
                columns: new[] { "user_id", "app_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "step_ups");
        }
    }
}
