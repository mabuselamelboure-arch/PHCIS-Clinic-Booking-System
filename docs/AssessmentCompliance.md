# Assessment compliance baseline

Source: PHCIS Blazor Practical Assessment - Senior Developer.pdf, pages 1-3, read in full on 30 October 2026. This file distinguishes document wording from engineering scope decisions.

| Requirement (attributed summary) | Classification | Location | Verification/evidence | Remaining work |
|---|---|---|---|---|
| Page 1: identify patients, clinic staff and administrators | Mandatory analysis | docs/Requirements.md | Stakeholder table inspected | None |
| Page 1: list functional requirements; booking, cancellation and rescheduling are examples | Mandatory analysis; named examples illustrative | docs/Requirements.md | Booking implementation; cancellation/rescheduling documented as future workflows | Retain booking scope; no unsupported claim of cancellation |
| Page 1: list security, performance, scalability requirements | Mandatory analysis | docs/Requirements.md | NFRs documented, no load guarantee claimed | Verify security implementation |
| Page 1: basic architecture | Mandatory | docs/Design.md; four-project solution | Source and baseline build inspected | Render diagrams |
| Page 1: Patients, Clinics, Appointments, Time slots data model | Mandatory | Api/Data/Entities.cs; BookingDbContext.cs; migrations | SQLite execution, relational tests | Execute SQL Server migrations and constraints |
| Page 1: ERD/flowchart/sequence diagram | Mandatory (diagram alternatives) | docs/Design.md | Mermaid source exists | Render and inspect |
| Page 1: Blazor WASM | Mandatory | PHCIS.Client | Build and static assets pass | Real browser execution |
| Page 1: booking, availability, confirmation, validation/no double booking | Mandatory | BookingService; BookingEndpoints; Home.razor | 28 baseline tests; historical live SQLite HTTP checks | SQL Server concurrent requests and browser checks |
| Page 1: SOLID, naming, comments/documentation | Mandatory | focused service/interface, DTOs, DI; docs | Source inspection | Maintain during changes |
| Page 1: basic unit tests | Mandatory | PHCIS.Tests | 28/28 baseline executed this continuation | Expand meaningful regressions |
| Page 2: Blazor, MudBlazor or FluentUI, FluentValidation, HttpClient | Recommended overview; framework repeats mandatory page 1 | Client; Contracts | Blazor/MudBlazor/validation/HttpClient implemented | Browser validation |
| Page 2: ASP.NET Core Web API and EF Core | Recommended overview; adopted | Api | Build and integration tests pass | SQL Server execution |
| Page 2: AutoMapper for DTO mapping | Recommended overview | Explicit projections in endpoints/service | Deliberate deviation: four small contracts do not justify mapping infrastructure | Retain explicit mapping and explain |
| Page 2: FluentValidation for business enforcement | Recommended; adopted | Contracts/BookingContracts.cs; BookingService | Validator and relational rules tested | Extend where security requires |
| Page 2: Serilog or NLog | Recommended alternatives | Program.cs; appsettings.json | Serilog observed | Retain no patient/request-body logging |
| Page 2: SQL Server or PostgreSQL | Recommended alternatives; SQL Server adopted as submission database | Api provider/migrations | SQL Server instance accessible; migration not yet applied in this baseline | Dedicated database and evidence |
| Page 2: EF Core Migrations | Recommended; adopted | Data/Migrations | Generated migration; previous consistency check passes | Apply and verify SQL Server |
| Page 2: Dapper | Explicitly optional | Not used | EF queries sufficient for scope | None |
| Page 2: Identity or Azure AD B2C; JWT access | Recommended overview; adopted to demonstrate bounded security | To be implemented | Currently anonymous; no ownership protection claimed | Identity, JWT, patient ownership and tests |
| Page 2: xUnit or NUnit | Recommended alternatives; adopted | PHCIS.Tests | xUnit 28/28 baseline | Preserve |
| Page 2: Moq | Recommended tool for mocking | Not used | Real relational DB and fixed clock provide meaningful tests | Add only for a useful isolation need; no box-ticking mock |
| Page 2: Playwright or bUnit | Recommended alternatives; Playwright adopted | To be implemented | Built-in browser tools failed initialization previously | Independent Playwright route authorized by directive |
| Page 2: GitHub Actions or Azure DevOps | Recommended alternatives; GitHub Actions adopted | To be implemented | No remote publishing authorized | Add restore/Release build/test/publish workflow |
| Page 2: Docker | Recommended overview; adopted for reproducible packaging | To be implemented | Not yet executed | Practical Dockerfile; document external SQL Server configuration |
| Page 2: Swagger/OpenAPI | Recommended alternatives; adopted | Development /openapi/v1.json | Historical live HTTP 200 | Retain and verify |
| Page 3: Twilio/SendGrid reminders | Explicitly optional | Not implemented | Excluded | None |
| Page 3: Outlook/Google calendar sync | Explicitly optional | Not implemented | Excluded | None |
| Page 3: SignalR | Explicitly optional | Not implemented | Excluded | None |
| Page 3: GraphQL | Explicitly optional | Not implemented | Excluded | None |

## Scope decisions

The document labels page 2 a Technology Stack Overview rather than a mandatory package checklist. Selected framework/database alternatives are adopted; AutoMapper and Moq are consciously not added without a real need. Identity/JWT, browser tests, CI and Docker are adopted because they improve the assessed deliverable. No external identity provider or page-three optional integration is needed.

Cancellation/rescheduling appear as examples under requirements analysis, not as explicit implementation tasks. The submission lists them as future requirements. Implementing them would introduce appointment state, history and cancellation-aware uniqueness and is not necessary for the bounded booking workflow. Only patient capabilities are currently required; staff/admin stakeholders are documented without inventing management features.
