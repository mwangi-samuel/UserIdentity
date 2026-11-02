using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UserIdentity.Persistence.Migrations;

/// <inheritdoc />
public partial class AddOutboxMessages : Migration
{
  /// <inheritdoc />
  protected override void Up(MigrationBuilder migrationBuilder)
  {
    migrationBuilder.CreateTable(
        name: "outbox_messages",
        columns: table => new
        {
          id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
          subject = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4"),
          payload = table.Column<string>(type: "longtext", nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4")
        },
        constraints: table =>
        {
          table.PrimaryKey("pk_outbox_messages", x => x.id);
        })
        .Annotation("MySql:CharSet", "utf8mb4");
  }

  /// <inheritdoc />
  protected override void Down(MigrationBuilder migrationBuilder)
  {
    migrationBuilder.DropTable(
        name: "outbox_messages");
  }
}
