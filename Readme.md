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
`docker-compose.yml` starts PostgreSQL, NATS (with JetStream), a stand-in code service (MockServer), Grafana's LGTM stack and UserIdentity.

- `.\up.ps1 -UseLocal` starts PostgreSQL, NATS, the stand-in code service and the LGTM stack, waits for them to be healthy, then runs UserIdentity from source on http://localhost:5050 with the `UserIdentity` launch profile. The OpenAPI document is at http://localhost:5050/openapi/v1.json.
- `.\up.ps1 -Build` builds the image, runs everything in Docker and waits for it to be healthy. UserIdentity is on http://localhost:5050.
- `.\up.ps1` does the same without rebuilding.
- `.\down.ps1` stops the stack and preserves its named volumes for the next run.

The first run of `up.ps1` on a machine generates an Ed25519 key pair with a pinned openssl image, encrypted with the passphrase in `appsettings.json` (`TestKeyPassPhrase-For-Local-Use-Only`), and saves it in `~/.local-eddsa-keys`. Every run reads it from there into `EDDSA_PRIVATE_KEY` and `EDDSA_PUBLIC_KEY`, which `docker-compose.yml` passes to the container and `-UseLocal` to the app. Other local stacks that use the folder share the pair, so tokens issued here work there. Delete the folder to get a new pair, then log in again. Start the stack with `up.ps1`, not `docker compose up` alone, so the keys are set.

`up.ps1` also sets the [bootstrap admin](#the-first-admin), which `docker-compose.yml` passes to the container and `-UseLocal` to the app: on its first start, UserIdentity registers the app `TestApp` and creates `admin` (password `Adm1n@P@ss`), who is the global admin and `testapp:admin`. A database that already has a global admin keeps its users as they are. Every local client uses the API key `TestApiKey-For-Local-Use-Only-0123456789`.

NATS monitoring is on http://localhost:8222, and NUI, a web UI for browsing streams and messages, is on http://localhost:31311 (add a connection to `nats://nats:4222` with NUI's own user, `ui`, password `ui-local`, which has full rights in this local stack only). UserIdentity's NATS user, `useridentity` (password `useridentity-local`), may only publish to UserIdentity's own subjects and create or update its own stream, and receives answers only on its own inboxes (`_INBOX_useridentity.>`): UserIdentity reads no subjects.

The stack's code service (see [Codes](#codes)) is a MockServer stand-in on http://localhost:1080, which `docker-compose.yml` and `-UseLocal` point `CodeCheckSettings` at. It sends no emails or texts: it accepts `123456` as the right code for every check, answers `WrongCode` to any other code, and `Sent` to every resend. Every request UserIdentity sends it, with its headers (the service token included) and body, appears in MockServer's dashboard at http://localhost:1080/mockserver/dashboard.

The LGTM stack (`grafana/otel-lgtm`: Grafana, with Loki for logs, Tempo for traces and Prometheus for metrics) collects UserIdentity's telemetry. Open Grafana on http://localhost:3030 and sign in as `admin` with the password `admin`. Loki, Tempo and Prometheus are already added as data sources; use **Explore** or **Drilldown** to search them.

- UserIdentity sends them only when it runs in Docker, where `docker-compose.yml` sets the three `OTEL_EXPORTER_OTLP_*` variables. Run from source with `-UseLocal`, it sends nothing.
- `OTEL_EXPORTER_OTLP_PROTOCOL` is `http/protobuf` because the endpoint is the stack's OTLP/HTTP port, 4318, while the .NET default is gRPC. The port isn't published, since only containers send to it.
- `OTEL_EXPORTER_OTLP_HEADERS` holds a dummy `Authorization=Basic <base64 of instance id:token>`, which the local stack ignores. A hosted OTLP backend needs the same three variables, with its own endpoint and a real token passed in from the environment, never written in a compose file.
- UserIdentity starts once the stack is healthy, so its startup logs and traces arrive too. The stack takes about half a minute to become healthy.
- The service is `service_name="useridentity"` in Loki, `resource.service.name = "useridentity"` in Tempo and `job="useridentity"` in Prometheus. Metric names carry their unit, such as `http_server_request_duration_seconds_count`.
- Every request's span and log lines carry its request id (the response's `X-Request-Id` header), app and user, set as `request.id`, `app.name` and `user.id`. To follow one request, search Loki for `{service_name="useridentity"} | request_id="<id>"` (Loki turns the dots into underscores) or Tempo for `{ span.request.id = "<id>" }`.
- The stack runs five processes and needs about 1 GB, so its container is limited to 2 GB. Its data lives in the `lgtm_data` volume.
- The stack is for local use only: the sign-in is the default one and nothing is secured.

The app applies database migrations on startup.

`GET /health` checks PostgreSQL and NATS. It answers `Healthy`, or 503 with `Unhealthy` while either can't be reached.

## Apps, Accounts and Admins
Each account belongs to the app it registered with. Logging in, refreshing a token or resetting a password through any other app fails as if the account didn't exist. A token carries only its app's roles (`<app>:*`, where `<app>` is the app's name in lower case), plus the global admin role.

There are two kinds of admin:

- **App admins** have the `<app>:admin` role. They manage their app's roles (names must start with `<app>:`, and are stored in lower case) and those roles' scopes, give those roles to their app's users, read and change their app's users, reset their two-step sign-in, and manage their app's invite codes and wait list. Anything belonging to another app returns 404, or 400 for a role name outside the prefix.
- **Global admins** have `RoleSettings:GlobalAdminRole` (default `global:admin`), whatever app they log in through. They manage registered apps and their sign-in settings, and every app's roles, role scopes and users. For invite codes, wait list entries, sign-in settings and user changes they name the app in the `X-App-Name` header. The app names `global` and `service` are reserved.

### Registered Apps
Global admins register apps with `/api/v1/registered-app`: the app's name, base URL and branding, and its backends, each with a name and an Ed25519 public key (see [Accounts Created by an App's Backend](#accounts-created-by-an-apps-backend)). `appSecretKey` is optional. The sign-in rules aren't part of the registered app: UserIdentity keeps them per app in its own [sign-in settings](#sign-in-settings).

### Sign-in Settings
`GET /api/v1/sign-in-settings` and `PUT /api/v1/sign-in-settings`, for global admins, act on the app named in the `X-App-Name` header. A `PUT` replaces every setting, so it sends all of them. An app without saved settings has every switch off and no default role, which is what a new app needs.

| Setting | Meaning |
|---|---|
| `requireConfirmedAccount` | Whether an account must prove its email (or its phone, when it has no email) with a code before it gets a session. Turn it on once the app's codes reach its users; while it's off, accounts sign in as soon as they sign up |
| `requireInviteCode` | Whether sign-up needs an [invite code](#invite-codes) |
| `defaultRole` | The role every sign-up gets, without the `<app>:` prefix (`user` gives `<app>:user`), stored in lower case and created on first use. A role name without spaces or colons, of up to 155 characters, that isn't `admin` or one of `RoleSettings:AdminRoles`. Without one, sign-ups get no role |
| `requireTwoFactor` | Every account must use [two-step sign-in](#two-step-sign-in) |
| `requireTwoFactorForAdmins` | Accounts holding `<app>:admin` (or another of `RoleSettings:AdminRoles`) or the global admin role must use two-step sign-in |
| `allowAuthenticatorTwoFactor`, `allowEmailTwoFactor`, `allowSmsTwoFactor` | The methods users may choose. Requiring two-step sign-in, for everyone or for admins, needs at least one |

```
PUT /api/v1/sign-in-settings
X-App-Name: testapp

{ "requireConfirmedAccount": true, "requireInviteCode": false, "defaultRole": "user", "requireTwoFactor": false, "requireTwoFactorForAdmins": true, "allowAuthenticatorTwoFactor": true, "allowEmailTwoFactor": true, "allowSmsTwoFactor": false }
```

Both answer `{ "signInSettings": { "appId": 1, "requireConfirmedAccount": true, ... } }`.

An app that doesn't require confirmed accounts can't hide which emails have accounts: a new sign-up signs in at once, so a taken email gets an error (see [Signing Up](#signing-up)). Requiring confirmed accounts is the fix.

### The First Admin
No user starts as an admin. The `BootstrapAdminSettings` create the first one when the app starts:

| Setting | Meaning |
|---|---|
| `BootstrapAdminSettings__AppName`, `BootstrapAdminSettings__AppBaseUrl` | The app the admin logs in through. Unless an app of that name exists, it's registered without a secret and without sign-in settings, so confirmation is off and there's no default role |
| `BootstrapAdminSettings__UserName`, `BootstrapAdminSettings__FirstName`, `BootstrapAdminSettings__Email`, `BootstrapAdminSettings__Password` | The admin's account, created confirmed. The password must follow the password rules |

Setting any of them requires all of them. The bootstrap runs only while no user has the global admin role, so later starts change nothing. It creates the account, gives it the global admin role and `<app>:admin`, and stops the app from starting if the user name or email is taken, so an account someone else signed up with never becomes an admin. Once the admin has logged in, remove the settings, or at least the password, from the environment.

After that, admins grant roles with `POST /api/v1/role/user`. If no user holds the global admin role any more, setting the bootstrap settings again, with a new user name and email, creates a new global admin at the next start.

Roles are read into the token at login, so the user has to log in again to get an admin token.

### Role Scopes
A role can carry scopes, which give its users access to specific actions in other services without making them admins. A scope is `<resource>:<action>`, where a service's resources are `<service>-<entity>` and the entity is its route segment, such as `catalog-option-definition:create` for `/api/v1/option-definition` in a `catalog` service. The actions are `create`, `read`, `edit`, `delete`, `download` and `all`.

- `POST /api/v1/role/claim` with `{ "roleId": "...", "resource": "catalog-product", "action": "create" }` adds a scope. Resources and actions are stored in lower case.
- `GET /api/v1/role/claim/{roleId}` lists a role's scopes.
- `DELETE /api/v1/role/claim` with the same body as adding removes one.

App admins manage the scopes of their app's roles, and get 404 for any other role. Global admins manage any role's scopes, except the global admin role's: that role is in every app's tokens, so adding a scope to it returns 400. Tokens carry the scopes of the app's roles from the next login or refresh.

## Invite Codes
An invite code lets one email address sign up to an app, and can give the new account a role. App admins manage their app's invite codes, and global admins name the app in the `X-App-Name` header.

- `POST /api/v1/invite-code` with `{ "inviteCode": "...", "userEmail": "...", "role": "<app>:member" }` creates one. The code has 10 to 255 characters and the email must be a valid address. `role` is optional. When set, it must be an existing role of the app that isn't an admin role (`<app>:admin` or any of `RoleSettings:AdminRoles`), or the request returns 400. There's one invite code per app and email.
- `GET /api/v1/invite-code?page=1&pageSize=50&search=member&applied=false` lists the app's invite codes, newest first, with `totalCount`. Optional `search` (email, up to 100 characters) and nullable `applied` filters are combined before counting/paging; omission retains the full list.
- `GET /api/v1/invite-code/{inviteCodeId}` and `GET /api/v1/invite-code/email/{email}` read one.
- `DELETE /api/v1/invite-code/{inviteCodeId}` revokes an unused invite code. A used one returns 400, and another app's returns 404.

Signing up with `inviteCode` checks it whether or not the app requires invite codes: the email is required, an invite code must exist for the app and email, the code must match (ignoring case), and it must be unused. Otherwise the request returns 400, with the same message whether the invite is missing, wrong or used, so sign-up never says whether an email has an invite. A valid code is marked used, and its role is given to the new account with the app's default role, if it has one, so it's in the account's tokens with its scopes. If that role no longer exists, sign-up returns 400 and nothing is saved. Without `inviteCode`, sign-up only needs one when the app requires it.

## Wait List
`POST /api/v1/wait-list` with the `X-App-Name` header and `{ "userEmail": "..." }` adds an email to the app's wait list. Anyone can call it. The email must be a valid address of up to 256 characters, and any other field in the body is ignored. App admins read their app's entries with `GET /api/v1/wait-list/{waitListId}` and `GET /api/v1/wait-list/email/{email}`, and global admins name the app in the `X-App-Name` header.

## Sign-in Steps
Every endpoint that can end in a session answers with the next step instead of always returning tokens: `register`, `login`, `confirm`, `two-factor/verify`, `two-factor/setup/confirm` and `update-password`, and the profile's `two-factor/change`. All of them send the `X-App-Name` header.

| `nextStep` | Means | Comes with |
|---|---|---|
| `Session` | Signed in | `userToken`. Also `recoveryCodes`, once, right after setting up a method |
| `ConfirmEmail` | Type the code just sent to the email | `ticket`, `codeSentTo` |
| `SecondFactor` | Type the second-step code | `ticket`, `method`, and `codeSentTo` for email and text |
| `SetUpSecondFactor` | Choose a method and set it up | `ticket`, `allowedMethods` |

```
{
  "user": { "id": "...", "userName": "...", "firstName": "...", "email": "...", "twoFactorMethod": "Email", ... },
  "nextStep": "SecondFactor",
  "ticket": "...",
  "method": "Email",
  "codeSentTo": "s***@example.com",
  "requestStatus": "Request Successful",
  "statusMessage": "Login successful"
}
```

- `codeSentTo` is the masked address the code went to: the first letter and the domain of an email (`s***@example.com`), or the last three digits of a phone number (`***123`).
- `method` and `allowedMethods` are `Authenticator`, `Email` and `Sms`, in that order.
- A session's `userToken` is `{ "accessToken": { "token": "...", "expiresIn": 180 }, "refreshToken": "..." }`.

A **ticket** is a random 256-bit token. Only its hash is stored, with its step, user, app and expiry, and what the step waits for: the code it expects, whether the email was proved in this flow, and the method and phone being set up. Each ticket works for one step only: finishing the step deletes it, and the next step gets a new ticket. The `ConfirmEmail` ticket from a sign-up lasts `LoginTicketSettings:ValidForSeconds` (default 1 hour), and every other ticket `TwoFactorSettings:TicketLifetimeSeconds` (default 10 minutes).

One place decides the next step, from the account, the app's sign-in settings and two facts about the flow so far: whether the user proved their email in this flow (with a sign-up code, a reset code or an emailed sign-in code), and whether they passed the second step. The first rule that matches wins:

| # | When | Next step |
|---|---|---|
| 1 | The app requires confirmed accounts and the account isn't confirmed | `ConfirmEmail`, with a new `LoginCode` |
| 2 | The user has a method and passed the second step in this flow | `Session` |
| 3 | The user's method is email and the email was proved in this flow | `Session` |
| 4 | The user has a method | `SecondFactor`. For email and text, a `LoginCode` is sent now |
| 5 | Two-step sign-in is required for the account, the user has no method, and the email was proved in this flow | `SetUpSecondFactor` |
| 6 | As in 5, but the email wasn't proved | `ConfirmEmail` with a `LoginCode`, then rule 5 |
| 7 | Anything else | `Session` |

A right sign-up, reset or emailed code marks the email confirmed, so a confirmed email is never asked for again. A texted code proves the phone, not the email. A method the app no longer allows counts as no method.

## Sessions
- Each refresh returns a new refresh token, and the one sent can't be used again. If a replaced refresh token is sent again, UserIdentity treats it as copied and ends that session. Clients must keep the newest refresh token, and must not send the same refresh token twice at once (for example, from two tabs): the second of two simultaneous refreshes gets a 409.
- A session lasts `RefreshTokenSettings:ValidForSeconds` (default 30 days) and each refresh extends it. Only hashes of refresh tokens are stored.
- `POST /api/v1/user/refresh-token` answers 401 and ends the session when two-step sign-in is required for the account and it has no method, which catches sessions that started before the requirement (the app switched it on, or the user became an admin). The next sign-in leads to setup.
- `POST /api/v1/user/logout` with the access token and `{ "refreshToken": "..." }` ends that session. The access token keeps working until it expires.
- Five failures in a row lock an account for five minutes: wrong passwords and wrong second-step codes of any method count together. A right password alone doesn't reset the count; only starting a session does. A locked account gets the same 401 as a wrong password.
- Each session belongs to the app it signed in through, and its refresh token works only through that app. It records the client's address and `User-Agent` (cut to 256 characters) at sign-in, and again at each refresh. Access tokens carry the session's id in the `sid` claim.

### Seeing and Ending Sessions
These follow the rule of `GET /api/v1/user/{userId}`: the user themselves, an app admin for accounts of their app that hold no admin role, or a global admin who names the app in `X-App-Name`. Anything else answers 404. They cover the sessions of the app the request works on.

- `GET /api/v1/user/{userId}/sessions` lists the unexpired sessions, most recently active first: `{ "sessions": [ { "id", "signedInAt", "lastActiveAt", "expiresAt", "ipAddress", "device", "current" } ] }`. `current` marks the session whose access token made the request.
- `DELETE /api/v1/user/{userId}/sessions/{sessionId}` ends one session. An unknown id, or another account's or app's, answers 404.
- `DELETE /api/v1/user/{userId}/sessions` ends every session but the one making the request. For an admin acting on someone else's account, that's all of them.

Ending a session deletes its refresh token. Its last access token keeps working until it expires (`JwtIssuerSettings:ValidForSeconds`, 3 minutes by default), since services check access tokens without asking UserIdentity. Ending a session sends no notice.

## Codes
Codes prove that the user holds an email address or a phone number. UserIdentity asks for a code by sending a message over NATS JetStream, through its outbox; the code service that receives it sends the code and keeps it. When the user types the code, UserIdentity asks the code service whether it's right and acts on the answer in the same request. Resending goes the same way.

- **The calls:** `POST CodeCheckSettings:VerifyUrl` with `{ "action", "correlationId", "code" }`, which answers `200` with `outcome` `Verified`, `WrongCode`, `Expired`, `NoAttemptsLeft` or `NotFound`, and `POST CodeCheckSettings:ResendUrl` with `{ "action", "correlationId" }`, which answers `Sent`, `TooSoon`, `NoResendsLeft` or `NotFound`. Anything else, such as a timeout, another status or an unknown outcome, fails the request with a 500 and changes nothing.
- **Who asks:** each call carries a service token that UserIdentity signs for the app (`azp`), with the `service:internal` role, the scope `<CodeCheckSettings:ServiceName>-message:create`, and UserIdentity's own `RoleSettings:ServiceName` in lower case as `sub`, which is also its subject prefix. The code service only checks codes that the caller asked for itself, and `KeySetSettings:ServiceCanGenerateTokens` must stay on. The `X-Api-Key` header comes from the `CodeCheck` entry of `ExternalServicesSettings`.
- **What users see:** every code that fails, whether wrong, expired, used up or unknown, and every unknown or expired ticket or reset request, answers 400 `That code didn't work. Check it, or send a new code.` The answer never says how many tries are left or how long to wait, so a decoy ticket (see [Signing Up](#signing-up)) can't be told from a real one. Unknown tickets still make a code check with a random correlation id, so they take as long.
- **Resending:** `POST /api/v1/user/resend-code` with the `X-App-Name` header and `{ "ticket": "..." }` or `{ "resetRequestId": "..." }` asks the code service to resend the code that ticket or reset waits for. It always answers `200` with the same body (`statusMessage` "Code resend requested"), whatever happened. Clients show their own countdown before offering it again.
- **Limits:** the code service allows a few tries per code and a few resends per correlation id. On top of that, UserIdentity's daily caps limit the second-step failures and the sign-in codes of each account (see [Two-Step Sign-in](#two-step-sign-in)).

## Signing Up
1. `POST /api/v1/user/register` with the `X-App-Name` header and `{ "firstName", "lastName"?, "userName", "userEmail"?, "phoneNumber"?, "userPassword", "inviteCode"? }`. The password rules are checked first, and the password is hashed whatever happens next.
   - **New email, app requires confirmed accounts:** the account is created, a `WelcomeUser` code (correlated by the user id) is sent, and the answer is a 201 with `ConfirmEmail`.
   - **New email, otherwise:** the account is created and the answer follows the [step table](#sign-in-steps): `Session`, or `ConfirmEmail` when two-step sign-in is required for the account.
   - **Taken user name or email, app requires confirmed accounts:** nothing is created. The answer is exactly like a new sign-up's, with a ticket that isn't stored. A `SignUpAttempted` notice goes to the existing account (the one with the email, otherwise the one with the user name), at most once a minute and five times a day (`AccountLimitSettings`); past that, nothing is sent and the answer stays the same. Codes typed against that ticket fail with the one failed-code message.
   - **Taken user name or email, otherwise:** 400 `We couldn't create this account.`
2. `POST /api/v1/user/confirm` with the `X-App-Name` header and `{ "ticket": "...", "code": "123456" }` checks the code the ticket waits for. On the right code the email is confirmed, the ticket is used up and the answer follows the step table with the email proved. A locked account gets a 401.

Login of an unconfirmed account in an app that requires confirmed accounts answers `ConfirmEmail` with a new `LoginCode`, and continues the same way.

## Two-Step Sign-in
Each user may have one method: an authenticator app, email or text messages, from the methods the app allows. Every method gets ten recovery codes (`TwoFactorSettings:RecoveryCodeCount`), so a user whose authenticator, inbox or phone isn't available signs in with a recovery code. Sign-in never switches to another channel, so an account is as strong as its one method.

Two-step sign-in is required for an account when the app requires it for everyone, or for admins and the account holds `<app>:admin`, another admin role or the global admin role.

### The Second Step
`SecondFactor` comes with the user's method. For email and text, a `LoginCode` has already been sent: for email to the email address only, for text to the phone only.

`POST /api/v1/user/two-factor/verify` with the `X-App-Name` header and `{ "ticket": "...", "code": "123456" }`, or `{ "ticket": "...", "recoveryCode": "xxxxx-xxxxx" }`:

- An authenticator code is checked by ASP.NET Core Identity's authenticator token provider, ignoring spaces and hyphens.
- An emailed or texted code is checked with the code service.
- A recovery code works once, for any method.
- Every wrong code counts toward the lockout, together with wrong passwords. An emailed or texted code counts only when the code service answers `WrongCode`.
- On success the ticket is used up, the failure count resets and the answer is `Session`. A right emailed code also proves the email.

In any 24 hours an account gets at most `AccountLimitSettings:MaxFailedSecondStepsPerDay` (10) failed second steps and `AccountLimitSettings:MaxLoginCodesPerDay` (10) sign-in codes. Past either, `login`, `two-factor/verify` and `two-factor/setup` answer the lockout's 401 and no code is sent, until the oldest counted event is a day old.

### Setting Up a Method
Setup is reached with a `SetUpSecondFactor` ticket, which needs the email proved in the same flow, so someone with only the password can't enrol their own authenticator.

1. `POST /api/v1/user/two-factor/setup` with `{ "ticket": "...", "method": "Authenticator" }`, `{ "ticket": "...", "method": "Email" }` or `{ "ticket": "...", "method": "Sms", "phoneNumber": "+254700000123" }`. A method the app doesn't allow answers 400.
   - **Authenticator:** Identity makes a new key. The answer is `{ "method": "Authenticator", "authenticatorKey": "...", "authenticatorUri": "otpauth://totp/<app>:<email>?secret=...&issuer=<app>&digits=6" }`, for a QR code and for typing in.
   - **Email:** `{ "method": "Email" }`; no code is needed, because the email was proved.
   - **Text:** a `LoginCode` goes to that phone only, and the answer is `{ "method": "Sms", "codeSentTo": "***123" }`.
2. `POST /api/v1/user/two-factor/setup/confirm` with `{ "ticket": "...", "code": "123456" }`, or without `code` for email. The first authenticator code or the texted code is checked (and a texted phone is stored as confirmed), then the method is stored, two-step sign-in is turned on, ten new recovery codes are made, a `TwoFactorChanged` notice is sent and the user's other sessions end. The answer is `Session` with `recoveryCodes`, shown once.

### Managing It from the Profile
These need a signed-in user of the app. The ones that take the password count a wrong password toward the lockout and answer 400 `That password isn't right.`, or `Too many failed attempts. Try again later.` while the account is locked.

- `GET /api/v1/user/two-factor` returns `{ "method": "Authenticator", "recoveryCodesLeft": 8, "required": false, "allowedMethods": ["Authenticator", "Email"] }`.
- `POST /api/v1/user/two-factor/change` with `{ "password": "..." }` answers `ConfirmEmail` with a `LoginCode`. Confirming it answers `SetUpSecondFactor`, and setup continues as above. The old key and codes go when the new method is confirmed.
- `POST /api/v1/user/two-factor/recovery-codes` with `{ "password": "..." }` answers `{ "recoveryCodes": [ ... ] }`; the old codes stop working. Without a method it answers 406.
- `POST /api/v1/user/two-factor/disable` with `{ "password": "..." }` removes the method, key and codes, sends `TwoFactorChanged`, and answers the status. It answers 406 while two-step sign-in is required for the account, or when it's off.

### Account Help
`POST /api/v1/user/two-factor/reset` with `{ "email": "..." }`, for app admins and global admins (who name the app in `X-App-Name`), removes the user's method, key and recovery codes, ends their sessions and sends `TwoFactorChanged` with the change `Reset`, which doesn't say who reset it. It answers the user. An unknown email, an account of another app, or an admin account when the caller is an app admin, all answer 404; only a global admin can reset an admin account.

### At Rest and the Code Window
ASP.NET Core Identity keeps the authenticator key and the recovery codes in its `user_tokens` table. UserIdentity's user store encrypts both with the `AppSecretSettings` keys (AES-256-GCM), so a database read doesn't give them away. Keep every key that encrypted something: a lost key makes those keys and codes unreadable, and the users need an [account reset](#account-help).

Identity accepts an authenticator code for about two and a half minutes, and the same code works more than once in that time. Each check still needs a fresh ticket, which needed the password.

## Password Reset
1. `POST /api/v1/user/reset-password` with the `X-App-Name` header and `{ "userName": "<user name or email>" }`. The response is always 200 with the same text and a `resetRequestId`, whether or not the account exists. For an existing user of the app, a reset is stored and a `PasswordReset` code is sent. Its correlation id is the reset's own id, not the `resetRequestId`, which only the requester gets and of which only a hash is stored, and it expires (`expiresAt`) with the reset. Another request for the same user within `PasswordResetSettings:RequestCooldownSeconds`, or after `PasswordResetSettings:MaxRequestsPerDay` requests (default 5) in the last 24 hours, sends nothing. Each request gets its own code and tries, so the daily cap limits how many codes can be guessed.
2. `POST /api/v1/user/resend-code` with `{ "resetRequestId": "..." }` asks for the code again.
3. `POST /api/v1/user/update-password` with the `X-App-Name` header and `{ "resetRequestId": "...", "code": "123456", "newPassword": "..." }`. A new password that breaks the password rules answers 400 before the code is checked. An unknown, expired (`PasswordResetSettings:ValidForSeconds`, default 15 minutes), used or other app's reset still makes a code check, then answers the one failed-code message. With the right code, the password changes, any lockout is lifted, all of the user's sessions and tickets end, and a `PasswordChanged` notice tells the user. The answer follows the step table with the email proved: `Session` for a user without a method or with the email method (or `SetUpSecondFactor` when it's required), `SecondFactor` for authenticator and text users.

The new password applies before the second step. Someone with only the inbox can change the password but can't sign in past an authenticator or text method, and the owner gets `PasswordChanged`. A profile's change of password uses the same flow.

## Updating User Details
Both endpoints use the rule `GET /api/v1/user/{userId}` uses: the user themselves, an app admin of the account's app, or a global admin who names the app in `X-App-Name`. An app admin can't change an account holding an admin role or the global admin role, other than their own; that, an unknown user and another app's user all answer 404.

- `GET /api/v1/user/email/{email}`, for app admins and global admins (who name the app in `X-App-Name`), finds an account by its email and answers `{ "user": { ... } }`. An unknown email, another app's account, or an admin account when the caller is an app admin all answer the same 404.
- `PUT /api/v1/user/{userId}` with `{ "firstName": "...", "lastName": "...", "phoneNumber": "..." }` replaces the three. The names change at once. A new, changed or removed phone is stored unconfirmed, becomes confirmed only through text-message setup, and sends `ContactChanged` with the change `Phone` to the account's email. While the account's method is text messages, changing the phone answers 406: the user changes the method first, so sign-in never breaks. The answer is the user.
- `POST /api/v1/user/{userId}/email` with `{ "email": "..." }`:
  - **The user themselves** also sends `"password"`, which counts toward the lockout. The change is stored as pending (one per user, replaced by a new request, lasting `TwoFactorSettings:TicketLifetimeSeconds`) and a `LoginCode` goes to the **new** address. The answer is `{ "user": { ... }, "codeSentTo": "n***@example.com" }`. An email that already has an account gets the same answer and no code. The daily sign-in code cap applies (406 `Too many codes were sent today. Try again tomorrow.`). `POST /api/v1/user/{userId}/email/confirm` with `{ "code": "123456" }` then sets the new email as confirmed and answers the user, or answers the one failed-code message.
  - **An admin's change** applies at once, with the email unconfirmed; the user proves it at their next sign-in. A taken email answers 400 `We couldn't change this email.`
  - Either way, a user name that was the old email changes with it, and the **old** address gets `ContactChanged` with the change `Email`, which doesn't say who made it.

## Deleting an Account
`DELETE /api/v1/user/{userId}` deletes the account at once. It follows the rule of [Updating User Details](#updating-user-details): the user themselves, an app admin for accounts of their app that hold no admin role, or a global admin who names the app in `X-App-Name`.

- **The user themselves** sends `{ "password": "..." }`. A missing password answers 400 `Your password is required to delete your account.`, and a wrong one counts toward the lockout and answers 400 `That password isn't right.` (or `Too many failed attempts. Try again later.` while the account is locked).
- **Admins** send no body.
- The last account holding the global admin role can't be deleted: 406.

In one transaction, UserIdentity deletes the invite codes and wait list entries that hold the account's email in its apps, adds an `AccountDeleted` notice to its outbox (see [Messages](#messages)), and deletes the account with its details, sessions, tickets, resets, email changes, app links, roles and two-step sign-in. The notice's correlation id is the deleted user's id, so other services that keep data about the user can act on it. Its last access token keeps working until it expires.

## Accounts Created by an App's Backend
`POST /api/v1/user/register-for-service` lets an app's own backend create accounts, for example while moving its existing users to UserIdentity as they next sign in. It takes the same body as `register`, plus the `X-Api-Key` header, and no token: the backend signs the request with its own Ed25519 key.

1. The backend makes a key pair and keeps the private key: `openssl genpkey -algorithm ed25519 -out backend.pem`, then `openssl pkey -in backend.pem -pubout -out backend.pub`. A global admin adds the backend to the app at `/api/v1/registered-app`, with a name (lower-case letters, digits and hyphens) and the public key in PEM.
2. Each request carries four headers: `X-App-Name` (the app), `X-App-Backend` (the backend's name), `X-Timestamp` (Unix seconds) and `X-Signature`, the base64 Ed25519 signature of these lines joined by `\n`, followed by the raw body:
   ```
   <timestamp>
   <method>
   <path and query, as sent>
   <body>
   ```
   For example: `printf '%s\nPOST\n/api/v1/user/register-for-service\n%s' "$ts" "$body" > request.txt`, then `openssl pkeyutl -sign -inkey backend.pem -rawin -in request.txt | base64 -w0`. UserIdentity rebuilds the path from the request it receives, so it mustn't sit behind a proxy that rewrites paths.
3. A timestamp more than `AppSignatureSettings:ToleranceSeconds` (default 300) away from UserIdentity's clock, either way, or a missing, wrong or unknown backend's signature, answers 401. The app is the one the signature names.
4. The account is created in that app, and **no code or other message is sent**. The answer is a 201 with `{ "user": { ... } }`, without a session or a ticket. A taken user name or email answers 400 with the explicit "exists" error, because the caller is trusted. The app's invite-code rule still applies.
5. The backend tells its frontend to sign in. The frontend's server calls `login` with the same user name and password; an unconfirmed account in an app that requires confirmed accounts gets `ConfirmEmail` with a `LoginCode`, and the flow continues as in [Signing Up](#signing-up).

To change keys, set the new public key as the backend's `nextPublicKey`, switch the backend to the new private key, then make it the current key. A replayed request works only within the tolerance, and only gets "exists". The endpoint covers users who are there to sign in: moving every user at once needs its own design, because legacy systems hold password hashes, and accounts still unconfirmed after `CleanupSettings:UnconfirmedUserLifetimeInDays` are deleted.

## Messages
UserIdentity adds these messages to its outbox in the same transaction as the change they report, and sends them over NATS JetStream. The code service sends them by email or text message from the app's templates for each action.

| Subject | Action | Kind | Correlation id | `expiresAt` | Goes to | Data |
|---|---|---|---|---|---|---|
| `useridentity.user.registered` | `WelcomeUser` | Code | The user id | None | The account's email and phone | `firstName`, `requestedAt`, `ipAddress`, `device` |
| `useridentity.password-reset.requested` | `PasswordReset` | Code | The reset's id | The reset's expiry | The account's email and phone | `firstName`, `requestedAt`, `ipAddress`, `device` |
| `useridentity.login-code.requested` | `LoginCode` | Code | The ticket's id, or the pending email change's id | The ticket's or the change's expiry | The email for email methods and email proof, the phone for text, the new address for an email change | `firstName`, `purpose` (`SignIn`, `ConfirmEmail`, `SetUp`, `ChangeEmail`), `requestedAt`, `ipAddress`, `device` |
| `useridentity.password.changed` | `PasswordChanged` | Notice | A new id | None | The account's email and phone | `firstName`, `changedAt`, `ipAddress`, `device` |
| `useridentity.sign-up.attempted` | `SignUpAttempted` | Notice | A new id | None | The existing account | `firstName`, `attemptedAt`, `ipAddress`, `device` |
| `useridentity.two-factor.changed` | `TwoFactorChanged` | Notice | A new id | None | The account's email and phone | `firstName`, `change` (`SetUp`, `Changed`, `TurnedOff`, `Reset`), `method`, `changedAt`, `ipAddress`, `device` |
| `useridentity.contact.changed` | `ContactChanged` | Notice | A new id | None | The old email for an email change, the email for a phone change | `firstName`, `change` (`Email`, `Phone`), `changedAt`, `ipAddress`, `device` |
| `useridentity.user.deleted` | `AccountDeleted` | Notice | The user id | None | The account's email and phone | `firstName`, `deletedAt`, `ipAddress`, `device` |

`requestedAt`, `attemptedAt`, `changedAt` and `deletedAt` are UTC times, `ipAddress` is the client's address (behind a proxy, see [Rate Limiting](#rate-limiting)) and `device` its `User-Agent` header, cut to 256 characters. Changes made by staff carry no address or device. Code templates should tell users never to share the code, say when and from where it was requested, and say what to do if it wasn't them; notices should say what changed, when and from where.

UserIdentity only publishes: it reads no subjects.

## Rate Limiting
Register, login, confirm, resend-code, reset-password, update-password, two-factor verify, setup and setup confirm, and the wait list sign-up allow `RateLimitSettings:PermitLimit` requests per `RateLimitSettings:WindowSeconds` (default 10 per minute) for each client IP and endpoint. Requests over the limit get a 429 with a `Retry-After` header.

Behind a reverse proxy, set `ForwardedHeadersSettings__KnownNetworks` to the proxy's network (comma-separated CIDR ranges). Otherwise every request appears to come from the proxy, and all clients share one limit.

Password hashing is limited across all clients too, because a hash takes most of a CPU for about 100 ms and would otherwise starve every other request. Login, register, `register-for-service`, update-password, the email change and the profile's two-factor change, recovery codes and disable run at most `PasswordHashingSettings:MaxConcurrent` at a time (default 1), with up to `PasswordHashingSettings:QueueLimit` more waiting (default 32). Requests beyond that get a 429.

## Captcha
While `CaptchaSettings:Enabled` is on, register, resend-code, reset-password and the wait list sign-up need a Cloudflare Turnstile token in the `X-Captcha-Token` header. The client renders the Turnstile widget with `data-action` set to the endpoint's action, `register`, `resend-code`, `reset-password` or `wait-list`, and gets a new token for each request, since a token is good for one check. A missing, invalid, reused or wrong-action token gets a 400, "The captcha check failed".

The check posts to `CaptchaSettings:VerifyUrl` (Turnstile's `siteverify` address) after the rate limit and before password hashing, so a slow answer (`ExternalServicesSettings:ExternalServices:Turnstile:TimeoutSeconds`, default 5) holds up only that request. When Turnstile can't be reached or refuses the check itself, such as for a wrong secret key, the request goes through and an error is logged.

The check is off by default. `appsettings.json` holds Cloudflare's always-pass test secret `1x0000000000000000000000000000000AA`, which fails every check while `AcceptTestKeys` is off. To try it locally, set `CaptchaSettings__Enabled=true` and `CaptchaSettings__AcceptTestKeys=true`, and send the dummy token `XXXX.DUMMY.TOKEN.XXXX`, as `UserIdentity/UserIdentity.http` does. Test keys answer without an action, so `AcceptTestKeys` lets them pass without the action check and logs a warning; keep it off everywhere else. To test the action check itself, use a separate widget whose hostnames include `localhost`.

Request bodies are limited to 64 KiB. Larger ones get a 413.

## Cleanup
Every `CleanupSettings:IntervalInMinutes` (default 60), expired refresh tokens, tickets and pending email changes are deleted, and so are password resets a day after they expire (kept that long for the daily cap) and the account limit events of the daily caps once they're a day old. Accounts still unconfirmed after `CleanupSettings:UnconfirmedUserLifetimeInDays` (default 30) are deleted too when every app they belong to requires confirmed accounts (an app without sign-in settings doesn't), along with their details, sessions, tickets, resets, app links and roles, and the invite code they used for their app can be used again.

## Settings
Every setting has a default in `appsettings.json` that works locally. In production, set at least these:

| Setting | Meaning |
|---|---|
| `EDDSA_PRIVATE_KEY`, `EDDSA_PUBLIC_KEY`, `KeySetSettings__PrivateKeyPassPhrase` | The Ed25519 key pair (PEM content) and the private key's passphrase |
| `AppSecretSettings__Keys__<id>`, `AppSecretSettings__CurrentKeyId` | Base64 256-bit keys that encrypt registered apps' secrets, authenticator keys and recovery codes at rest. Generate one with `openssl rand -base64 32` and keep it in a secret store: values encrypted with a lost key can't be recovered |
| `ApiKeySettings__ApiKey` | The key clients send in `X-Api-Key`, at least 32 characters |
| `CodeCheckSettings__VerifyUrl`, `CodeCheckSettings__ResendUrl` | The code service's check and resend addresses (see [Codes](#codes)) |
| `CodeCheckSettings__ServiceName` | The code service's name, which the scope of UserIdentity's service tokens names (`<name>-message:create`) |
| `ExternalServicesSettings__ExternalServices__CodeCheck__DefaultHeaders__X-Api-Key` | The code service's API key |
| `CaptchaSettings__Enabled`, `CaptchaSettings__SecretKey`, `CaptchaSettings__VerifyUrl` | Turns on the Turnstile check (see [Captcha](#captcha)) with the widget's secret key, at Turnstile's `siteverify` address. Keep the key in a secret store, and leave `CaptchaSettings__AcceptTestKeys` off |
| `BootstrapAdminSettings__*` | The first admin and the app they log in through, until a global admin exists (see [The First Admin](#the-first-admin)) |
| `DatabaseSettings__*` | Database connection |
| `MessagingSettings__NatsUrl` | The NATS server, without credentials |
| `MessagingSettings__NatsUser`, `MessagingSettings__NatsPassword` | UserIdentity's own NATS user. Keep the password in a secret store |
| `ForwardedHeadersSettings__KnownNetworks` | The reverse proxy's networks (see [Rate Limiting](#rate-limiting)) |

These have defaults that suit most deployments:

| Setting | Default | Meaning |
|---|---|---|
| `TwoFactorSettings__TicketLifetimeSeconds` | 600 | How long a ticket (other than a sign-up's) and a pending email change last |
| `TwoFactorSettings__RecoveryCodeCount` | 10 | Recovery codes made for each method |
| `AccountLimitSettings__MaxFailedSecondStepsPerDay` | 10 | Failed second steps per account in 24 hours |
| `AccountLimitSettings__MaxLoginCodesPerDay` | 10 | Sign-in codes per account in 24 hours |
| `AccountLimitSettings__SignUpAttemptNoticeCooldownSeconds`, `AccountLimitSettings__MaxSignUpAttemptNoticesPerDay` | 60, 5 | How often a `SignUpAttempted` notice can go to one account |
| `AppSignatureSettings__ToleranceSeconds` | 300 | How far a backend's signed timestamp may be from UserIdentity's clock |

`Logging:LogLevel:Default` (`Warning`) sets the log level, and `Logging:LogLevel:Microsoft.EntityFrameworkCore` sets EF Core's (`Error`). From the environment, set the level with `Logging__LogLevel__Default`. Logs go to the console as plain text in Development and as one JSON object per line everywhere else. Setting `OTEL_EXPORTER_OTLP_ENDPOINT` also sends logs, traces and metrics there over OTLP, with OpenTelemetry's other `OTEL_EXPORTER_OTLP_*` settings.

Settings are checked at startup. An invalid setting stops the app with every failure listed, such as `ValidForSeconds: The field ValidForSeconds must be between 1 and 2147483647.`

## API Requests
`UserIdentity/UserIdentity.http` holds requests for sending by hand, in an order that works from top to bottom: health, the OpenAPI document and the key set, logging in the admin, a user, roles with their scopes and users, invite codes, the wait list, the entities that scopes can name, registered apps, an app's sign-in settings with sign-up, confirmation, two-step setup and sign-in, a user's names and email, their sessions and the deletion of their account, a password reset, a refresh and a logout. It leaves out `register-for-service`, which needs a signed request. Open it in Visual Studio 2022 (17.8 or later), or in VS Code with the [REST Client](https://marketplace.visualstudio.com/items?itemName=humao.rest-client) extension, and select **Send Request** above a request. The tests are the automated checks.

- No environment needs selecting: the settings (URL, API key, app name, the admin's details and the code to send) are variables at the top of the file.
- Requests that use an earlier response, such as an admin's token or a ticket, need that request sent first.
- New users, roles, invites, wait list entries and registered apps are named after the current time, so the requests can be sent again.
- Access tokens last 3 minutes (`JwtIssuerSettings:ValidForSeconds`). When a request answers 401, send the admin's login again.
- `Code` is `123456`, which the stack's stand-in code service accepts for every emailed or texted code (see [Instructions](#instructions)). For the authenticator method, set it to the authenticator app's code. `TestApp` has no sign-in settings, so its new accounts sign in at once. Start with the admin's login: the bootstrap admin is the global admin and `testapp:admin`.

## Running the Tests
The tests use xUnit v3 on Microsoft Testing Platform (selected in `global.json`). The unit tests call each endpoint method directly with faked handlers. The integration tests send requests to the whole app and start PostgreSQL and NATS with Testcontainers, so Docker must be running. The smoke tests build the Docker image, run it as it's published (trimmed, single file) beside PostgreSQL and NATS, and reach it only over HTTP and NATS.

```
dotnet test --project UserIdentity.UnitTests/UserIdentity.UnitTests.csproj
dotnet test --project UserIdentity.IntegrationTests/UserIdentity.IntegrationTests.csproj
dotnet test --project UserIdentity.SmokeTests/UserIdentity.SmokeTests.csproj
```

Add `--coverlet` for a Cobertura coverage report. Set `USERIDENTITY_SMOKE_IMAGE` to run the smoke tests against an image that's already built.

## Adding Migrations
Migrations need the global `dotnet-ef` tool, version 10 (`dotnet tool install --global dotnet-ef`, or `update` if an older one is installed). It doesn't need a running database. The tool builds the database context from `DatabaseSettings` in `appsettings.json`, which `DatabaseSettings__*` environment variables override, so it doesn't need the Ed25519 keys.

```
dotnet ef migrations add <MigrationName> --project UserIdentity
```

## Persistent storage

Normal `./down.ps1; ./up.ps1` and rebuilds reuse project-scoped named volumes. PostgreSQL mounts `postgres_data` at `/var/lib/postgresql`; object storage and the broker also retain their data when present. The scripts resolve Compose relative to their own directory. Deliberate reset uses `docker compose down --volumes` and deletes the stack's stored data; it is separate from normal shutdown.

