# PHCIS Clinic Booking System

Senior developer assessment: standalone **Blazor WebAssembly**, **ASP.NET Core Web API**, **SQL Server**, Identity/JWT, shared FluentValidation, MudBlazor and EF Core. A development-only SQLite mode enables a self-contained demonstration.

Start with docs/AssessmentCompliance.md for the attributed assessment scope, docs/Design.md for architecture/ERD/sequence diagrams, and docs/Validation.md for measured execution evidence and limitations.

## Prerequisites

- .NET SDK 10.0.403, pinned in global.json, CI and Docker so implicit Blazor dependencies match the lock files. All projects target net10.0. Upgrade the SDK and lock files deliberately together.
- Node.js 22 or newer and npm for browser tests/diagram generation.
- A modern browser. Playwright installs its own Chromium for automated tests.
- SQL Server for the assessed database path; SQLite demo requires no database server.
- Docker only for container verification. Windows examples use PowerShell 7.

## Quick local demonstration

From the repository root:

```powershell
dotnet tool restore
dotnet restore PHCIS.sln --locked-mode
dotnet build PHCIS.sln -c Release --no-restore
dotnet run --project src/PHCIS.Api --launch-profile http -c Release --no-build -- --demo-local
```

Open http://localhost:5080. Choose **Create an account**, enter a fictional email and a new password with at least 10 characters, uppercase, lowercase, a digit and a symbol. No demonstration password or privileged account is seeded. Select a clinic and future weekday, choose a slot, enter fictional patient details, and confirm. Keep the confirmation reference.

Demo mode is restricted to Development. It explicitly creates/seeds `src/PHCIS.Api/clinic-demo-auth.db`; the earlier anonymous `clinic-demo.db` is preserved. Demo slots are 30 minutes on weekdays from 08:00 to 14:00 SAST over the next 14 calendar days. Re-running seed extends the rolling window without duplicating slots. A 90-day search horizon does not guarantee slots beyond the seed window. Ctrl+C stops the app. Only one process should use a given demo file. `DemoDatabasePath` can point to a new disposable file, as the browser harness does under ignored work/.

The SQLite demonstration uses EnsureCreated, not the SQL Server migrations. After future schema changes, use a new disposable demo file or remove only your stopped, disposable demo database. Never use this procedure for a SQL Server or real patient database.

## SQL Server setup

Use a newly named, dedicated assessment database. Do not point at AIGIS, production or unrelated data. Your account needs permission to create/migrate that database. Supply configuration through the shell or a secret store, never a committed file:

```powershell
$env:ConnectionStrings__ClinicBooking = '<connection string for your dedicated assessment database>'
dotnet tool restore
dotnet ef database update --project src/PHCIS.Api --configuration Release --no-build
dotnet run --project src/PHCIS.Api --launch-profile http -c Release --no-build -- --seed-demo
dotnet run --project src/PHCIS.Api --launch-profile http -c Release --no-build
```

The seed command runs and exits. Normal startup never creates/migrates SQL Server automatically. The design-time factory uses the environment variable for database updates; generating migrations needs no live connection. Keep credentials outside source. Development can generate an ephemeral signing key; restarting invalidates previous development tokens.

## Configuration and security

| Setting | Purpose |
|---|---|
| `ConnectionStrings__ClinicBooking` | Dedicated SQL Server connection; required outside demo mode |
| `Jwt__SigningKey` | Base64 random signing key, at least 32 decoded bytes; required outside Development/Testing |
| `ASPNETCORE_ENVIRONMENT` | Development only for local HTTP/demo use; normal published hosting defaults to Production |
| `ASPNETCORE_URLS` | Listening URLs for host execution |
| `AllowedHosts` | Deployment hostname allowlist; defaults to localhost and 127.0.0.1 |
| `DemoDatabasePath` | Optional new SQLite file for Development demo/test isolation |
| `PHCIS_SQLSERVER` | Opt-in test server connection; tests create unique owned databases |

For a local production-configuration check, generate signing material directly into the process environment without printing or saving it:

```powershell
$env:Jwt__SigningKey = [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(64))
```

Use a managed secret store for a real deployment. HTTPS/HSTS are configured outside Development; terminate TLS and configure certificates/hosting properly. HTTP demo instructions are not a production deployment recipe.

Passwords use ASP.NET Core Identity. Five incorrect attempts lock an account for five minutes. Account endpoints have IP-based rate limits. JWT issuer, audience, signature and expiry are checked. Tokens last 15 minutes, remain only in browser memory, and are cleared on logout/reload. An expired session requires sign-in again. Logout does not revoke an already issued JWT server-side.

Booking and confirmation retrieval require authorization. Ownership comes from the validated subject, not a patient ID or email supplied in a booking body. Public clinic/availability queries expose schedule data only. No patient list or public appointment history is exposed. Confirmation excludes contact details. Logs do not include patient fields, passwords, tokens or request bodies; do not enable sensitive EF logging.

Email confirmation, recovery, MFA, refresh tokens, immediate revocation, staff/admin workflows and operational healthcare privacy controls are outside the bounded assessment. Use fictional data only. No email/SMS/provider calls occur.

## Automated tests

```powershell
dotnet test tests/PHCIS.Tests -c Release --no-build
```

Without PHCIS_SQLSERVER, the SQL Server test is explicitly skipped. For the full suite, set it to an available test server connection with database-creation permission:

```powershell
$env:PHCIS_SQLSERVER = '<test SQL Server connection>'
dotnet test tests/PHCIS.Tests -c Release --no-build
```

Each infrastructure test overrides the database name with a unique `PHCIS_Assessment_Test_` GUID name, applies migrations, runs real concurrent HTTP bookings and relational checks, and deletes only its own guarded database. Never provide an unrelated database as a test target. Other tests use isolated in-memory SQLite and a fixed clock. Legacy booking tests use a test-only claim scheme; dedicated security tests execute real Identity/JWT middleware. Latest measured counts are in Validation.md.

Browser tests:

```powershell
cd tests/PHCIS.BrowserTests
npm ci
npx playwright install chromium
npm test
npm run diagrams
```

The default harness starts the existing Release build at port 5085 in Development, using a new SQLite file under work/. It creates fictional accounts, verifies eight browser scenarios, and stops its server. For an already running assessment app, set `PHCIS_BROWSER_URL` to its URL; the harness then leaves server lifecycle to you. Do not target real patient data. Browser requests will create fictional accounts/bookings. Server account rate limiting remains active; use a new test server/window if repeatedly running account-heavy tests. Committed review screenshots are in docs/evidence. Ordinary test screenshots go to ignored work/evidence so test runs do not dirty tracked files. Set PHCIS_CAPTURE_EVIDENCE=1 only when intentionally refreshing the reviewed screenshots; transient results stay ignored. No credential-bearing traces are recorded.

## Publish and run

```powershell
dotnet publish src/PHCIS.Api -c Release --no-build --no-restore -o artifacts/publish
```

For a local published demonstration (intentionally Development), from the repository root:

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:ASPNETCORE_URLS = 'http://localhost:5086'
$env:DemoDatabasePath = Join-Path (Get-Location) 'work/published-demo.db'
New-Item -ItemType Directory -Path work -Force | Out-Null
Push-Location artifacts/publish
dotnet PHCIS.Api.dll --demo-local
Pop-Location
```

Production requires the SQL Server connection, signing key, hostname and TLS settings described above. Apply migrations and seed through the SDK project before starting the published runtime. No database updates run automatically.

## Docker

```powershell
docker build -t phcis-assessment:local .
docker run --rm -p 5087:8080 -e ASPNETCORE_ENVIRONMENT=Development -e DemoDatabasePath=/data/demo.db phcis-assessment:local --demo-local
```

This explicitly runs a disposable SQLite demo as a non-root Linux user. For persistence, mount a writable volume at /data. For SQL Server, omit --demo-local and inject ConnectionStrings__ClinicBooking and Jwt__SigningKey securely, plus the hostname/TLS hosting configuration. localhost inside the container is the container, not the host database. A Linux container cannot reuse this workstation's Windows integrated authentication; use a suitable SQL login/secret or supported external identity configuration. Never bake database credentials or signing material into the image. Database migration tools are not shipped in the runtime image.

## CI

`.github/workflows/assessment.yml` restores locked dependencies, builds/tests Release, provisions an isolated SQL Server container with an ephemeral generated password, publishes, runs Chromium tests, builds Docker and uploads evidence. No remote repository or workflow run has been created. Local execution evidence does not claim a hosted CI run passed.

## API

| Method | Route | Access/result |
|---|---|---|
| POST | `/api/auth/register` | Anonymous account creation; 200 token or 400 validation |
| POST | `/api/auth/login` | Anonymous; 200 token or generic 401; lockout applies |
| GET | `/api/clinics` | Public clinic DTOs |
| GET | `/api/clinics/{clinicId}/slots?date=yyyy-MM-dd` | Public future unbooked slots for SAST day |
| POST | `/api/appointments` | Bearer JWT; 201 confirmation, 400 invalid/expired/mismatched, 404 unknown slot, 409 conflict |
| GET | `/api/appointments/{reference}` | Bearer JWT; owned confirmation, otherwise 404 |
| GET | `/openapi/v1.json` | Development OpenAPI JSON, no Swagger UI |
| GET | `/health` | Process liveness only, not database readiness |

Booking JSON properties: `clinicId` (optional backward-compatible consistency check), `timeSlotId`, `fullName`, `email`, `phone`. No owner/patient ID is accepted. Server lookup supplies the authoritative clinic. Timestamps use explicit UTC markers and the UI displays SAST.

## Troubleshooting

- **Build cannot copy DLLs:** stop the previous API process before rebuilding on Windows.
- **No slots:** choose a future weekday in the 14-day window; rerun the explicit seed when needed.
- **401:** register/sign in again; page reload and token expiry clear/expire the session.
- **429:** wait for the account rate-limit window rather than disabling the control.
- **SQL connection/migration failure:** verify the dedicated target, database permissions, SQL connectivity and local development certificate trust. Do not solve it by targeting another application's database.
- **Old demo schema:** use the new auth demo file; EnsureCreated does not upgrade existing SQLite schemas.
- **Port in use:** stop only your prior assessment process or select another URL.
- **Browser missing:** run `npx playwright install chromium`; on Linux CI add `--with-deps`.
- **Locked restore mismatch:** use the documented SDK and committed source/lock files. The full Blazor source is required during Docker restore because static-asset inputs affect implicit SDK dependencies.

## Deliberate scope choices

The PDF's cancellation/rescheduling entries are illustrative requirements-analysis examples; no such workflow is claimed. Page-three reminders/calendar/SignalR/GraphQL and optional Dapper are excluded. Explicit mapping replaces AutoMapper for the small DTO surface. Real relational tests/fixed clocks replace unnecessary mocks; Moq is not added solely to reference it. See AssessmentCompliance.md for the full classification.
