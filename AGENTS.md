# Service rules

These rules apply to every agent working on this service: Claude, Codex, Cursor, Copilot or anyone else. The service is an ASP.NET Core REST API on .NET 10, built on the PolyzenKit NuGet package (written for PolyzenKit 14.x), with PostgreSQL as its database. PolyzenKit's `docs/Guide.md` is the reference for its API and settings.

The repo's `CLAUDE.md` loads this file and adds what's specific to this service. If the two disagree, follow this file and point out the conflict.

## How to work

### Git
- Work on `dev`. Never commit, push or tag unless asked. Don't create feature branches or PRs unless asked.
- Commit messages: an imperative subject, a blank line, then `- ` bullets wrapped at about 72 characters.

### Scope
- Change only what the task asks for.
- Don't create plan, handoff, verification or summary files. Report in your reply instead.
- Don't edit `AGENTS.md`, `CLAUDE.md` or the Readme unless the task is a docs task.

### Secrets and search
- Never read or print secrets: `.env` files, private keys (`*.pem`), kubeconfigs, or anything the repo's `CLAUDE.md` lists.
- Skip `bin/`, `obj/`, `TestResults/`, `node_modules/` and generated API clients when searching.

### Checks
- Before reporting, run the checks for what you changed (see [Checks](#checks)).
- Report failures with their output.
- Name any check you didn't run and say why, for example that Docker isn't running. Never describe an unrun check as passing.

### Cleanup
- Leave no empty folders and no files that only re-export another file.

## Design

### Compatibility
The repo's `CLAUDE.md` says whether the service is deployed.
- **Not deployed yet:** keep no compatibility code for old shapes, names or clients. No legacy fallbacks, aliases, re-exports, "was this field sent" flags or data backfills. When you rename or remove something, update every caller.
- **Deployed:** a change to stored data ships with a migration that keeps existing rows correct, and a breaking API change is agreed before it's made.

### Prefer what already exists
- Follow the existing code and the established patterns for the domain.
- Reuse PolyzenKit before adding a helper. Anything another service could use belongs in PolyzenKit, which must be released to NuGet before a service can depend on the change. Never copy PolyzenKit code into a service to change it.
- Add an abstraction only for a need that exists now, such as a second caller or a real invariant. Deleting code beats adding code.
- Each rule lives in one place. If the same thing is checked twice, remove one of the checks.

### Persistence
- Look at the relationships, queries and lifecycle before choosing a design.
- Records that are managed independently or referenced elsewhere get their own entity, foreign keys and unique constraints.
- Use JSON columns only for value snapshots that nothing else references, and note the trade-off in the Readme. Map a list column with PolyzenKit's `HasJsonList()`.
- Avoiding a migration is not a design goal.

### Deletion
- Delete rows that are removed.
- Soft-delete (`IsDeleted`) only when something still reads the row, such as an order line that points at a product.

### What each role sees
- Roles rank from the lowest (for example a signed-in user) up to app admin, then global admin.
- A lower role never learns about a higher one. Text, errors and responses shown to a lower role don't name a higher role, say what it can do, or say that it acted.
- When a lower role needs to know about a decision made above it, attribute it to the organisation the repo's `CLAUDE.md` names ("The marketplace has hidden this product").
- A role may see its own name and anything about lower roles.

## Comments
- Comment only when someone would otherwise change the code and break it: a non-obvious setting, a magic number, a required order, an external constraint.
- Don't comment what the code does, what the next line, the name or the type already says, normal behaviour, library defaults or the phases of a method. Explanations belong in the Readme.
- Before finishing, remove any comment you added that doesn't meet this bar.

## Naming

### .NET acronyms and abbreviations
- Acronyms of three or more letters are Pascal case: `Dto`, `Jwt`, `Jwks`, `Smtp`, `EdDsa`.
- Two-letter acronyms stay upper case: `IO`.
- Abbreviations are Pascal case: `Id`, `Db`.
- Constants and enum members are Pascal case like other members: `MaxPageSize`, `ProductStatus.Active`.
- These keep their own spelling: protocol strings (`"EdDSA"`), environment variables (`EDDSA_PUBLIC_KEY`) and other libraries' names (`ServiceURL`).
- Write "DTO" in prose.

### Type names
PolyzenKit registers classes by name, so these suffixes are required, not style:

| Kind | Name | Registered by |
|---|---|---|
| Repository | `ProductRepository` implementing `IProductRepository` | `AddAppRepositories` |
| Command handler | `CreateProductCommandHandler` | `AddAppCommandAndQueryHandlers` |
| Query handler | `GetProductsQueryHandler` | `AddAppCommandAndQueryHandlers` |
| Event handler | `OrderPaidEventHandler` | `AddAppEventHandlers` |
| Message handler | `OrderPaidMessageHandler` with `[MessageSubject("payments.order.paid")]` | `AddAppMessaging` |

The rest follow one pattern per feature:
- `ProductEntity`, `ProductDto`, `ProductConfiguration`.
- `CreateProductCommand`, `UpdateProductCommand`, `DeleteProductCommand`, and `ProductInput` for the body they share.
- `GetProductQuery`, `GetProductsQuery`, `ProductViewModel`, `ProductsViewModel`.
- `ProductEndpoints` with `MapProductEndpoints`, whose group `MapApiGroup("Product")` is served at `/api/v1/product`. A two-word group is kebab-case in its route and scopes: `MapApiGroup("OptionDefinition")` is `/api/v1/option-definition` and `<service>-option-definition:read`.
- Settings classes end in `Settings` and bind the section of the same name.

### Classes, files and folders
- Name a class or file for what it does (`ProductEditor`, `SkuReserver`), not for a category (`Rules`, `Helpers`, `Utils`, `Manager`, `Preparation`).
- Don't create folders for kinds of class either (`Rules/`, `Values/`, `Inputs/`, `Validators/`). A feature's `Shared/` folder says who uses its classes, not what kind they are, so it isn't one of these.
- A class that does something is named for who does it: `...CommandHandler`, `...Creator`, `...Verifier`, `...Sender`, `...Provider`, `...Reader`, `...Mapper`. A repository is the exception. A class that only holds values is a noun (`NewAccount`, `AccountCreationResult`). A method is a verb.
- An interface is named the same way and does one thing (`ICodeVerifier`, `ICodeResender`).
- One type per file, named after it. The exception is a command, query or event, which shares its handler's file.
- `partial` is only for the classes whose bodies a source generator writes, `LogMessages` (`[LoggerMessage]`) and `ValidationPatterns` (`[GeneratedRegex]`), and for EF's migrations. Never split a class over files.
- `Application/Interfaces` holds interfaces only. What they take and answer is in the feature's `Shared`. `Infrastructure/ExternalServices` holds the calls to other services, `Infrastructure/Security` keys, signing and passwords.
- Texts of logs are in `Common/Logging/LogMessages`, patterns in `Common/Validation/ValidationPatterns`, a feature's messages in its `...Messages`.
- An entry of `ExternalServicesSettings` is named for the endpoint it calls, and its address goes up to that endpoint. A class of the same name holds the entry's name and, where the address ends in `/`, the last words. No class holds an address or any other path.
- A start-up step is `AddAppX`. What applies to a client as a whole (signing, connections) is wired there, not in the class that calls.
- Follow a pattern of another service only if it is the better one.

## Formatting
CSharpier owns the layout of C# and project files. `.editorconfig` holds its indent and width, plus every code style, naming and analyser rule. `.csharpierignore` lists the generated files it skips.

**Files**
- 4-space indentation in C#, 2 in XML and project files, a final newline, no trailing whitespace.
- Don't set `end_of_line`: line endings differ by platform, and forcing one fails CSharpier on Linux.
- Keep an existing UTF-8 BOM.
- Git Bash `sed -i` silently turns CRLF into LF, so edit C# files with an editor tool. Check line endings with `git ls-files --eol`.
- Migrations, their designer files and the model snapshot are generated. Never edit them by hand.

**Namespaces and usings**
- File-scoped namespaces that match the folder.
- `using` directives go outside the namespace, `System` first, with no blank lines between groups and no unused usings.

**Style**
- Allman braces: the opening brace goes on its own line.
- Braces are optional for single-statement branches, and expression-bodied members are fine.
- A method whose body fits on one line uses an expression body (`=>`). The build enforces this with IDE0022 set to `when_on_single_line` in `.editorconfig`, and `dotnet format style` converts it.
- Use `var` everywhere.
- Use primary constructors, with members initialised from the parameters, through a static method when that takes several steps. Write a constructor body only when it must run statements.
- Prefer collection expressions (`[]`), target-typed `new()`, pattern matching and switch expressions where they fit.
- Give every non-interface member explicit accessibility, and make fields `readonly`.
- Add parentheses for clarity in arithmetic and relational expressions.

**Blank lines**
- At most one blank line in a row.
- Leave a blank line after every block.
- Never put two statements on one line.
- Separate a method's lookup, validation, mapping, persistence and return steps with blank lines, keeping related assignments together.

**Wrapping**
- CSharpier wraps at 140 columns. Don't wrap, align or unwrap code by hand, and don't restyle its output.
- A string literal stays whole, even past 140 columns, so messages can still be found by searching.

**Reformatting**
- A Debug build formats every project it builds, and a Release build fails on unformatted code. To format without building, run `dotnet tool restore` once, then `dotnet csharpier format <paths>`.
- Before committing, run `./format.ps1` at the repo root. It runs the style fixes, the analyser fixes and CSharpier, then a Release build, and stops at the first step that fails.
- Use `dotnet format style --severity info` and `dotnet format analyzers` for code style fixes. Without `--severity info`, the style fixes skip the `.editorconfig` preferences left at their default severity, such as `new()` (IDE0090), which Code Cleanup applies. Never run plain `dotnet format` or `dotnet format whitespace`, or keep "Format document" in a Visual Studio Code Cleanup profile: they undo CSharpier.
- For format on save in Visual Studio, install the CSharpier extension and turn on "Reformat with CSharpier on Save".
- `.config/dotnet-tools.json` and the `CSharpier.MsBuild` package pin the same version. Change them together.

## Layers and folders

```
<Service>.slnx
Directory.Build.props
Directory.Packages.props
<Service>/
  Application/
    Core/<Feature>/{Commands,Queries,Events,ViewModels,Shared}
    Interfaces/
  Common/{Enums,Errors,Logging,Validation}
  Domain/<Table>/
  Infrastructure/
  Persistence/{AppDbContext.cs,Configurations,Migrations,Repositories/<Feature>}
  Presentation/Endpoints/
  Extensions.cs
  Program.cs
<Service>.UnitTests/
<Service>.IntegrationTests/
```

**Dependencies**
- `Presentation` and `Infrastructure` use `Application` and `Domain`. `Application` uses `Persistence` and `Domain`. `Persistence` uses `Domain`. `Domain` uses only `Common`. `Common` uses nothing in the service.
- `Application` never touches EF Core or `AppDbContext`. The one exception is `DbUpdateConcurrencyException`, which a handler may throw for a stale `Version`.
- The unit tests include a layer test that fails on these imports. Keep it passing; don't loosen it.

**`Common`**
- Small types with no dependencies that several layers share and no layer owns: `Enums`, `Errors/ErrorMessages`, `Logging/LogMessages`, shared constants, validation attributes and `Validation/ValidationPatterns`.
- No services or business rules.

**`Infrastructure`**
- Implementations of `Application/Interfaces` (storage, email, other services' HTTP APIs) and message handlers.

## Domain
- Each table's folder (`Domain/Products/`) holds its entity, its DTO, its limits and the value records stored in that table's JSON columns.
- Entities derive from PolyzenKit's `BaseAuditableEntity<TKey>`, which adds `CreatedBy`, `CreatedAt`, `UpdatedBy`, `UpdatedAt` and `IsDeleted`, or `BaseEntity<TKey>` for rows that need no audit, such as join rows.
- Entities hold data. No methods that enforce rules or throw; read-only computed values are fine.
- A table's limits go in `<Table>Limits`, a static class of constants next to the entity, even when only one place reads them: `ProductLimits.NameMaxLength`. Entity attributes and validation attributes read them from there. Entities and DTOs hold no constants. A table owned by a library, such as ASP.NET Identity's, gets a limits class too, applied in its configuration.
- Entities carry every column rule that has a `System.ComponentModel.DataAnnotations` attribute: `[MaxLength]`, `[ConcurrencyCheck]`, `[ForeignKey]`. Configurations hold the rest: keys, indexes, relationships without a navigation property, conversions, and the columns of a library's tables, whose classes can't carry attributes. Name members `<Field>MaxLength`, `<Field>MinLength` or `<Field>MaxBytes`.
- A DTO derives from its entity and implements `IBaseAuditableEntityDto<TKey>`. It has `Basic` and `Detailed` projections as `Expression<Func<TEntity, TDto>>`, plus `FromEntityBasic` and `FromEntityDetailed` that call the compiled expression. Repositories `.Select(XDto.Basic)`; handlers return `XDto.FromEntityDetailed(entity)` after a write. Add a DTO only when something reads it.

## Persistence

**Context**
- `AppDbContext` is registered as `DbContext` (`AddDbContext<DbContext, AppDbContext>`), since PolyzenKit's repositories and unit of work resolve `DbContext`.
- It includes `DbSet<AppEntity>` and applies `AppEntityConfiguration`. Each entity has an `IEntityTypeConfiguration` in `Persistence/Configurations`.
- Table names (`ToTable`) are set only in `AppDbContext`'s list, never in the entity configurations. Columns are snake_case through `UseSnakeCaseNamingConvention()`.
- Money is `decimal(18,2)`, set once in `ConfigureConventions`. Every amount is validated with the service's amount attribute.
- The database is PostgreSQL, through `Npgsql.EntityFrameworkCore.PostgreSQL`. `AddAppPostgreSql` in `Extensions.cs` reads PolyzenKit's `DatabaseSettings` section, calls `UseNpgsql` and registers `UniqueViolationInterceptor` with PostgreSQL's duplicate-key check (`PostgresException { SqlState: PostgresErrorCodes.UniqueViolation }`, state `23505`), so a unique-index violation becomes `RecordExistsException`.
- **Case:** PostgreSQL compares text with its case. A column compared with user input (an email, a name, a code, a slug) or holding an app name gets the ICU collation `case_insensitive` (`und-u-ks-level2`, non-deterministic), declared once in `OnModelCreating` with `modelBuilder.HasCollation("case_insensitive", locale: "und-u-ks-level2", provider: "icu", deterministic: false)` and set on the property with `.UseCollation("case_insensitive")`, as PolyzenKit's guide shows for app names. Lookups and unique indexes on it then ignore case.
- **Searches:** a pattern search escapes the user's text with PolyzenKit's `LikePattern.Containing(search)` and passes `LikePattern.EscapeCharacter` to `EF.Functions.ILike`. PostgreSQL won't run `ILIKE` on a non-deterministic collation, so a `case_insensitive` column is searched as `COLLATE "default"`: `EF.Functions.ILike(EF.Functions.Collate(e.Email, "default"), pattern, LikePattern.EscapeCharacter)`.
- **Times:** every `DateTime` stored or passed as a query parameter is UTC (`DateTimeKind.Utc`), because Npgsql refuses other kinds for `timestamp with time zone`. Take the time from `TimeProvider`, and convert a `DateTime` that comes from a request to UTC before it reaches a query.
**Keys and concurrency**
- Entities use `Guid` keys configured with `ConfigureGuidPrimaryKey()` (time-ordered v7 GUIDs). Child rows that are only reached through their parent may use `long` keys with `ConfigureLongPrimaryKey()`.
- Every uniqueness rule has a unique index. A handler may also check first to give a precise message; the index is what guarantees it.
- An aggregate that can be changed by concurrent requests carries a `Guid Version` concurrency token, rotated on every change. A conflict becomes a 409.

**Repositories** (`Persistence/Repositories/<Feature>/`)
- Each holds an interface and an implementation. The interface extends `IEntityRepository<TEntity, TKey>` and `IItemEntityRepository<TEntity, TKey>`; the class extends `EntityRepository<TEntity, TKey>`.
- `GetEntityByAlternateIdAsync` looks up by the entity's natural key, scoped to the app, and ends with `queryCondition.Apply(existing, key)`.
- Repositories return data or `null`. The only exception they throw is `NoRecordException` from a `MustExist` lookup or a DTO read by id.
- No business rules and no response building in repositories.
- Write one query method with nullable filters (`search == null || ...`), not one method per combination of filters.
- Lists filter first, then order with a unique tie-breaker (`.ThenBy(e => e.Id)`), project with `.Select(XDto.Basic)` and end with `ToPageAsync(page, pageSize, cancellationToken)`.

**Transactions**
- A handler calls `IUnitOfWork.SaveChangesAsync` once. Use `BeginTransactionAsync`, `CommitTransactionAsync` and `RollbackTransactionAsync` only when several saves must succeed or fail together.
- Every begin needs a commit or a rollback on every path, early returns included. A handler that begins a transaction while another handler's is open joins it, and only the outermost commit commits.

**Migrations**
- Generate them with the global `dotnet-ef` tool, version 10: `dotnet ef migrations add <Name> --project <Service>`.
- The service applies pending migrations at startup with `await app.AppMigrateDatabaseAsync()`.
- Hand-write SQL in a migration only when existing data needs it.

## Application

### Handlers
Each file under `Application/Core/<Feature>/{Commands,Queries,Events}` holds one record and its handler. Handlers implement PolyzenKit's interfaces:

| Interface | Method | Input |
|---|---|---|
| `ICreateItemCommandHandler<TCommand, TViewModel>` | `CreateItemAsync(command, userId, ct)` | `IBaseCommand` |
| `IUpdateItemCommandHandler<TCommand, TViewModel>` | `UpdateItemAsync(command, userId, ct)` | `IBaseCommand` |
| `IDeleteItemCommandHandler<TCommand, TViewModel>` | `DeleteItemAsync(command, userId, ct)` | `IBaseCommand` |
| `IGetItemQueryHandler<TQuery, TViewModel>` | `GetItemAsync(query, ct)` | `IBaseQuery` |
| `IGetItemsQueryHandler<TQuery, TViewModel>` | `GetItemsAsync(query, ct)` | `IBaseQuery` |
| `IEventHandler<TEvent>` | `HandleEventAsync(event, ct)` | `IBaseEvent` |

- An action that isn't create, read or delete (approve, cancel, fulfil) is an update command of its own: `CancelOrderCommand` with `IUpdateItemCommandHandler`.
- Write handlers on audited entities derive from `BaseAuditableItemCommandHandler<TKey, TEntity, TRepository>` and save through `CreateEntityItemAsync` and `UpdateEntityItemAsync`, which set the audit fields. `CreateEntityItemAsync` first checks the natural key with `QueryCondition.MustNotExist`.
- View models are records: `ItemDetailBaseViewModel` for one item, `ItemsBaseViewModel` for a list (with `TotalCount`), `DeleteRecordViewModel` for deletes, `BaseViewModel` otherwise. They hold DTOs, never entities.
- Read the time from an injected `TimeProvider` (`GetUtcNow().UtcDateTime`), never `DateTime.UtcNow`. Log through `ILogger<T>`.
- Logic several handlers of a feature use goes in the feature's `Shared/` folder, one class per job, named for its job (`Products/Shared/ProductEditor.cs`). Nothing sits directly in the feature folder.
- Interfaces that Application needs from outside go in `Application/Interfaces`.
- Bind the service's own settings with `configuration.GetRequiredSetting<XSettings>()`, so a missing section or an invalid value fails at startup. A settings type is a sealed class with init-only properties: a class so that `ToString` can't print a secret, and init-only because settings are read once at startup. Nothing runs when a property is set; beyond its rules, the class holds at most read-only values worked out from its own members. The rules live on the settings class: `required` for a setting that must be set (PolyzenKit checks it, since the configuration binder doesn't), data annotations for other single-setting rules, and `IValidatableObject` for rules that span several.

### Commands and validation

**Binding**
- There are no request classes. Endpoints take the command as a parameter, read from the body of a POST or PUT. A DELETE with a body needs `[FromBody]`.
- Fields the server sets (app, user, route ids) are `[JsonIgnore]` with `internal set`, and the endpoint fills them in. Such strings start as `string.Empty`.
- Create and update commands that take the same body share one input record: `record CreateXCommand : XInput, IBaseCommand`.
- An update replaces the whole resource.

**Validation** has three homes, and each rule is checked in exactly one of them:
1. **Single fields:** data annotations, including the service's own attributes in `Common/Validation`. `AddValidation()` rejects bad values with a 400 before the handler runs. Only attributes count: a non-nullable property isn't required unless it has `[Required]`.
2. **Several fields of one request:** `IValidatableObject` on the command or input record.
3. **Rules that need stored data or current state:** the handler or a `Shared/` class.

**Errors**
Throw PolyzenKit's exceptions; `AddAppExceptionHandler` maps them to JSON errors, and `app.UseStatusCodePages()` gives the 401, 403, 404 and 405 that never reach it the same body:

| Exception | Status |
|---|---|
| `InvalidDataException`, `RecordExistsException` | 400 |
| `NoRecordException` | 404 |
| `IllegalOperationException` (not allowed in the current state) | 406 |
| `DbUpdateConcurrencyException` | 409 |
| Anything else | 500 |

- Error text lives in `Common/Errors/ErrorMessages`, grouped in nested static classes. Entity labels come from the `EntityTypes` enum's `[Description]`, read with `.Description()`.
- Pass the request's `CancellationToken` through endpoints, handlers and repositories. An endpoint takes it as a `CancellationToken` parameter, which ASP.NET Core binds to `HttpContext.RequestAborted`.

**Enums**
- In `Common/Enums`, travelling and stored under their member names (`OutOfStock`): PolyzenKit's `AddAppEndpoints` writes them as strings, and the DbContext stores each with `HasConversion<string>()`. No `[JsonConverter]` attribute.
- Bind fixed choices such as sort orders and filters as enums, not as validated strings.

## Presentation
- **One static class per entity** in `Presentation/Endpoints/` (`ProductEndpoints`), with a `MapProductEndpoints(this IEndpointRouteBuilder endpoints)` that starts from `endpoints.MapApiGroup("Product")`. The group gives the route (`api/v1/product`), the scope entity and the OpenAPI tag, so every endpoint is in one.
- **One named `internal static` method per endpoint.** It takes the route values, the command, the `ApiRequest`, the handler and the `CancellationToken`. Keep it thin: fill the fields the server sets, call one handler, and return a `ViewModelResults` result (`CreateItem` returns 201). Add `.Produces<TViewModel>()` so the OpenAPI document has the response schema.
- **`ApiRequest`** holds the caller: `LoggedInUserId`, `AuthorizedParty`, `XAppName`, `IsAdmin`, `ScopeResource` and `GetRegisteredAppAsync`. Unit tests build one with `ApiRequest.For(httpContext)`.
- **`Program.cs`** calls `AddAppEndpoints()` and `AddValidation()`. The validation source generator covers only the endpoints of the project that calls `AddValidation()`, so the service calls it itself.
- `AddAppAuthorization` sets a fallback policy, so an endpoint without a policy needs a signed-in user, and an anonymous request to an unknown path gets a 401. Every group or endpoint still states its access: a policy such as `.RequireAuthorization(AuthorizationPolicies.AdminOrUserScope)`, or `.AllowAnonymous()` for what's open to anyone with the API key.
- Paged lists bind `[Range(1, PagingLimits.MaxPage)] int page = 1` and `[Range(1, PagingLimits.MaxPageSize)] int pageSize = PagingLimits.DefaultPageSize` from the query string.
- Per-IP limits on public endpoints (sign-up, login, code checks) use `.RequireRateLimiting(RateLimitSettings.ClientIpPolicy)`. Behind a proxy, also `AddAppForwardedHeaders` and `app.UseForwardedHeaders()` first.

## Apps and access
- **Calling app:** the token's `azp` claim, read as `ApiRequest.AuthorizedParty`. Anonymous endpoints take it from the `X-App-Name` header (`XAppName`). Every query filters by app.
- **Not found:** another app's or another user's record raises `NoRecordException` (404), exactly like a missing one.
- **Roles:** admin roles are checked as `<app>:<role>` from `RoleSettings:AdminRoles`. The global admin role is not an admin of any app.
- **Scopes:** `<service>-<entity>:<action>`, where `<service>` is `RoleSettings:ServiceName` lower-cased, `<entity>` is the `MapApiGroup` name in kebab-case as in the route, and the action comes from the HTTP method (`create`, `read`, `edit`, `delete`) or is `all`.
- **Scope strings don't name an app**, so a scope-protected endpoint must act only on the caller's app, or also require a role that app admins can't grant.
- **Ownership:** a user who may change only their own records is checked in the handler against `LoggedInUserId`.

## Messaging and background work

**Messages (NATS JetStream)**
- Send with `ICreateItemCommandHandler<CreateOutboxMessageCommand, OutboxMessageViewModel>` before the save or commit that holds the change, never after it. It only adds the row, so the handler that calls it still saves. The service maps `OutboxMessageEntity` in its `AppDbContext`.
- It throws for a message over 1,000,000 bytes, which fails the save. Put a length limit on every request field copied into a message, so an oversized value gets a 400 instead of a 500.
- A message handler lives in `Infrastructure/Messaging`, implements `IMessageHandler` with `[MessageSubject("<sender>.<name>")]`, turns the message into an event record and calls one `IEventHandler<TEvent>` in Application.
- Delivery is at least once: handlers must be safe to run twice for the same message. Throw `InvalidDataException`, `RecordExistsException`, `NoRecordException` or `IllegalOperationException` for a message that can never succeed, and let anything else throw so the message is retried. Pass the handler's `CancellationToken` through: after `MessagingSettings:HandlerTimeoutSeconds` it's cancelled and the message is retried, even if the handler is still running.

**Scheduled clean-ups**
- Use `AddAppPeriodicDeleteCommand<TCommand>(interval)`. Every replica runs it, so the handler must cope with another replica removing the same rows.

**Other HTTP services**
- Give each endpoint its entry in `ExternalServicesSettings` ([Classes, files and folders](#classes-files-and-folders)) and call it through PolyzenKit's `BaseExternalService` and `IHttpRequestSender`, behind an interface in `Application/Interfaces`.

## Service standard
Every service repo has the same shape; only the service name and port change.

**Settings**
- `appsettings.json` holds every setting with a default that works locally, including dummy keys. `ApiKey` and `AppSecretKey` need at least 32 characters even as dummy values, or the service won't start. Real secrets come from the environment.
- The `Logging` section holds only `LogLevel:Default` (`Warning`). PolyzenKit's `AddAppLogging` logs to the console, as plain text in Development and as JSON otherwise. `AddAppTelemetry("Npgsql")`, called after it, sends logs, traces and metrics over OTLP only when `OTEL_EXPORTER_OTLP_ENDPOINT` is set. There's no `appsettings.Development.json`.
- `launchSettings.json` has one profile named after the service, setting only `ASPNETCORE_ENVIRONMENT=Development`, the URL and `launchUrl: openapi/v1.json`.

**Build**
- The solution is `<Service>.slnx`.
- `Directory.Build.props` holds what every project shares: `TargetFramework`, `Nullable`, `ImplicitUsings`, `AnalysisLevel` `latest-recommended`, `EnforceCodeStyleInBuild`, `TreatWarningsAsErrors` with `WarningsNotAsErrors` `NU1901;NU1902`, and `GenerateDocumentationFile` with `CS1591` in `NoWarn`. A package with a known high or critical vulnerability (NU1903, NU1904) therefore fails the restore: update it, or, while no fixed version exists, add `<NuGetAuditSuppress Include="<advisory URL>" />` for that advisory alone.
- `Directory.Packages.props` holds every package version, and project files reference packages without versions.

**Docker Compose and scripts**
- `docker-compose.yml` sets only what differs inside Docker: container hostnames, plus the `EDDSA_*` keys passed through from the shell.
- The database is the shared `db` block, PostgreSQL `postgres:18.7` with its `postgres_data` volume, identical in every repo. Copy it from the starter in PolyzenKit's `service-rules/Readme.md`.
- No `container_name`, custom networks, `image:` on built services or bind mounts. Data lives in named volumes.
- Other services run from published images pinned by tag.
- `up.ps1` takes `-UseLocal` (dependencies in Docker, the service with `dotnet run`) and `-Build`. `down.ps1` keeps named volumes.

**Docker image**
- The Dockerfile copies `Directory.Build.props`, `Directory.Packages.props` and `.editorconfig` before the project file, since the restore needs the package versions and the build the analyser settings. It restores and publishes for `$TARGETARCH` with `ExcludeEfDesign=true`, skips the formatter with `CSharpier_Bypass=true`, copies only the project folder besides those three files, and runs as `$APP_UID`.
- `.dockerignore` allows only the project folder and those three files.

**Endpoints**
- `/health` from `app.MapHealthChecks("/health").AllowAnonymous()`, listed in `ApiKeySettings:ExcludedUris`. `AddAppMessaging` adds a NATS check; a service without it calls `AddHealthChecks()`.
- OpenAPI from `Microsoft.AspNetCore.OpenApi`, mapped in Development only with `.AllowAnonymous()`, and `/openapi/v1.json` in `ApiKeySettings:ExcludedUris`.
- Requests for sending by hand live in `<Service>/<Service>.http`, next to the project file, and cover only the main flows; the tests are the automated checks. Settings are variables at the top of the file, and later requests read tokens and ids from earlier responses (`# @name adminLogin`, then `{{adminLogin.response.body.$.userToken.accessToken.token}}`). Use only syntax that both Visual Studio and VS Code's REST Client support.

**Readme**
- Sections, in this order: Summary, Running the App (Prerequisites, Instructions), the service's own sections, Settings, API Requests, Running the Tests, Adding Migrations.
- Repos may be public. Readmes and in-repo `CLAUDE.md` files don't name other services, their topics or endpoints, or the hosting. Say a message is sent to or received from NATS JetStream, and that tokens come from the token issuer.

## Tests
- xUnit v3 on Microsoft Testing Platform (selected in `global.json`).
- **Mocking:** a new service uses Moq. A service whose tests already use FakeItEasy keeps it: write its new tests with FakeItEasy, and don't move existing tests from one library to the other.
- Mark steps `// Arrange`, `// Act`, `// Assert`, or `// Act & Assert` when the call is inside `Assert.Throws*`.
- Pass `TestContext.Current.CancellationToken`. Match any token in setups with `It.IsAny<CancellationToken>()` (Moq) or `A<CancellationToken>._` (FakeItEasy).
- **Unit tests** (`<Service>.UnitTests`) cover pure logic and handlers with mocked repositories, mirroring the service's folders.
- **Integration tests** (`<Service>.IntegrationTests`) cover repositories, migrations and HTTP. HTTP tests run the app through `WebApplicationFactory<Program>` against a Testcontainers PostgreSQL (`postgres:18.7`), sign their own tokens, and reset the database with Respawn (`DbAdapter.Postgres`, schema `public`) between tests.
- Repository tests may use the EF InMemory provider unless they depend on the database's behaviour: unique indexes, concurrency tokens, transactions, collation or `ILike` searches.
- Test the access rules: another app's record is a 404, and a lower role gets a 403 or 404.
- Don't write tests that pin the number of types, files or enums.

## Checks

| What changed | Run |
|---|---|
| Any .NET code | `dotnet build -clp:ErrorsOnly`, which also formats, and `dotnet test --project <Service>.UnitTests/<Service>.UnitTests.csproj` |
| Persistence, messaging, email or HTTP | Also `dotnet test --project <Service>.IntegrationTests/<Service>.IntegrationTests.csproj` (needs Docker) |
| Formatting | `./format.ps1`, which applies the fixes, then fails the Release build on anything left |

Run the Docker Compose end-to-end check only when asked.
