# Verification and handover record

Date: 30 October 2026, Africa/Johannesburg. These are executed results, not estimated completion. No external source repository or hosted CI run was created.

## Final measured results

| Check | Executed result |
|---|---|
| Existing baseline before changes | Debug build: 0 warnings/errors; original 28 tests passed |
| Final application Release build | 0 warnings, 0 errors |
| Expanded .NET suite with SQL Server enabled | 36 passed, 0 failed, 0 skipped |
| SQL Server migrations | InitialCreate and AddPatientIdentity applied; no pending model changes |
| Real SQL Server simultaneous HTTP booking | Both requests gated after prechecks; exactly one 201 and one 409 |
| SQL Server integrity | One booking/patient in isolated race; unique-key rollback and FK errors executed |
| Identity/JWT | Real registration/login, lockout, token expiry/audience/signature, anonymous rejection and cross-owner protection passed |
| Build-path Chromium against SQL Server | 8 passed, 0 failed |
| Published Windows Release app against SQL Server | 8 passed, 0 failed; actual WASM execution |
| Linux Docker published app with disposable SQLite | 8 passed, 0 failed; runtime UID 1654 |
| Fresh local clone on this workstation | Tool/locked restore, Release build, 36 tests, publish, npm ci, Chromium install and 8 browser tests passed |
| Fresh-clone tracked working tree after tests | Clean |
| Diagrams | Architecture, ERD and sequence rendered to SVG/PNG and visually inspected |
| CI | Workflow authored/reviewed; hosted execution unverified because no remote/run was authorized |

SDK 10.0.403 is pinned; all projects target net10.0 and Microsoft runtime/framework packages are 10.0.14. Docker runtime is 10.0.14. Browser tooling is Playwright 1.64.1 with its installed Chromium; Mermaid 12.2.0 renders the diagrams. npm ci reported zero vulnerabilities at execution time; this is not a full security audit.

## SQL Server evidence

A running instance was discovered using Windows authentication. Product version: 16.0.1210.3. Dedicated assessment database: PHCIS_Assessment_20261030_617d64da. No unrelated database was reused or modified.

Both migrations are present in __EFMigrationsHistory. Final read-only inspection after browser runs found 8 appointments, zero duplicate TimeSlotId groups and zero Patient rows without an appointment. All data is fictional.

SqlServerTests creates a new PHCIS_Assessment_Test_ GUID database per fixture, applies actual migrations and dispatches genuinely concurrent HTTP requests. A test interceptor holds each request before saving until both have passed availability checks. Assertions execute 201/409 results, UTC confirmation and owned confirmation retrieval, availability removal, one persisted appointment/patient, unique-index failure 2601/2627 with rollback, and FK failure 547. Cleanup only deletes its generated database after checking its ownership prefix and length. This verifies correctness, not throughput/load capacity.

## Commands executed

From C:/Assessment, with externally supplied SQL configuration where required:

~~~powershell
dotnet build PHCIS.sln --nologo
dotnet test tests/PHCIS.Tests --no-build --nologo
dotnet test tests/PHCIS.Tests -c Release --nologo
dotnet test tests/PHCIS.Tests -c Release --no-build --nologo
dotnet ef database update --project src/PHCIS.Api --no-build
dotnet ef database update --project src/PHCIS.Api --configuration Release --no-build
dotnet ef migrations has-pending-model-changes --project src/PHCIS.Api --configuration Release --no-build
dotnet build PHCIS.sln -c Release --no-restore --nologo
dotnet publish src/PHCIS.Api -c Release --no-build --no-restore -o artifacts/publish
docker build -t phcis-assessment:local .
~~~

From tests/PHCIS.BrowserTests:

~~~powershell
npm ci --no-fund
npx playwright install chromium
npm test
npm test -- --grep 'booking validation'
npm run diagrams
~~~

PHCIS_BROWSER_URL selected the running SQL Server app, published SQL Server app or container for each eight-scenario run. Without it, the harness started its own new SQLite demo file and stopped the server afterward. Routine evidence is ignored under work/; PHCIS_CAPTURE_EVIDENCE intentionally refreshes reviewed screenshots.

## Fresh local checkout

A local clone was created using git clone --no-hardlinks into ignored work/clean-checkout-bb85efc. It built the bb85efc application revision, then fast-forwarded to b1691c9 for the evidence-output-path correction. git diff bb85efc HEAD -- src tests/PHCIS.Tests was empty: no compiled application or .NET test sources changed between these revisions.

Executed there: tool restore, locked NuGet restore, Release build, 36 .NET tests with SQL Server enabled, publish, npm ci, Chromium installation and eight browser scenarios using an automatically started new SQLite database. Final git status --short was empty. This proves a fresh checkout on this workstation with installed prerequisites/caches, not a pristine operating-system installation.

## Browser coverage

Eight scenarios cover registration, clinic/date/slot selection, missing/invalid input and correction, double-click suppression, saved confirmation/focus, refreshed availability, two-tab competition, stale responses after rapid clinic/date changes, API unavailability/recovery, keyboard order, mobile width, console errors, MudBlazor assets, expired-session recovery and empty dates.

Reviewed PNG/SVG files and sanitized provider-specific summaries are in docs/evidence. No credential-bearing traces are recorded. Actual successful UI submission and confirmation establish framework execution; an HTTP 200 index page alone is not counted.

## Failures corrected

- Initial build found advisories in transitive PKCS 9.0.11; patched 10.0.14 was pinned, without suppression.
- An old API process locked Windows rebuild binaries; only that assessment process was stopped.
- The SQL fixture expected two clinics while its own fixture plus seed created three; its assertion was corrected.
- Failure injection initially severed a required EF navigation before database execution; the fixture now injects an actual invalid identity FK and verifies propagation/rollback.
- An initial browser locator matched both summary and inline errors; it was narrowed.
- A real validation layout shift removed errors during mousedown/blur and moved the button before mouseup. Single-error cascade, inline-only booking errors and reserved space fix it; the original double-click regression passes without a blur workaround.
- Published HTML retained an unresolved bootstrap fingerprint placeholder. A partial fix conflicted with legacy UseBlazorFrameworkFiles and returned 500. The final host uses MapStaticAssets aliases and a stable bootstrap URL. Both the asset regression and actual published Chromium suite pass. Early published startup tests failed/timed out; the remaining run was stopped for diagnosis and was not counted as verified.
- Docker restore optimization omitted static-asset source inputs and failed NU1004. Full-source restore with a package-cache mount fixed it; the final pinned image built and executed eight browser scenarios.
- One EF CLI invocation used -c, which selects a DbContext, instead of --configuration; it was corrected before database application.
- Earlier HTTP harness errors used PowerShell's reserved HOME variable and misread automatic JSON DateTime conversion; these were corrected, not product defects.

## Remaining boundaries

No unresolved blocker remains for the explicitly bounded assessment workflow. Hosted GitHub Actions execution remains unverified. Linux Docker-to-SQL Server authentication/network setup was not executed: native Windows-authenticated SQL Server and Linux-container SQLite were independently verified.

AutoMapper/Moq deviations, illustrative cancellation/rescheduling, excluded page-three integrations and security limits are explicit in AssessmentCompliance.md, Requirements.md and README. No email verification/recovery, MFA, refresh tokens, immediate JWT revocation, clinical operations certification, or load/scalability guarantee is claimed. Deployment requires its own TLS, durable secrets, hostname and database access configuration.

## Follow-up live patient verification

A requested fictional account was registered through the browser and checked in Identity. This exposed an account-form layout defect: clearing validation on blur moved Create account between mousedown and mouseup, so no request was sent. PatientSignIn now uses inline messages in reserved space and concise error text. Added a browser regression for correcting registration and sign-in errors with one click.

Executed after the fix: Release build 0 warnings/errors; AuthenticationTests 6 passed/0 failed/0 skipped; Chromium suite 9 passed/0 failed. The corrected published app then completed named fictional patient registration, SQL Identity persistence/hash-presence checks, browser login/booking/sign-out/re-login, linked SQL booking/ownership records, slot removal, own confirmation HTTP 200, other authenticated user HTTP 404 and anonymous HTTP 401. Browser console/assets, keyboard order, confirmation focus and mobile overflow checks passed. Own retrieval after re-login was verified via authenticated API; no history/lookup UI is claimed.

A separate verifier command-line error and process interruption required regeneration of only that fictional test account's password via standard Identity reset. No SQL account insert or custom hashing was used; plaintext credentials stayed in process memory/standard input, never Git/logs/screenshots. Named-patient evidence is outside the repository in the task output directory. No schema, package or backend authentication change was needed. Earlier 36-test and container/clean-checkout counts above remain historical evidence; this follow-up reran the relevant six authentication and nine browser tests.
