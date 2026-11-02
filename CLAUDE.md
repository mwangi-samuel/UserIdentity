# UserIdentity

.NET 10 auth service on PolyzenKit, EF Core 10 (Microting MySQL provider, snake_case names) and Kafka (Redpanda locally).

## Layout
- `Application/Core/<Feature>/{Commands,Queries,Events,ViewModels,Settings}`. Handlers implement PolyzenKit interfaces such as `ICreateItemCommandHandler<,>`, `IUpdateItemCommandHandler<,>` or `IEventHandler<>`. Handlers and repositories are registered by name scan.
- `Domain/<Feature>` holds entities and their DTO projections (audit fields via `SetDtoAuditFields`); response-only DTOs go in the feature's `ViewModels`, and external API shapes (`GoogleRecaptchaResponseDto`) in `Infrastructure`.
- Settings are bound in `Extensions.cs` (`AddApp...Settings`) and wired up in `Program.cs`.
- The Kafka consumer lets exceptions through so PolyzenKit retries the message.
- `AddAppCleanups` registers PolyzenKit periodic deletes for expired refresh tokens, expired password resets and unconfirmed users (`CleanupSettings`). The unconfirmed-user cleanup does nothing unless `RequireConfirmedAccount` is on. Nothing is soft-deleted, so queries don't filter on `IsDeleted`.

## Apps and roles
- Roles are shared by all apps and named `<app>:<role>`, so admin endpoints are scoped in the handlers. Controllers pass `AppName` (`IsGlobalAdmin ? null : AuthorizedParty`), and handlers use `AppRolesHelper` for role names and `IUserRegisteredAppRepository.IsUserRegisteredForAppAsync` for users, returning 404 for another app's records. Null means a global admin (`RoleSettings:GlobalAdminRole`). Registered apps are global admin only.
- App admins manage their own app's role scopes. Scope strings don't name an app, so this is safe only because tokens carry only `ForApp` roles; every scope-protected endpoint must act only on the caller's app, or also require a role app admins can't give. `CreateRoleClaimCommandHandler` refuses scopes on the global admin role (it's in every app's tokens) and lowercases resource and action, since scopes are compared case-sensitively against lowercase names.
- Accounts belong to the apps in `user_registered_apps`. `GetUserQuery` with an `AppName` fails for other apps' users, which is what makes login fail. Login, register and refresh put only `AppRolesHelper.ForApp` roles in tokens.
- Invite codes and wait list entries are keyed by `(AppId, UserEmail)`, and their controllers resolve the admin's app with `GetAdminAppAsync`. Invite lists filter by email (up to 100 characters) and applied status before counting and paging.
- An invite's optional `Role` is granted on sign-up, after the default role and in the same transaction. `CreateInviteCodeCommandHandler` accepts only an existing `ForApp` role that isn't an admin role (`AppRolesHelper.IsAppAdminRole`, which counts `<app>:admin` as well as `RoleSettings:AdminRoles`), since app admins create invites and must not hand out admin. `RegisterUserCommandHandler` checks the invite whenever `InviteCode` is sent, not only when the app requires one. Only unused invites can be revoked.
- `PasswordResetSettings:MaxRequestsPerDay` counts the last `PasswordResetSettings.RequestCountWindow` of requests, so expired requests are deleted only that long after they expire.

## Commands
- Add a migration: run `dotnet ef migrations add <Name> --project UserIdentity/UserIdentity.csproj --output-dir Persistence/Migrations`.
  - The tool runs `Program.cs` up to `Build()`, so it needs a reachable MySQL (`ServerVersion.AutoDetect`) and PEM content in `EDDSA_PUBLIC_KEY` and `EDDSA_PRIVATE_KEY`.
  - The `certs/` private key doesn't decrypt with the default passphrase. Generate a throwaway pair: `openssl genpkey -algorithm ed25519 -aes-256-cbc -pass pass:X`, then `openssl pkey -pubout`, and set `KeySetOptions__PrivateKeyPassPhrase=X`.
- When adding or changing endpoints, update `Readme.md` and the Bruno collection. Scripts save state with `bru.setVar`, not `bru.setEnvVar`.

## Tests
- Tests use FakeItEasy, not Moq.
- `TestingWebAppFactory` starts MySQL and Redpanda, fakes Google reCAPTCHA with WireMock and turns rate limiting off. It seeds a second app, `OtherApp`, for cross-app checks (`AppScopingTests`). `TestDbHelper.CreateUserEntity` also links the user to `TestApp`.
- Respawn resets the database before each test. It must ignore `__EFMigrationsHistory`, or a second host re-runs every migration.
- Other tests' Kafka messages land on the same topic, so filter by correlation id with `KafkaConsumerHelper.WaitAndConsumeUserUpdatedMessageAsync(correlationId)`.
- `InviteOpenApiContractTests` exports the Development contract from a disposable InMemory host, with no broker or database.
