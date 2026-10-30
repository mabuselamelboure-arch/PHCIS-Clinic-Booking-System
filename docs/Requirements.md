# Requirements and assessed scope

Source: original three-page PHCIS assessment; detailed attribution and technology decisions are in AssessmentCompliance.md.

## Stakeholders

| Stakeholder | Need and implemented scope |
|---|---|
| Patient | Register/sign in, select a clinic/date/slot, provide booking details, receive an owned confirmation. |
| Clinic staff | Accurate availability and no duplicate booking. Staff management screens are outside the assessed implementation list. |
| System administrator | Reproducible configuration, migrations, diagnostics and deployment. No invented admin workflow or privileged demo account. |
| Assessor | Understand architecture, run from a clean checkout, inspect tests and evidence. |

## Functional requirements

| ID | Requirement / acceptance |
|---|---|
| FR01 | List fictional demonstration clinics with names and addresses. |
| FR02 | Return future, unbooked slots for the selected clinic and SAST day, with a 90-day query horizon. |
| FR03 | Enforce full name <=120 characters; email <=254 and valid format; phone 7-15 digits and <=25 formatted characters; non-empty slot. |
| FR04 | Persist patient details and appointment atomically; reject missing/expired slots and supplied clinic/slot mismatches. |
| FR05 | Database enforces one appointment per slot, including concurrent requests; loser receives HTTP 409. |
| FR06 | Return reference, clinic/address and UTC times after persistence; display SAST confirmation and refresh booked availability. |
| FR07 | Show loading, empty, validation, connection and conflict states; prevent duplicate UI submissions; ignore stale availability responses. |
| FR08 | Register/login using ASP.NET Core Identity; authenticate protected endpoints with short-lived JWTs. |
| FR09 | Derive patient ownership from validated JWT subject. Owners can retrieve their confirmation; other users receive 404. |
| FR10 | Identify cancellation/rescheduling as future requirements. The PDF gives these as illustrative analysis examples, not explicit implementation tasks. No cancellation is claimed. |

## Non-functional requirements

- Integrity: restricted foreign keys; unique clinic/start, appointment/slot and reference indexes; end-after-start constraint; transactional insert.
- Security: standard Identity password hashing/policy/lockout; JWT issuer/audience/signature/lifetime checks; account endpoint IP rate limits; no client-supplied owner; no patient request-body logging or committed credentials. HTTPS/HSTS outside Development; production hosting must terminate TLS and configure its secret store and hostname.
- Maintainability: client, contracts, endpoints, business service and EF persistence have distinct responsibilities. DI and injectable TimeProvider keep rules testable. Small explicit DTO mappings avoid unnecessary abstractions.
- Usability: labelled controls, selected slot state, stable inline-validation space, confirmation focus, keyboard order, responsive desktop/mobile layout and recoverable errors.
- Performance/scalability: indexed, no-tracking availability projections for one date. No load or scale guarantee is claimed.
- Reproducibility: pinned package versions and lock files, local EF tool, Release build/test/publish, browser tests, CI definition and non-root Docker packaging.

## Assumptions and limitations

All clinics use SAST (UTC+02:00); storage and API properties use UTC. Capacity is one per slot; no room/provider management or overlapping schedule authoring exists. Demo slots run weekdays 08:00-14:00 over the next 14 calendar days. A 90-day query limit does not guarantee seeded availability for 90 days.

A Patient row captures details for a booking and links to an Identity user. The user can have several booking-detail rows; email is not used as ownership proof. Legacy anonymous rows remain nullable and are not available through owned confirmation retrieval. Patient-overlap/deduplication rules are outside scope.

Only patient capabilities are implemented. Staff/admin stakeholders do not imply management features or unnecessary roles. No email verification, password recovery, refresh-token flow, MFA or immediate server-side JWT revocation is provided. Logout clears the in-memory token; tokens expire within 15 minutes. Reload requires sign-in again. Fictional data only; this is an assessment, not a production healthcare deployment.

Cancellation/rescheduling, reminders, calendar synchronization, SignalR, GraphQL and Dapper are excluded as documented in the compliance matrix. A lost booking response can leave a saved booking; the UI warns and refreshes availability. Request idempotency and appointment history UI are future enhancements.
