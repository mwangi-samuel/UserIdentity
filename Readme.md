# Summary
UserIdentity is an easy-to-use RESTful API that simplifies user management by providing a set of endpoints for managing user identities, creating user accounts, and handling role management.

## Running the App
### Prerequisites
- Dotnet 10.0 SDK (pinned in `global.json`)
- Docker
- In production, an Ed25519 key pair for signing and checking tokens, set as `EDDSA_PRIVATE_KEY` and `EDDSA_PUBLIC_KEY` (the PEM contents) with the passphrase in `KeySetSettings__PrivateKeyPassPhrase`. Generate one outside the repo with your own passphrase:
  ```
  openssl genpkey -algorithm ED25519 -aes-256-cbc -out ed25519-private.pem -pass pass:<your passphrase>
  openssl pkey -pubout -in ed25519-private.pem -out ed25519-public.pem -passin pass:<your passphrase>
  ```
  Locally, `up.ps1` provides one (see below).

### Instructions
`docker-compose.yml` starts MySQL, NATS (with JetStream), OpenObserve and UserIdentity.

- `.\up.ps1 -UseLocal` starts MySQL, NATS and OpenObserve, waits for them to be healthy, then runs UserIdentity from source on http://localhost:5050 with the `UserIdentity` launch profile. The OpenAPI document is at http://localhost:5050/openapi/v1.json.
- `.\up.ps1 -Build` builds the image, runs everything in Docker and waits for it to be healthy. UserIdentity is on http://localhost:5050.
- `.\up.ps1` does the same without rebuilding.
- `.\down.ps1` stops the stack and preserves its named volumes for the next run.

The first run of `up.ps1` on a machine generates an Ed25519 key pair with a pinned openssl image, encrypted with the passphrase in `appsettings.json` (`TestKeyPassPhrase-For-Local-Use-Only`), and saves it in `~/.local-eddsa-keys`. Every run reads it from there into `EDDSA_PRIVATE_KEY` and `EDDSA_PUBLIC_KEY`, which `docker-compose.yml` passes to the container and `-UseLocal` to the app. Other local stacks that use the folder share the pair, so tokens issued here work there. Delete the folder to get a new pair, then log in again. Start the stack with `up.ps1`, not `docker compose up` alone, so the keys are set.

Once UserIdentity is healthy, `up.ps1` registers `admin` (password `Adm1n@P@ss`) and `globaladmin` (password `Gl0b@lAdm1n`) for the app `testapp`, unless they can already log in, and gives them `testapp:admin` and `global:admin` with `.sql/add_admin_user_role.sql`. Every local client uses the API key `TestApiKey-For-Local-Use-Only-0123456789`.

NATS monitoring is on http://localhost:8222, and NUI, a web UI for browsing streams and messages, is on http://localhost:31311 (add a connection to `nats://nats:4222` with the user below). The local NATS server has one user, `useridentity` (password `useridentity-local`), which may publish only to UserIdentity's own subjects and the JetStream API.

OpenObserve collects UserIdentity's logs, traces and metrics. Open http://localhost:5080 and sign in as `admin@example.com` with the password `OpenObserve-local1`; everything is in the `default` organisation.

- UserIdentity sends them only when it runs in Docker, where `docker-compose.yml` sets the three `OTEL_EXPORTER_OTLP_*` variables. Run from source with `-UseLocal`, it sends nothing.
- `OTEL_EXPORTER_OTLP_PROTOCOL` is `http/protobuf` because OpenObserve's port 5080 doesn't take gRPC, the .NET default. `OTEL_EXPORTER_OTLP_HEADERS` carries `Authorization=Basic <base64 of email:password>`, so it changes with `ZO_ROOT_USER_EMAIL` and `ZO_ROOT_USER_PASSWORD`.
- UserIdentity starts once OpenObserve is healthy, so its startup logs and traces arrive too.
- Every request's span and log lines carry `request_id` (the response's `X-Request-Id` header), `app_name` and `user_id`. Search Logs or Traces for `request_id = '<id>'` to follow one request.
- OpenObserve sizes its caches from the container's memory limit, and `ZO_MAX_FILE_SIZE_IN_MEMORY` (in MB) must stay under a quarter of that limit, or it refuses new data until its next timed flush. Data is kept for 14 days (`ZO_COMPACT_DATA_RETENTION_DAYS`).
- The sign-in and the open port are for local use. On a server, set your own root password and keep port 5080 private, since it takes data as well as serving the UI.

The app applies database migrations on startup.

`GET /health` checks MySQL and NATS. It answers `Healthy`, or 503 with `Unhealthy` while either can't be reached.

## Apps, Accounts and Admins
Each account belongs to the app it registered with. Logging in, refreshing a token or resetting a password through any other app fails as if the account didn't exist. A token carries only its app's roles (`<app>:*`, where `<app>` is the app's name in lower case), plus the global admin role.

There are two kinds of admin:

- **App admins** have the `<app>:admin` role. They manage their app's roles (names must start with `<app>:`, and are stored in lower case) and those roles' scopes, give those roles to their app's users, read their app's users, and manage their app's invite codes and wait list. Anything belonging to another app returns 404, or 400 for a role name outside the prefix.
- **Global admins** have `RoleSettings:GlobalAdminRole` (default `global:admin`), whatever app they log in through. They manage registered apps, and every app's roles, role scopes and users. For invite codes and wait list entries they name the app in the `X-App-Name` header. The app names `global` and `service` are reserved.

No user starts as an admin, so the first admins are created with `.sql/add_admin_user_role.sql`. It takes the user name or email and the app name, creates the role if it's missing, and is safe to run again. Add `@role_name='global:admin'` for a global admin:

```
Get-Content .sql\add_admin_user_role.sql | docker compose exec -T db mysql -uroot -proot --init-command="SET @username='<user name or email>', @app_name='testapp'"
Get-Content .sql\add_admin_user_role.sql | docker compose exec -T db mysql -uroot -proot --init-command="SET @username='<user name or email>', @role_name='global:admin'"
```

Roles are read into the token at login, so the user has to log in again to get an admin token.

### Role Scopes
A role can carry scopes, which give its users access to specific actions in other services without making them admins. A scope is `<resource>:<action>`, where a service's resources are `<service>-<entity>` and the entity is its route segment, such as `catalog-option-definition:create` for `/api/v1/option-definition` in a `catalog` service. The actions are `create`, `read`, `edit`, `delete`, `download` and `all`.

- `POST /api/v1/role/claim` with `{ "roleId": "...", "resource": "catalog-product", "action": "create" }` adds a scope. Resources and actions are stored in lower case.
- `GET /api/v1/role/claim/{roleId}` lists a role's scopes.
- `DELETE /api/v1/role/claim` with the same body as adding removes one.

App admins manage the scopes of their app's roles, and get 404 for any other role. Global admins manage any role's scopes, except the global admin role's: that role is in every app's tokens, so adding a scope to it returns 400. Tokens carry the scopes of the app's roles from the next login or refresh.

## Invite Codes
An invite code lets one email address sign up to an app, and can give the new account a role. App admins manage their app's invite codes, and global admins name the app in the `X-App-Name` header.

- `POST /api/v1/invite-code` with `{ "inviteCode": "...", "userEmail": "...", "role": "<app>:owner" }` creates one. The code has 10 to 255 characters and the email must be a valid address. `role` is optional. When set, it must be an existing role of the app that isn't an admin role (`<app>:admin` or any of `RoleSettings:AdminRoles`), or the request returns 400. There's one invite code per app and email.
- `GET /api/v1/invite-code?page=1&pageSize=50&search=owner&applied=false` lists the app's invite codes, newest first, with `totalCount`. Optional `search` (email, up to 100 characters) and nullable `applied` filters are combined before counting/paging; omission retains the full list.
- `GET /api/v1/invite-code/{inviteCodeId}` and `GET /api/v1/invite-code/email/{email}` read one.
- `DELETE /api/v1/invite-code/{inviteCodeId}` revokes an unused invite code. A used one returns 400, and another app's returns 404.

Signing up with `inviteCode` checks it whether or not the app has `RequireInviteCode` on: the email is required, an invite code must exist for the app and email, the code must match (ignoring case), and it must be unused. Otherwise the request returns 400, with the same message whether the invite is missing, wrong or used, so sign-up never says whether an email has an invite. A valid code is marked used, and its role is given to the new account with the default role, so it's in the account's tokens with its scopes. If that role no longer exists, sign-up returns 400 and nothing is saved. Without `inviteCode`, sign-up only needs one when the app requires it.

## Wait List
`POST /api/v1/wait-list` with the `X-App-Name` header and `{ "userEmail": "..." }` adds an email to the app's wait list. Anyone can call it. The email must be a valid address of up to 256 characters, and any other field in the body is ignored. App admins read their app's entries with `GET /api/v1/wait-list/{waitListId}` and `GET /api/v1/wait-list/email/{email}`, and global admins name the app in the `X-App-Name` header.

## App Callbacks
When a service registers a user through `register-for-service` and the app has a `CallbackUrl`, UserIdentity POSTs the user details to it. Registrations through the app's UI (`register`) get no callback. The callback goes through the outbox: sign-up stores a message with the new account and sends it to NATS JetStream, and UserIdentity receives it and calls the app. A failed callback is retried after each of `MessagingSettings:RetryDelaysSeconds` until it succeeds or `MessagingSettings:MaxDeliver` runs out. Delivery is at least once, so an app may get the same callback twice and should ignore a repeated `userIdentityId`.

Each callback is signed with the app's `AppSecretKey`:

- `X-Timestamp`: Unix time in seconds.
- `X-Signature`: `sha256=` followed by the hex HMAC-SHA256 of `{timestamp}.{body}`.

Apps should recompute the signature over the raw request body, compare it in constant time and reject stale timestamps. .NET apps can use `HmacSignature.IsValid(...)` from PolyzenKit.

## Sessions
- Login and registration return a short-lived access token and a refresh token. A session lasts `RefreshTokenSettings:ValidForSeconds` (default 30 days) and each refresh extends it. Only hashes of refresh tokens are stored.
- When the account isn't confirmed yet, registration from the app (not `register-for-service`) and login return a `loginTicket` instead, so the app can sign the user in once they confirm without asking for the password again. `POST /api/v1/user/login-ticket` with the `X-App-Name` header and `{ "ticket": "..." }` returns the same body as login. It answers 406 while the account is still unconfirmed, so the app can try again a moment later (the confirmation arrives over NATS JetStream shortly after the code is verified), and 401 for an unknown, used or expired ticket, or one from another app. A ticket works once and expires after `LoginTicketSettings:ValidForSeconds` (default 1 hour). Only its hash is stored.
- Each refresh returns a new refresh token, and the one sent can't be used again. If a replaced refresh token is sent again, UserIdentity treats it as copied and ends that session. Clients must keep the newest refresh token, and must not send the same refresh token twice at once (for example, from two tabs): the second of two simultaneous refreshes gets a 409.
- `POST /api/v1/user/logout` with the access token and `{ "refreshToken": "..." }` ends that session. The access token keeps working until it expires.
- Five wrong passwords in a row lock an account for five minutes. A locked account gets the same response as a wrong password.

## Password Reset
Users reset their password with a code, the same way they confirm their account:

1. `POST /api/v1/user/reset-password` with the `X-App-Name` header and `{ "userName": "<user name or email>" }`. The response is always 200 with a `resetRequestId`, whether or not the account exists. For an existing user, a `PasswordReset` message with the reset request id as its correlation id is stored with the request and sent to NATS JetStream. Another request for the same user within `PasswordResetSettings:RequestCooldownSeconds`, or after `PasswordResetSettings:MaxRequestsPerDay` requests (default 5) in the last 24 hours, sends nothing. Each request gets its own code and attempts, so the daily cap limits how many codes can be guessed.
2. Once a message saying the code for that correlation id was verified is received from NATS JetStream, the reset request is marked verified.
3. `POST /api/v1/user/update-password` with `{ "resetRequestId": "...", "newPassword": "..." }`. It returns 406 if the request is unknown, not verified yet, expired (`PasswordResetSettings:ValidForSeconds`, default 15 minutes) or already used, and 400 if the new password breaks the password rules. A successful reset ends all of the user's sessions and lifts any lockout.

## Rate Limiting
Register, login, reset-password, update-password and the waitlist sign-up allow `RateLimitSettings:PermitLimit` requests per `RateLimitSettings:WindowSeconds` (default 10 per minute) for each client IP and endpoint. Requests over the limit get a 429 with a `Retry-After` header.

Behind a reverse proxy, set `ForwardedHeadersSettings__KnownNetworks` to the proxy's network (comma-separated CIDR ranges). Otherwise every request appears to come from the proxy, and all clients share one limit.

Password hashing is limited across all clients too, because a hash takes most of a CPU for about 100 ms and would otherwise starve every other request. Login, register, `register-for-service` and update-password run at most `PasswordHashingSettings:MaxConcurrent` at a time (default 1), with up to `PasswordHashingSettings:QueueLimit` more waiting (default 32). Requests beyond that get a 429.

Request bodies are limited to 64 KiB. Larger ones get a 413.

## Cleanup
Every `CleanupSettings:IntervalInMinutes` (default 60), expired refresh tokens and login tickets are deleted, and so are password reset requests a day after they expire (kept that long for the daily cap). When confirmed accounts are required, accounts still unconfirmed after `CleanupSettings:UnconfirmedUserLifetimeInDays` (default 30) are deleted too, along with their details, sessions, login tickets, reset requests, app links and roles, and the invite code they used for their app can be used again.

## Settings
Every setting has a default in `appsettings.json` that works locally. In production, set at least these:

| Setting | Meaning |
|---|---|
| `EDDSA_PRIVATE_KEY`, `EDDSA_PUBLIC_KEY`, `KeySetSettings__PrivateKeyPassPhrase` | The Ed25519 key pair (PEM content) and the private key's passphrase |
| `AppSecretSettings__Keys__<id>`, `AppSecretSettings__CurrentKeyId` | Base64 256-bit keys that encrypt registered apps' secrets at rest. Generate one with `openssl rand -base64 32` and keep it in a secret store: secrets encrypted with a lost key can't be recovered |
| `ApiKeySettings__ApiKey` | The key clients send in `X-Api-Key`, at least 32 characters |
| `ExternalServicesSettings__ExternalServices__AppCallback__DefaultHeaders__X-Api-Key` | The `X-Api-Key` header sent with app callbacks |
| `GoogleRecaptchaSettings__Enabled`, `GoogleRecaptchaSettings__SiteKey` | Turns on the reCAPTCHA check at sign-up, which is off by default |
| `RegisteredAppsSettings__RegisteredApps__<n>__*` | The apps to seed. Each `AppSecretKey` needs 32 to 100 characters |
| `DatabaseSettings__*` | Database connection |
| `MessagingSettings__NatsUrl` | The NATS server, without credentials |
| `MessagingSettings__NatsUser`, `MessagingSettings__NatsPassword` | UserIdentity's own NATS user. Keep the password in a secret store |
| `ForwardedHeadersSettings__KnownNetworks` | The reverse proxy's networks (see [Rate Limiting](#rate-limiting)) |

Sign-up verifies the reCAPTCHA token before it takes a place in the password hashing limit, so a slow answer (`ExternalServicesSettings:ExternalServices:GoogleRecaptcha:TimeoutSeconds`, default 5) holds up only that sign-up.

`Logging:LogLevel:Default` (`Warning`) sets the log level, and `Logging:LogLevel:Microsoft.EntityFrameworkCore` sets EF Core's (`Error`). From the environment, set the level with `Logging__LogLevel__Default`. Logs go to the console as plain text in Development and as one JSON object per line everywhere else. Setting `OTEL_EXPORTER_OTLP_ENDPOINT` also sends logs, traces and metrics there over OTLP, with OpenTelemetry's other `OTEL_EXPORTER_OTLP_*` settings.

Settings are checked at startup. An invalid setting stops the app with every failure listed, such as `ValidForSeconds: The field ValidForSeconds must be between 1 and 2147483647.`

## API Requests
`UserIdentity/UserIdentity.http` holds requests for sending by hand, in an order that works from top to bottom: health, the OpenAPI document and the key set, logging in the admins, a user, roles with their scopes and users, invite codes, the wait list, the entities that scopes can name, registered apps, a password reset, a refresh and a logout. It leaves out `register-for-service`, which needs a service's token. Open it in Visual Studio 2022 (17.8 or later), or in VS Code with the [REST Client](https://marketplace.visualstudio.com/items?itemName=humao.rest-client) extension, and select **Send Request** above a request. The tests are the automated checks.

- No environment needs selecting: the settings (URL, API key, app name and the admins' details) are variables at the top of the file.
- Requests that use an earlier response, such as an admin's token or the new role's id, need that request sent first.
- New users, roles, invites, wait list entries and registered apps are named after the current time, so the requests can be sent again.
- Access tokens last 3 minutes (`JwtIssuerSettings:ValidForSeconds`). When a request answers 401, send the admin's login again.
- The stack doesn't deliver confirmation codes, so it runs with `IdentityUserSettings:RequireConfirmedAccount` off (the default in `appsettings.json`, which `docker-compose.yml` also sets), and new accounts can log in at once. `up.ps1` registers both admins and gives them their roles, so start with their logins.
- The stack doesn't verify reset codes either, so `update-password` answers 406. To see it succeed, mark the reset requests verified after sending `reset-password`:
  ```
  docker compose exec -T db mysql -uroot -proot useridentity -e "UPDATE password_resets SET verified = 1"
  ```

## Running the Tests
The tests use xUnit v3 on Microsoft Testing Platform (selected in `global.json`). The unit tests call each endpoint method directly with faked handlers. The integration tests send requests to the whole app and start MySQL and NATS with Testcontainers, so Docker must be running. The smoke tests build the Docker image, run it as it's published (trimmed, single file) beside MySQL and NATS, and reach it only over HTTP and NATS.

```
dotnet test --project UserIdentity.UnitTests/UserIdentity.UnitTests.csproj
dotnet test --project UserIdentity.IntegrationTests/UserIdentity.IntegrationTests.csproj
dotnet test --project UserIdentity.SmokeTests/UserIdentity.SmokeTests.csproj
```

Add `--coverlet` for a Cobertura coverage report. Set `USERIDENTITY_SMOKE_IMAGE` to run the smoke tests against an image that's already built.

## Adding Migrations
Migrations need the global `dotnet-ef` tool, version 10 (`dotnet tool install --global dotnet-ef`, or `update` if an older one is installed). MySQL must be running, since the server version is auto-detected. The tool builds the database context from `DatabaseSettings` in `appsettings.json`, which `DatabaseSettings__*` environment variables override, so it doesn't need the Ed25519 keys.

```
dotnet ef migrations add <MigrationName> --project UserIdentity
```

## Persistent storage

Normal `./down.ps1; ./up.ps1` and rebuilds reuse project-scoped named volumes. MySQL mounts `mysql_data` at `/var/lib/mysql`; object storage and the broker also retain their data when present. The scripts resolve Compose relative to their own directory. Deliberate reset uses `docker compose down --volumes` and deletes the stack's stored data; it is separate from normal shutdown.

