# PHCIS Clinic Booking System

A first working version of the senior developer practical assessment. A standalone **Blazor WebAssembly** client is hosted by an **ASP.NET Core Web API**. SQL Server is the assessment database; an explicit SQLite mode supports a local demonstration without a database server.

## Prerequisites

- .NET 10 SDK (global.json selects 10.0.403 or a later .NET 10 feature band).
- A modern browser.
- SQL Server only if using the SQL Server instructions below.

## Quick start: local demonstration

Open PowerShell in `C:\Assessment`:

```powershell
dotnet restore PHCIS.sln
dotnet run --project src/PHCIS.Api --launch-profile http -- --demo-local
```

Visit **http://localhost:5080**. The API serves the WebAssembly client from the same origin. This mode explicitly creates `src/PHCIS.Api/clinic-demo.db` and seeds two fictional clinics with 30-minute weekday slots between 08:00 and 14:00 SAST over the next 14 calendar days. Choose a weekday with available future slots. Re-running the seed extends the rolling window without duplicating slots. Only run one demo instance at a time. Ctrl+C stops the application.

Use fictional details, such as `Alex Example`, `alex@example.test`, and `0820000000`. No external email, SMS, payment, or other provider is called. The browser receives an on-screen confirmation after persistence succeeds.

Local demo mode is restricted to the Development environment. It uses EnsureCreated for the throwaway SQLite database; it does not apply the SQL Server migrations. If changing the model later, remove only the disposable demo database after stopping the app, then restart demo mode.

## SQL Server setup

Use a dedicated, disposable assessment database and an account with permission to create its tables. Do not target an AIGIS or production database. Configure the connection string in the current shell, never in a tracked file:

```powershell
$env:ConnectionStrings__ClinicBooking = '<connection string for your dedicated PHCIS assessment database>'
dotnet tool restore
dotnet ef database update --project src/PHCIS.Api
dotnet run --project src/PHCIS.Api --launch-profile http -- --seed-demo
dotnet run --project src/PHCIS.Api --launch-profile http
```

The seed command runs and exits. Normal startup neither creates nor migrates a database. The design-time factory uses the environment variable for `database update`; generating or reviewing migrations does not require a connection. Remove the environment variable when finished. Credentials must remain outside the repository.

## Validation

```powershell
dotnet build PHCIS.sln
dotnet test tests/PHCIS.Tests --no-build
```

Tests use isolated in-memory SQLite databases, a fixed clock, and ASP.NET Core's in-process test host. No SQL Server instance, external provider, scheduler, or email service is started. Tests cover patient validation, SAST date boundaries, availability, booking persistence, API status codes, duplicate bookings, database uniqueness, transactional rollback, a deterministic booking race, and repeatable demo seeding. The recorded run passed 28 tests. See `docs/Validation.md` for results and outstanding checks. SQL Server execution and browser verification are separate from these automated tests.

## Structure

- `src/PHCIS.Client`: Blazor WASM, MudBlazor feedback/buttons, booking UI, typed HTTP client.
- `src/PHCIS.Contracts`: request/response DTOs, shared FluentValidation rules, clinic time conversions.
- `src/PHCIS.Api`: endpoints, booking service, EF model, SQL Server migrations, explicit demo seed.
- `tests/PHCIS.Tests`: focused unit and integration tests.
- `docs/Requirements.md`: stakeholders, requirements, acceptance criteria, and scope.
- `docs/Design.md`: architecture, ERD, booking sequence, and design tradeoffs.

## API

| Method | Route | Result |
|---|---|---|
| GET | `/api/clinics` | Clinics |
| GET | `/api/clinics/{clinicId}/slots?date=yyyy-MM-dd` | Available future slots in that clinic's SAST day |
| POST | `/api/appointments` | 201 confirmation; 400 validation; 404 unknown slot; 409 conflict |
| GET | `/openapi/v1.json` | OpenAPI document in Development |
| GET | `/health` | Process liveness only; does not check database readiness |

Example POST body (replace timeSlotId with a value returned by availability):

```json
{
  "timeSlotId": "20000000-0000-0000-0000-000000000002",
  "fullName": "Alex Example",
  "email": "alex@example.test",
  "phone": "0820000000"
}
```

## First-version boundaries

This is an **unauthenticated assessment demonstration**, not a production healthcare service. The implemented workflow is clinic selection, availability, patient details, booking, and on-screen confirmation. No patient-list or appointment-history endpoints are exposed. The server remains authoritative and the database enforces one appointment per slot.

Authentication/authorization, cancellation, rescheduling, clinic administration, stable patient identity/deduplication, email/SMS, reminders, calendar sync, SignalR, GraphQL, CI/CD, and Docker are not implemented. The assessment's technology overview lists some of these; clarify which are expected before final submission. Authentication and patient ownership must be implemented before handling real patient information. HTTPS is enforced outside Development; hosting settings and security controls require deployment-specific configuration. AllowedHosts defaults to localhost and 127.0.0.1.

DTO mapping is explicit because the contracts are small; no AutoMapper, generic repository, microservices, or unnecessary layers are introduced. Logs exclude patient fields and request bodies. Booking references use random GUIDs and do not grant access to patient records.

## Manual review checklist

- Book a future weekday slot and check clinic, date, time, and reference on confirmation.
- Start another booking and verify the booked slot is absent.
- Open the same slot in two tabs: the second submission should report a conflict and refresh availability.
- Submit missing/invalid patient details; check readable validation messages.
- Select a weekend or a date without slots; check the empty state.
- Change clinic/date quickly; verify the last selection wins.
- Check the layout and keyboard navigation at desktop and narrow browser widths.
