# Summary
UserIdentity is an easy-to-use RESTful API that simplifies user management by providing a set of endpoints for managing user identities, creating user accounts, and handling role management.

## Running the App
### Prerequisites
- Dotnet 10.0 SDK (pinned in `global.json`)
- Docker
- An Ed25519 key pair for signing and checking tokens. Generate one outside the repo (replace the passphrase with your own):
  ```
  openssl genpkey -algorithm ED25519 -aes-256-cbc -out ed25519-private.pem -pass pass:S3T@G00dPa22Phr@S3
  openssl pkey -pubout -in ed25519-private.pem -out ed25519-public.pem -passin pass:S3T@G00dPa22Phr@S3
  ```
  Then set the PEM contents, and the passphrase if you changed it, in the shell that runs `up.ps1`:
  ```
  $env:EDDSA_PRIVATE_KEY = Get-Content -Raw .\ed25519-private.pem
  $env:EDDSA_PUBLIC_KEY = Get-Content -Raw .\ed25519-public.pem
  $env:EDDSA_PRIVATE_KEY_PASSPHRASE = '<your passphrase>'
  ```

### Instructions
`docker-compose.yml` starts MySQL, Redpanda (Kafka), Redpanda Console and UserIdentity.

- `.\up.ps1 -UseLocal` starts MySQL and Redpanda, waits for them to be healthy, then runs UserIdentity from source on http://localhost:5050 with the `UserIdentity` launch profile. The OpenAPI document is at http://localhost:5050/openapi/v1.json.
- `.\up.ps1 -Build` builds the image and runs everything in Docker. UserIdentity is on http://localhost:5050.
- `.\up.ps1` runs everything in Docker without rebuilding.
- `.\down.ps1` stops the stack and removes its volumes, so the next run starts with an empty database.

Kafka topics can be inspected in Redpanda Console at http://localhost:8090.

The app applies database migrations on startup.

## Apps, Accounts and Admins
Each account belongs to the app it registered with. Logging in, refreshing a token or resetting a password through any other app fails as if the account didn't exist. A token carries only its app's roles (`<app>:*`, where `<app>` is the app's name in lower case), plus the global admin role.

There are two kinds of admin:

- **App admins** have the `<app>:admin` role. They manage their app's roles (names must start with `<app>:`) and those roles' scopes, give those roles to their app's users, read their app's users, and manage their app's invite codes and wait list. Anything belonging to another app returns 404, or 400 for a role name outside the prefix.
- **Global admins** have `RoleSettings:GlobalAdminRole` (default `global:admin`), whatever app they log in through. They manage registered apps, and every app's roles, role scopes and users. For invite codes and wait list entries they name the app in the `X-App-Name` header. The app names `global` and `service` are reserved.

No user starts as an admin, so the first admins are created with `.sql/add_admin_user_role.sql`. It takes the user name or email and the app name, creates the role if it's missing, and is safe to run again. Add `@role_name='global:admin'` for a global admin:

```
Get-Content .sql\add_admin_user_role.sql | docker compose exec -T db mysql -uroot -proot --init-command="SET @username='<user name or email>', @app_name='testapp'"
Get-Content .sql\add_admin_user_role.sql | docker compose exec -T db mysql -uroot -proot --init-command="SET @username='<user name or email>', @role_name='global:admin'"
```

Roles are read into the token at login, so the user has to log in again to get an admin token.

### Role Scopes
A role can carry scopes, which give its users access to specific actions in other services without making them admins. A scope is `<resource>:<action>`, where a service's resources are `<service>-<controller>`, such as `catalog-product:create` for a `catalog` service. The actions are `create`, `read`, `edit`, `delete`, `download` and `all`.

- `POST /api/v1/role/claim` with `{ "roleId": "...", "resource": "catalog-product", "action": "create" }` adds a scope. Resources and actions are stored in lower case.
- `GET /api/v1/role/claim/{roleId}` lists a role's scopes.
- `DELETE /api/v1/role/claim` with the same body as adding removes one.

App admins manage the scopes of their app's roles, and get 404 for any other role. Global admins manage any role's scopes, except the global admin role's: that role is in every app's tokens, so adding a scope to it returns 400. Tokens carry the scopes of the app's roles from the next login or refresh.

## App Callbacks
When a user registers through an app's UI and the app has a `CallbackUrl`, UserIdentity POSTs the user details to it. Each callback is signed with the app's `AppSecretKey`:

- `X-Timestamp`: Unix time in seconds.
- `X-Signature`: `sha256=` followed by the hex HMAC-SHA256 of `{timestamp}.{body}`.

Apps should recompute the signature over the raw request body, compare it in constant time and reject stale timestamps. .NET apps can use `HmacSignatureUtil.IsValidSignature` from PolyzenKit.

## Sessions
- Login and registration return a short-lived access token and a refresh token. A session lasts `RefreshTokenSettings:ValidForSeconds` (default 30 days) and each refresh extends it. Only hashes of refresh tokens are stored.
- Each refresh returns a new refresh token, and the one sent can't be used again. If a replaced refresh token is sent again, UserIdentity treats it as copied and ends that session. Clients must keep the newest refresh token, and must not send the same refresh token twice at once (for example, from two tabs).
- `POST /api/v1/user/logout` with the access token and `{ "refreshToken": "..." }` ends that session. The access token keeps working until it expires.
- Five wrong passwords in a row lock an account for five minutes. A locked account gets the same response as a wrong password.

## Password Reset
Users reset their password with a code, the same way they confirm their account:

1. `POST /api/v1/user/reset-password` with the `X-App-Name` header and `{ "userName": "<user name or email>" }`. The response is always 200 with a `resetRequestId`, whether or not the account exists. For an existing user, a `PASSWORD_RESET` message is produced to Kafka with the reset request id as its correlation id. Another request for the same user within `PasswordResetSettings:RequestCooldownSeconds`, or after `PasswordResetSettings:MaxRequestsPerDay` requests (default 5) in the last 24 hours, produces nothing. Each request gets its own code and attempts, so the daily cap limits how many codes can be guessed.
2. Once a message saying the code for that correlation id was verified is consumed from Kafka, the reset request is marked verified.
3. `POST /api/v1/user/update-password` with `{ "resetRequestId": "...", "newPassword": "..." }`. It returns 406 if the request is unknown, not verified yet, expired (`PasswordResetSettings:ValidForSeconds`, default 15 minutes) or already used, and 400 if the new password breaks the password rules. A successful reset ends all of the user's sessions and lifts any lockout.

## Rate Limiting
Register, login, reset-password, update-password and the waitlist sign-up allow `RateLimitSettings:PermitLimit` requests per `RateLimitSettings:WindowSeconds` (default 10 per minute) for each client IP and endpoint. Requests over the limit get a 429 with a `Retry-After` header.

Behind a reverse proxy, set `ForwardedHeadersSettings__KnownNetworks` to the proxy's network (comma-separated CIDR ranges). Otherwise every request appears to come from the proxy, and all clients share one limit.

## Cleanup
Every `CleanupSettings:IntervalInMinutes` (default 60), expired refresh tokens are deleted, and so are password reset requests a day after they expire (kept that long for the daily cap). When confirmed accounts are required, accounts still unconfirmed after `CleanupSettings:UnconfirmedUserLifetimeInDays` (default 30) are deleted too, along with their sessions, app links and roles, and their invite code can be used again.

## Settings
Every setting has a default in `appsettings.json` that works locally. In production, set at least these:

| Setting | Meaning |
|---|---|
| `EDDSA_PRIVATE_KEY`, `EDDSA_PUBLIC_KEY`, `KeySetOptions__PrivateKeyPassPhrase` | The Ed25519 key pair (PEM content) and the private key's passphrase |
| `AppSecretOptions__Keys__<id>`, `AppSecretOptions__CurrentKeyId` | Base64 256-bit keys that encrypt registered apps' secrets at rest. Generate one with `openssl rand -base64 32` and keep it in a secret store: secrets encrypted with a lost key can't be recovered |
| `ApiKeySettings__ApiKey` | The key clients send in `X-Api-Key` |
| `MysqlSettings__*` | Database connection |
| `KafkaSettings__BootstrapServers`, `KafkaSettings__SchemaRegistryUrl` | Kafka connection |
| `CorsSettings__AllowedOrigins` | Comma-separated origins allowed to call the API |
| `ForwardedHeadersSettings__KnownNetworks` | The reverse proxy's networks (see [Rate Limiting](#rate-limiting)) |

## API Collections
Import `postman/UserIdentity.postman_collection.json` and `postman/UserIdentity.postman_environment.json` into Postman.

## Running the Tests
The tests use xUnit v3 on Microsoft Testing Platform (selected in `global.json`). The integration tests start MySQL and Redpanda with Testcontainers, so Docker must be running.

```
dotnet test --project UserIdentity.UnitTests/UserIdentity.UnitTests.csproj
dotnet test --project UserIdentity.IntegrationTests/UserIdentity.IntegrationTests.csproj
```

Add `--coverlet` for a Cobertura coverage report.

## Adding Migrations
Migrations need the global `dotnet-ef` tool, version 10 (`dotnet tool install --global dotnet-ef`, or `update` if an older one is installed). MySQL must be running, since the server version is auto-detected. The app's startup also loads the Ed25519 keys, so set `EDDSA_PRIVATE_KEY` and `EDDSA_PUBLIC_KEY` as for running the app.

```
dotnet ef migrations add <MigrationName> --project UserIdentity
```
