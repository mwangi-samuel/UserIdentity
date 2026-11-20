using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UserIdentity.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CopySignInSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                INSERT INTO app_sign_in_settings (
                    id, app_id, require_confirmed_account, require_invite_code, default_role,
                    require_two_factor, require_two_factor_for_admins,
                    allow_authenticator_two_factor, allow_email_two_factor, allow_sms_two_factor,
                    created_by, created_at, updated_by, updated_at, is_deleted)
                SELECT uuidv7(), id, require_confirmed_account, require_invite_code, default_role,
                    false, false, false, false, false,
                    'system', now(), 'system', now(), false
                FROM registered_apps;
                """
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
