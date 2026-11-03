# Assessment compliance

Source: PHCIS Blazor Practical Assessment - Senior Developer.pdf, all three pages read on 30 October 2026. Page 1 contains assessment tasks; page 2 is labelled Technology Stack Overview; page 3 explicitly lists Optional Enhancements. Summaries below are attributed, not invented quotations.

Status describes implemented scope; execution evidence is separated from source inspection. Final packaging/clean-checkout results are recorded in Validation.md rather than assumed here.

| Requirement / page | Classification and decision | Implementation location | Verification / evidence | Remaining work |
|---|---|---|---|---|
| Patients book, view slots, receive confirmation / 1 | Mandatory scenario | Home.razor; BookingEndpoints; BookingService | API tests, live SQL Server and Chromium flow | No missing core workflow |
| Identify patients, staff, admin / 1 | Mandatory analysis; capabilities not implied | Requirements.md | Stakeholder table inspected | None |
| List functional requirements, with booking/cancellation/rescheduling examples / 1 | Analysis mandatory; examples illustrative | Requirements.md FR01-FR10 | Booking implemented; exclusions explicit | Cancellation/rescheduling not implemented or claimed |
| List security/performance/scalability NFRs / 1 | Mandatory analysis | Requirements.md | Documented targets/limits; security tests | No load/scale guarantee claimed |
| Basic architecture / 1 | Mandatory, architecture style alternatives | Design.md; four projects | Source/build; rendered architecture PNG/SVG | None |
| Patients model / 1 | Mandatory | Data/Entities.cs; BookingDbContext; migrations | SQLite and SQL Server constraints; ERD | None |
| Clinics model / 1 | Mandatory | Same | SQL Server seed/persistence | None |
| Appointments model / 1 | Mandatory | Same | Atomic booking/rollback, unique slot | None |
| Time slots model / 1 | Mandatory | Same | Date filtering, relational/time constraints | None |
| ERD/flowchart/sequence diagrams / 1 | Mandatory diagrams; alternatives | Design.md; docs/evidence | All three Mermaid diagrams rendered and visually inspected | None |
| Blazor WASM / 1,2 | Mandatory framework; repeated overview | PHCIS.Client | Build, actual Chromium WASM execution | None |
| Booking logic / 1 | Mandatory | BookingService | 201 creation, missing/expired/mismatched rejection, SQL race | None |
| Availability / 1 | Mandatory | BookingEndpoints | Future/unbooked filtering; SAST boundaries; booked slot disappears | None |
| Confirmation / 1 | Mandatory; on-screen satisfies wording | Home; BookingConfirmation; owned GET | UI confirmation/focus; owner isolation tests | No email/SMS implied |
| Basic validation/no double booking / 1 | Mandatory | Shared validator; DB unique index | Original tests plus real SQL Server 201/409 race/rollback | None |
| SOLID/naming/comments/documentation / 1 | Mandatory quality criteria | IBookingService; DI; contracts; docs | Source review; focused responsibilities; meaningful names/comments | Maintain existing style |
| Basic unit tests / 1 | Mandatory | PHCIS.Tests | Original 28 preserved; Release suite expanded to 36 including SQL | None |
| MudBlazor or FluentUI / 2 | Recommended alternatives; MudBlazor adopted | Client package/layout/components | Component assets and browser checks pass | None |
| FluentValidation forms / 2 | Recommended; adopted | Contracts; BookingValidator | Browser invalid/corrected input; validation unit tests | None |
| HttpClient / 2 | Recommended; adopted | BookingApiClient; PatientSession | Actual same-origin browser/API calls | None |
| ASP.NET Core Web API / 2 | Recommended; adopted | Api minimal endpoints | API tests and runtime | None |
| EF Core / 2 | Recommended; adopted | BookingDbContext | Real relational persistence | None |
| AutoMapper / 2 | Recommended overview tool; deliberate alternative | Explicit DTO projections | Small typed contracts and straightforward projections remain easy to inspect; no mapping infrastructure needed | Not installed; deviation explicit |
| FluentValidation business rules / 2 | Recommended; adopted | Validator and BookingService | Inputs validated; existence/expiry/ownership/integrity enforced server-side | None |
| Serilog or NLog / 2 | Recommended alternatives; Serilog adopted | Program/appsettings | Structured runtime logging; no sensitive EF logging | None |
| SQL Server or PostgreSQL / 2 | Recommended alternatives; SQL Server selected for assessed persistence | SQL Server provider and migrations | Dedicated live database, FK/unique/transaction/concurrency execution | PostgreSQL unnecessary |
| EF migrations / 2 | Recommended; adopted | InitialCreate; AddPatientIdentity | Applied on dedicated database; no pending model changes | None |
| Dapper / 2 | Explicitly optional; excluded | Not used | EF sufficient for current indexed projections | None |
| Identity or Azure AD B2C / 2 | Recommended alternatives; Identity adopted for bounded security | Security/Authentication.cs; IdentityDbContext | Registration/login, policy, lockout and ownership tests | Email recovery/MFA not claimed |
| JWT secure API access / 2 | Recommended; adopted | JwtBearer; PatientSession; RequireAuthorization | Signature/audience/expiry, anonymous and cross-owner rejection | Production secret/TLS hosting must be supplied |
| xUnit or NUnit / 2 | Recommended alternatives; xUnit adopted | PHCIS.Tests | 36-case executed Release suite with SQL opt-in enabled | None |
| Moq / 2 | Recommended mocking tool; no mock requirement implied | Not used | Real relational DB, fixed clock and failure interceptors are meaningful; no dummy mock added | Deliberate deviation |
| Playwright or bUnit / 2 | Recommended alternatives; Playwright adopted | PHCIS.BrowserTests | Nine current Chromium scenarios; earlier eight-scenario SQL Server runs and screenshots/result summaries | Packaging provider runs tracked in Validation.md |
| GitHub Actions or Azure DevOps / 2 | Recommended alternatives; GitHub Actions adopted | .github/workflows/assessment.yml | Restore/Release/test/SQL/browser/publish/Docker definition | Publication and hosted results recorded separately in Validation.md |
| Docker / 2 | Recommended; adopted | Dockerfile; .dockerignore | Local image build; final runtime results in Validation.md | External SQL credentials/network/TLS are deployer configuration |
| Swagger/OpenAPI / 2 | Recommended alternatives; OpenAPI adopted | /openapi/v1.json in Development | JSON endpoint executed | Swagger UI unnecessary |
| Twilio/SendGrid reminders / 3 | Explicitly optional; excluded | Not implemented | No provider calls | None |
| Outlook/Google calendar / 3 | Explicitly optional; excluded | Not implemented | No provider calls | None |
| SignalR / 3 | Explicitly optional; excluded | Not implemented | Refresh workflow sufficient | None |
| GraphQL / 3 | Explicitly optional; excluded | Not implemented | REST adequate for supplied tasks | None |

## Bounded engineering decisions

The overview is not treated as a mandatory package checklist. Framework/provider alternatives are respected. AutoMapper and Moq are explicitly omitted where explicit mapping and real relational tests are clearer. Identity/JWT, Playwright, CI and Docker are adopted because they materially strengthen this assessment.

Cancellation/rescheduling appear only as examples in requirements analysis. They are listed as future requirements, not silently claimed complete. Adding them would require appointment state, historical records and cancellation-aware uniqueness; those schema/workflow changes are outside the explicit implementation task. No page-three optional feature is added. Staff/admin stakeholders are documented without inventing management capabilities or unnecessary roles.
