using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UserIdentity.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ClearShortLivedSignInRows : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM login_tickets;");
            migrationBuilder.Sql("DELETE FROM password_resets;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
