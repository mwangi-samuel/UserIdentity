# UserIdentity

.NET 10 auth service on PolyzenKit 2.5.0. Uses EF Core 10 with the Microting MySQL provider (snake_case names) and Kafka (Redpanda locally).

## Layout
- `UserIdentity/Application/Core/<Feature>/{Commands,Queries,Events,ViewModels,Settings}`. Each file holds one command or query record plus its handler, which implements a PolyzenKit interface such as `ICreateItemCommandHandler<,>`, `IUpdateItemCommandHandler<,>` or `IEventHandler<>`. Handlers and repositories are registered by name scan.
- `Domain/<Feature>` holds entities and DTOs (set audit fields with `SetDTOAuditFields`).
- `Persistence/` holds `Configurations`, `Repositories` and `Migrations`.
- `Presentation/Controllers` derive from PolyzenKit's `BaseController`.
- `Infrastructure/` holds `Kafka` and `ExternalServices`. The Kafka consumer lets exceptions through so PolyzenKit retries the message.
- Settings are bound in `Extensions.cs` (`AddApp...Settings`) and wired up in `Program.cs`.
- Response-only DTOs live in the feature's `ViewModels`; `Domain` keeps entities and their projections. External API shapes (`GoogleRecaptchaResponseDTO`) live in `Infrastructure`.
- `AddAppCleanups` registers PolyzenKit periodic delete commands for expired refresh tokens, expired password resets and unconfirmed users (`CleanupSettings`). The unconfirmed-user cleanup does nothing unless `RequireConfirmedAccount` is on.
- Nothing is soft-deleted, so queries don't filter on `IsDeleted`.

## Commands
- Build: `dotnet build -clp:ErrorsOnly`
- Unit tests: `dotnet test --project UserIdentity.UnitTests/UserIdentity.UnitTests.csproj`
- Integration tests (need Docker): `dotnet test --project UserIdentity.IntegrationTests/UserIdentity.IntegrationTests.csproj`
- Add a migration: run `dotnet ef migrations add <Name> --project UserIdentity/UserIdentity.csproj --output-dir Persistence/Migrations` with the global `dotnet-ef` tool, version 10.
  - At design time the tool runs `Program.cs` up to `Build()`, so it needs a reachable MySQL (`ServerVersion.AutoDetect`), the env vars from launchSettings, and PEM content in `EDDSA_PUBLIC_KEY` and `EDDSA_PRIVATE_KEY`.
  - The `certs/` private key doesn't decrypt with the launchSettings passphrase. Generate a throwaway key pair: `openssl genpkey -algorithm ed25519 -aes-256-cbc -pass pass:X`, then `openssl pkey -pubout`.

## Tests
- xUnit v3 on Microsoft Testing Platform, with FakeItEasy.
- `TestingWebAppFactory` starts MySQL and Redpanda Testcontainers, fakes Google Recaptcha with WireMock and turns rate limiting off.
- Respawn resets the database before each test. It must ignore `__EFMigrationsHistory`, otherwise a second host re-runs every migration.
- Kafka messages from other tests also land on the topic, so filter by correlation id with `KafkaConsumerHelper.WaitAndConsumeUserUpdatedMessageAsync(correlationId)`.

## When adding or changing endpoints
- Update `UserIdentity.postman_collection.json` and `Readme.md`.
