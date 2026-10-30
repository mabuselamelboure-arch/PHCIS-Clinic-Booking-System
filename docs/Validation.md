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
