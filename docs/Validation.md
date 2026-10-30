# Validation record

Validated on 30 October 2026.

- Solution build: passed with zero warnings and zero errors.
- Focused xUnit suite: 28 passed, zero failed, zero skipped.
- EF migration consistency: no pending model changes.
- SQL Server InitialCreate migration: generated and indexes/foreign keys reviewed; not applied to a SQL Server instance.
- Running local SQLite demo: home, app stylesheet, MudBlazor stylesheet/script, Blazor bootstrap, and Development OpenAPI document returned HTTP 200.
- Real HTTP booking against the local demo: HTTP 201 with a UTC timestamp and confirmation; repeated booking returned HTTP 409; booked slot disappeared from availability.
- Automated tests cover validation, SAST day boundaries, explicit UTC serialization, availability, atomic persistence/rollback, database uniqueness, deterministic competing booking, repeatable seeding, HTTP errors and hosted component assets.
- No external SQL Server service, email/SMS provider, payment provider or scheduler was started.

## Outstanding checks

- Browser interaction, visual layout, keyboard behavior and narrow viewport checks: not executed. Both available browser automation tools failed to initialize on this host. HTTP asset checks are not a substitute for browser execution.
- Apply migrations and validate the booking/concurrency flow on a dedicated SQL Server assessment database. SQLite tests do not verify SQL Server-specific exception behavior or locking.
- Resolve the assessment's authentication/authorization, cancellation/rescheduling, CI and Docker expectations before treating the submission as complete.

See README.md for run instructions and the manual review checklist. The disposable local demo database contains fictional bookings from the HTTP verification.

## SQL Server foundation milestone

The original suite was rerun: build 0 warnings/errors, 28 passed. A provider-specific opt-in test now executes migrations against a fresh owned SQL Server database, two genuinely concurrent HTTP requests gated after both availability checks, 201/409 outcomes, confirmation UTC, availability removal, one patient/appointment, duplicate-key rollback (2601/2627), and foreign-key error 547. Result: 29 passed, 0 failed, 0 skipped with PHCIS_SQLSERVER configured. Each test-created database is uniquely named and cleaned up only after ownership-prefix/length validation.

Two development runs failed on a test-fixture expectation (expected two clinics but seed plus fixture create three). The expectation was corrected; no product defect was involved. One initial rebuild failed because the old demo process locked its binaries; that process was stopped before rebuilding.

## Identity and domain milestone

Release suite executed against SQL Server plus isolated SQLite: 36 passed, 0 failed, 0 skipped. Original 28 scenarios remain. Added real Identity registration/login, password policy/lockout, JWT signature/audience/expiry rejection, anonymous rejection, confirmation ownership, clinic/slot mismatch, and unexpected database-failure propagation. A test-only claim scheme keeps legacy booking scenarios focused; separate security tests use the actual JWT middleware.

Patient ownership derives from the validated subject, never a request patient ID. Identity uses its standard password hasher. Tokens last 15 minutes and are held only in client memory; development/test keys are random and ephemeral. Production requires externally configured random signing material. No staff/admin workflows or invented roles are added. A new SQLite demo filename preserves the older anonymous demo database.

One new failure-injection test initially severed an EF required navigation and failed before database execution; the fixture was corrected to exercise an actual foreign-key violation instead. It now verifies rollback and that an unexpected failure is not converted into 409.

## Browser milestone

Independent Playwright 1.64.1 with Chromium executed the Release app against the dedicated SQL Server database: 8 passed, 0 failed. This verifies real WASM execution, account creation, clinic/date/slot selection, invalid input correction, one POST on double click, confirmation/focus, availability refresh, two tabs yielding 201/409, stale responses, API recovery, mobile keyboard order/no horizontal overflow, JWT-expiry recovery, empty dates and no console/component-asset errors. Screenshots and a sanitized result summary are in docs/evidence.

Initial browser runs had a duplicate test locator, then exposed a real layout shift: blur removed validation messages during mousedown, moving the submit button before mouseup. The fix shows only inline booking errors, stops each property at its first validation error, and reserves message space. The regression now passes without adding a blur workaround. A narrow-layout sign-out wrap was fixed; theme colors now match the existing clinic branding. All three Mermaid diagrams rendered successfully and their PNGs were visually inspected.

Release suite was rerun after the validation change: 36 passed, 0 failed, 0 skipped; migration consistency check reports no pending changes.

## Published-host regression

A real published-app run caught an unresolved bootstrap fingerprint placeholder: HTML/API responded, but WASM never initialized. Adding static endpoints while retaining the legacy framework middleware exposed a second pipeline conflict (HTTP 500 in its framework branch). The source now uses a stable bootstrap alias and .NET 10 MapStaticAssets without that legacy branch. No generated files were patched. The hosted-assets regression asserts that the bootstrap alias returns 200 and index HTML has no unresolved placeholder.

After the fix: Release suite 36 passed/0 failed/0 skipped; Windows published application against SQL Server, Chromium 8 passed/0 failed. The earlier published startup test timed out and its remaining run was stopped for diagnosis; it was never counted as verified. Published WASM is now actually executed, not inferred from HTML delivery.

## Packaging baseline

The final SDK is pinned to 10.0.403 in global.json, GitHub Actions and Docker; runtime image is pinned to 10.0.14. NuGet and npm lock files are committed. Release publish succeeds. The final Docker image builds from locked dependencies and its runtime process uses non-root UID 1654. A restore-layer optimization initially omitted source inputs that influence Blazor implicit packages and failed NU1004; Docker now restores with the full source tree and a package-cache mount.

README now documents dedicated SQL setup, migrations, secure configuration, Identity/JWT limitations, demo startup, tests, published execution, container network/auth requirements and troubleshooting. GitHub Actions defines isolated SQL Server, Release tests/publish, Chromium and Docker verification. No remote repository was created and hosted CI execution is not claimed. Container browser and fresh-clone evidence will be added after execution.
