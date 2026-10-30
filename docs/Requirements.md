# Requirements and scope

## Stakeholders

| Stakeholder | Needs |
|---|---|
| Patient | Select a clinic and available time, provide contact details, receive clear confirmation. |
| Clinic staff | Reliable bookings, accurate availability, no double booking. Staff management screens are future scope. |
| System administrator | Repeatable setup, database integrity, diagnostics, and future identity/role management. |
| Assessor/developer | Readable architecture, documented decisions, diagrams, runnable setup, and meaningful tests. |

## Functional requirements

| ID | Requirement | First-version acceptance criterion |
|---|---|---|
| FR01 | List clinics | API and UI display seeded clinic names and addresses. |
| FR02 | Query availability by clinic/date | Only future, unbooked slots for the selected SAST calendar day are returned. |
| FR03 | Validate patient input | Full name <=120 characters, valid email <=254, contact number with 7-15 digits and up to 25 formatted characters, and non-empty slot ID are enforced in client and server. |
| FR04 | Book an appointment | A valid future slot creates a patient and appointment atomically. |
| FR05 | Prevent double bookings | A unique appointment-slot constraint permits one booking, including competing requests. The losing request receives HTTP 409. |
| FR06 | Confirm the booking | Confirmation shows reference, clinic/address, date, time and SAST only after successful persistence. |
| FR07 | Recover from errors | Invalid, missing, expired, and already-booked slots have distinct responses; the UI allows another selection. |
| FR08 | Cancellation and rescheduling | Identified from assessment examples; deferred pending identity, ownership and slot-release rules. |
| FR09 | Staff/administrator management | Identified stakeholder need; outside the first booking workflow. |

## Non-functional requirements

- **Integrity:** relational foreign keys, a unique clinic/start index, unique appointment/slot and reference indexes, and end-after-start constraint. Patient and appointment insertion is one transaction.
- **Security/privacy:** treat client input as untrusted; validate on the API; do not log patient fields or request bodies; keep credentials outside source. The demo has no identity or ownership controls and must use fictional data. Production requires authentication, authorization, patient ownership, retention/privacy policies and operational security review.
- **Maintainability:** separate UI, contracts/validation, HTTP endpoints, business rules and persistence. Use DI and an injectable TimeProvider; avoid artificial repository layers over EF Core.
- **Performance/scalability:** no-tracking read projections and clinic/time indexes. Availability queries cover one date and permit searches up to 90 days ahead. No capacity benchmark or scale guarantee is claimed; measure against agreed targets before production.
- **Usability/accessibility:** labels, keyboard-operable slot buttons, visible selected state, loading/empty/error states, responsive layout and focus on confirmation.
- **Reliability:** handle a stale availability list with a database constraint and recoverable conflict; reject expired slots using server time. A lost network response may leave a successful booking, which the UI explains before refreshing availability.
- **Testability:** deterministic clock, isolated relational test databases, API integration tests, race/rollback tests, and separate manual browser checks.

## Assumptions

1. Each clinic slot has capacity one. There are no providers, rooms, specialties, overlapping slot administration, or multi-resource capacity rules in this version.
2. All clinics use SAST (UTC+02:00). Storage and API properties use UTC; availability dates and UI display use SAST.
3. Demonstration slots last 30 minutes on weekdays, 08:00-14:00, over a rolling 14-day seed window. A 90-day query limit does not imply slots exist for all 90 days.
4. A patient row represents the submitted details for one booking. It is not a verified or deduplicated identity. Cross-clinic patient overlap rules require identity and are deferred.
5. Confirmation is on-screen. Email and SMS are optional enhancements and are not called.
6. SQL Server is the assessment persistence provider; SQLite is an explicit convenience for local demos and isolated tests. SQL Server-specific behavior still needs validation on SQL Server.
7. Page 2 is a technology overview. MudBlazor, FluentValidation, HttpClient, EF Core, Serilog, and xUnit are used. Authentication, AutoMapper, UI automation frameworks, Docker and CI are tracked as scope decisions rather than claimed as complete.

## Future work, in dependency order

1. Clarify required identity/roles; implement secure patient ownership and staff access.
2. Add cancellation and rescheduling with atomic slot release/reassignment and audit history.
3. Add clinic/staff schedule management and stable patient identity where required.
4. Validate SQL Server concurrency under load; add UI regression tests and CI.
5. Add reminders, calendar integration or real-time updates only if required.
