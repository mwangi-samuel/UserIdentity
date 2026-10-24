# UserIdentity

.NET 10 auth service on PolyzenKit 3.0.0. Uses EF Core 10 with the Microting MySQL provider (snake_case names) and Kafka (Redpanda locally).

## Layout
- `UserIdentity/Application/Core/<Feature>/{Commands,Queries,Events,ViewModels,Settings}`. Each file holds one command or query record plus its handler, which implements a PolyzenKit interface such as `ICreateItemCommandHandler<,>`, `IUpdateItemCommandHandler<,>` or `IEventHandler<>`. Handlers and repositories are registered by name scan.
- `Domain/<Feature>` holds entities and DTOs (set audit fields with `SetDtoAuditFields`).
- `Persistence/` holds `Configurations`, `Repositories` and `Migrations`.
- `Presentation/Controllers` derive from PolyzenKit's `BaseController`.
- `Infrastructure/` holds `Kafka` and `ExternalServices`. The Kafka consumer lets exceptions through so PolyzenKit retries the message.
- Settings are bound in `Extensions.cs` (`AddApp...Settings`) and wired up in `Program.cs`.
- Response-only DTOs live in the feature's `ViewModels`; `Domain` keeps entities and their projections. External API shapes (`GoogleRecaptchaResponseDto`) live in `Infrastructure`.
- `AddAppCleanups` registers PolyzenKit periodic delete commands for expired refresh tokens, expired password resets and unconfirmed users (`CleanupSettings`). The unconfirmed-user cleanup does nothing unless `RequireConfirmedAccount` is on.
- Nothing is soft-deleted, so queries don't filter on `IsDeleted`.
- Roles are shared by all apps and named `<app>:<role>`, so admin endpoints are scoped in the handlers. Controllers pass `AppName` (`IsGlobalAdmin ? null : AuthorizedParty`), and handlers use `AppRolesHelper` for role names and `IUserRegisteredAppRepository.IsUserRegisteredForAppAsync` for users, returning 404 for another app's records. Null means a global admin (`RoleSettings:GlobalAdminRole`), who isn't limited. Registered apps are global admin only.
- Role claims (scopes) follow the same app scoping: app admins manage their own app's roles' scopes. Scope strings don't name an app, so this is only safe because tokens carry only `ForApp` roles. Every scope-protected endpoint in any service must act only on the caller's app, or also require a role app admins can't give. `CreateRoleClaimCommandHandler` refuses scopes on the global admin role (it's in every app's tokens) and lowercases resource and action, since services compare scopes case-sensitively against lowercase names.
- Accounts belong to the apps in `user_registered_apps`. `GetUserQuery` with an `AppName` fails for other apps' users, which is what makes login fail. Login, register and refresh put only `AppRolesHelper.ForApp` roles in tokens.
- Invite codes and wait list entries are keyed by `(AppId, UserEmail)`. Invite and wait list controllers resolve the admin's app with `GetAdminAppAsync`.
- `PasswordResetSettings:MaxRequestsPerDay` counts the last `PasswordResetSettings.RequestCountWindow` of requests, so expired requests are only deleted that long after they expire.

## Commands
- Build: `dotnet build -clp:ErrorsOnly`
- Unit tests: `dotnet test --project UserIdentity.UnitTests/UserIdentity.UnitTests.csproj`
- Integration tests (need Docker): `dotnet test --project UserIdentity.IntegrationTests/UserIdentity.IntegrationTests.csproj`
- Add a migration: run `dotnet ef migrations add <Name> --project UserIdentity/UserIdentity.csproj --output-dir Persistence/Migrations` with the global `dotnet-ef` tool, version 10.
  - At design time the tool runs `Program.cs` up to `Build()`, so it needs a reachable MySQL (`ServerVersion.AutoDetect`), the env vars from launchSettings, and PEM content in `EDDSA_PUBLIC_KEY` and `EDDSA_PRIVATE_KEY`.
  - The `certs/` private key doesn't decrypt with the launchSettings passphrase. Generate a throwaway key pair: `openssl genpkey -algorithm ed25519 -aes-256-cbc -pass pass:X`, then `openssl pkey -pubout`.

## Tests
- xUnit v3 on Microsoft Testing Platform, with FakeItEasy.
- `TestingWebAppFactory` starts MySQL and Redpanda Testcontainers, fakes Google Recaptcha with WireMock and turns rate limiting off. It seeds a second app, `OtherApp`, for cross-app checks (`AppScopingTests`). `TestDbHelper.CreateUserEntity` also links the user to `TestApp`.
- Respawn resets the database before each test. It must ignore `__EFMigrationsHistory`, otherwise a second host re-runs every migration.
- Kafka messages from other tests also land on the topic, so filter by correlation id with `KafkaConsumerHelper.WaitAndConsumeUserUpdatedMessageAsync(correlationId)`.

## When adding or changing endpoints
- Update `UserIdentity.postman_collection.json` and `Readme.md`.
