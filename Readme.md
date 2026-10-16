# Summary 
Useridentity is an easy-to-use RESTful API that simplifies user management by providing a set of endpoints for managing user identities, creating user accounts, and handling role management. 

## Running the App
### Prerequisites
- MYSQL Database 
- Dotnet 10.0 SDK (pinned in `global.json`)
- Docker 
- Visual Studio (optional)
- An Ed25519 key pair for JWT signing and verification. You can generate a key pair using the following commands (replace the passphrase with a strong one of your choice):
```
    openssl genpkey -algorithm ED25519 -out ed25519-private.pem -pass pass:S3T@G00dPa22Phr@S3
    openssl pkey -pubout -in ed25519-private.pem -out ed25519-public.pem -passin pass:S3T@G00dPa22Phr@S3
 
```

Ensure to (at least) set two environment variables using the keys  `EDDSA_PRIVATE_KEY` and `EDDSA_PUBLIC_KEY` for the generated ed25519-private.pem and pued25519-public.pem keys' content.

- A 256-bit key for encrypting registered apps' secrets at rest, set as `AppSecretOptions__Keys__key1` (`key1` is the `AppSecretOptions:CurrentKeyId` in appsettings.json). Generate one with `openssl rand -base64 32` and keep it in a secret store - secrets encrypted with a lost key cannot be recovered. `launchSettings.json` and `docker-compose.yml` carry a dev-only key.

 - Postman (optional)

### Instructions

#### Running the App Locally
1. Clone the repository
2. Set the required environment variables. This can be done in project properties (on Visual Studio) or launchSettings.json
   
``` 
    KeySetOptions__PrivateKeyPath=EDDSA_PRIVATE_KEY
    KeySetOptions__PublicKeyPath=EDDSA_PUBLIC_KEY
    KeySetOptions__PrivateKeyPassPhrase=<Your Good Passphrase>
    AppSecretOptions__Keys__key1=<base64 32-byte key>
```
3. Run the docker-compose file using `docker compose -f docker-compose.yml up -d --scale useridentity=0` to start the database and redpanda dependencies.
4. Run the application using Visual Studio or the command line with `dotnet run` from the project directory.
5. Use the included Postman collection and environment to test the endpoints.

#### Running the App Using Docker Compose
1. Clone the repository
2. `cd` into the cloned folder
3. Run the docker-compose file using `docker compose -f docker-compose.yml up --build -d`
4. Use the included Postman collection and environment to test the endpoints

## Running the Tests
The tests use xUnit v3 on Microsoft Testing Platform (selected in `global.json`). The integration tests start MySQL and Redpanda with Testcontainers, so Docker must be running.

```
dotnet test --project UserIdentity.UnitTests/UserIdentity.UnitTests.csproj
dotnet test --project UserIdentity.IntegrationTests/UserIdentity.IntegrationTests.csproj
```

Add `--coverlet` for a Cobertura coverage report.

## Adding Migrations
`dotnet-ef` is pinned in the local tool manifest. MySQL must be running, since the server version is auto-detected.

```
dotnet tool restore
dotnet ef migrations add <MigrationName> --project UserIdentity
```

## App Callbacks
When a user registers through an app's UI and the app has a `CallbackUrl`, UserIdentity POSTs the user details to it. Each callback is signed with the app's `AppSecretKey`:

- `X-Timestamp`: Unix time in seconds.
- `X-Signature`: `sha256=` followed by the hex HMAC-SHA256 of `{timestamp}.{body}`.

Apps should recompute the signature over the raw request body, compare it in constant time and reject stale timestamps. .NET apps can use `HmacSignatureUtil.IsValidSignature` from PolyzenKit.
