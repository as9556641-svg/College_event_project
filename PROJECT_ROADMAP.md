# VAP UNIVERSITY — Comprehensive Project Roadmap & Audit Ledger

**Project:** College Event and Workshop Registration System  
**Institution:** VAP UNIVERSITY  
**Workspace:** `E:\CollegeEventRegistration`  
**Framework:** .NET 10 LTS (ASP.NET Core MVC)  
**Database:** SQL Server 2025 Express (`CollegeEventRegistrationDb`)  
**ORM:** Entity Framework Core 10  
**Architectural Standard:** Clean Architecture  
**Last Updated:** November 14, 2026

---

## 1. Project-Wide Phase Implementation Audit Ledger

| Phase # | Phase Title | Required Functionality | Existing Implementation | Verified Functionality (Tested) | Missing Functionality / Identified Deficiencies | Current Status |
|:---:|:---|:---|:---|:---|:---|:---:|
| **01** | **Project Foundation & Setup** | .NET 10 Solution structure, Core/Infrastructure/Web separation, Git setup, NuGet packages, base entities, configuration. | Clean Architecture solution with Core, Infrastructure, and Web projects. Global usings, dependency injection, and Git configuration. | Verified solution compilation, clean folder architecture, database connection string to SQL Server 2025 Express. | None. Foundation fully established. | **COMPLETED** |
| **02** | **Database Architecture** | Complete relational schema, EF Core DbContext, entity relationships, seed data, initial migrations, soft deletes, concurrency tokens. | `ApplicationDbContext` with 16 entities, DbInitializer with roles, admin/coordinator/student users, departments, categories, events. | Verified EF Core database updates, table schemas, foreign key cascade behaviors, and RowVersion concurrency tokens. | None. Schema supports all 15 roadmap phases. | **COMPLETED** |
| **03** | **Authentication & Role Management** | ASP.NET Core Identity, multi-role security (SuperAdmin, Admin, Coordinator, Student), Login, Register, Forgot Password, Access Denied, Session handling. | `AccountController`, Identity services, `RegisterViewModel`, `LoginViewModel`, institutional VAP branding on auth screens. | Verified multi-role authentication, password hashing, role cookies, access control redirect, and campus branding. | Password reset token email dispatch (stubbed until Phase 13). | **COMPLETED** |
| **04** | **Role-Based Dashboards** | Dedicated database-backed dashboards for SuperAdmin, Admin, Coordinator, and Student with real metrics, audit logs, and navigation layout. | 4 role Area controllers (`/SuperAdmin`, `/Admin`, `/Coordinator`, `/Student`), `_DashboardLayout.cshtml`, `dashboard.css`, live stats. | Verified all 4 dashboards render with real SQL counts, responsive sidebar navigation, and empty-state handling. | Role-specific downstream actions wired as subsequent phases develop. | **COMPLETED** |
| **05** | **Event & Workshop Management** | Department CRUD, Category CRUD, Multi-Step Event Creation Wizard (Steps 1–6), Banner Upload Service with magic byte security, 6-state approval workflow, conflict-of-interest checks. | `DepartmentService`, `EventCategoryService`, `BannerUploadService`, `EventManagementService`, `AuditLogService`, SuperAdmin/Admin/Coordinator controllers, ViewModels, and Razor views. | Verified Department CRUD, duplicate code guards, active status toggles, deletion guards (free and linked states); Category CRUD, slug generation and stability; Event schedule validation; Approval workflow (Submit &rarr; Conflict-of-interest check &rarr; Mandatory rejection reason &rarr; Formal approval metadata &rarr; Publishing &rarr; Cancellation); Banner magic-byte validation (JPEG/PNG/WEBP &le;5MB) and spoofing defense; Audit logging across all entities; Identity role authorization and credential regression. Fixed and verified Defect 1 (EF Core ExecutionStrategy transaction retry) and Defect 2 (Coordinator state machine authorization restrictions). 41/41 automated tests passed. | None. All Phase 5 core objectives and service-level defects fully resolved and verified. | **COMPLETED (41/41 TESTS PASSED)** |
| **06** | **Student Event Discovery** | Dedicated student event discovery portal (`/Student/Events`), search bar, multi-criteria filtering (category, date, department, mode, fee, registration open), server-side pagination, sorting, event details view with chronological sessions and eligibility evaluation. | `EventsController` in Student area, `EventDiscoveryService`, `IEventDiscoveryService`, `StudentEventCatalogViewModel`, `StudentEventDetailsViewModel`, `_StudentPortalLayout.cshtml`, `discovery.css`. | Verified Published-only visibility rules (Draft, PendingApproval, Rejected, Cancelled strictly excluded and return 404), full-text search across Title/Description/Venue, multi-criteria filters, server-side pagination, chronological sessions, personalized student eligibility evaluation, regression clean. 25/25 automated tests passed. | None. All Phase 6 objectives fully implemented and verified. | **COMPLETED (25/25 TESTS PASSED)** |
| **07** | **Registration Management** | Student registration workflow, seat reservation concurrency lock, duplicate registration guard, waitlist queue, registration cancellation. | `EventRegistrationService`, `IEventRegistrationService`, `RegistrationsController` across Student/Coordinator/Admin areas, `RegistrationDtos`, `RowVersion` optimistic concurrency. | Verified student registration, duplicate prevention, unpublished/date/capacity/eligibility rejections, tenant isolation, coordinator scoping, state machine transitions, cancellation with seat release, concurrent race-condition safety. 49/49 automated tests passed. | None. All Phase 7 core objectives and security guards fully resolved and verified. | **COMPLETED (49/49 TESTS PASSED)** |
| **08** | **Payment Integration** | Payment gateway integration (Razorpay/Stripe test sandbox), fee calculation, transaction logging, payment webhook, receipt generation. | `Payment` entity and table exist in database. `Event.EventType` and `RegistrationFee` properties verified. | Entity mapping and database schema verified. | Gateway client integration, checkout initiation, signature verification, payment failure/refund handling. | **COMPLETED (59/59 TESTS PASSED)** |
| **09** | **QR Ticket Generation** | Unique QR code generation per registration, cryptographically signed payload, digital admission ticket view, printable PDF/pass. | `Ticket` entity and table exist in database with `TicketCode`, `QrCodeDataUrl`, `IssuedAt`. | Entity mapping and database schema verified. | QR generation library integration (e.g. QRCoder), digital ticket Razor view, PDF download service. | **COMPLETED (53/53 TESTS PASSED)** |
| **10** | **Attendance & Check-In** | Live scanner interface for coordinators, QR scanner via device camera, manual roll-number check-in, attendance timestamping, duplicate scan prevention. | `Attendance` entity and table exist in database. | Entity mapping and database schema verified. | HTML5 QR camera scanner view, attendance verification API endpoint, real-time checked-in count sync. | **COMPLETED (50/50 TESTS PASSED)** |
| **11** | **Feedback & Ratings** | Post-event student feedback form, 5-star rating, quantitative and qualitative questions, feedback aggregation, coordinator feedback report. | `EventFeedback` entity and table exist in database with unique constraint `IX_EventFeedbacks_RegistrationId`. | Entity mapping and database schema verified. | `FeedbackService`, `IFeedbackService`, `FeedbackDtos`, student submission workflow, coordinator rating analytics, admin campus ledger, 47/47 automated tests passed. | **COMPLETED (47/47 TESTS PASSED)** |
| **12** | **Automated Certificate Generation** | Certificate template designer, dynamic student name/event replacement, PDF certificate generation, certificate verification portal. | `CertificateTemplate` and `Certificate` entities exist in database with unique indexes on `RegistrationId`, `CertificateNumber`, and `VerificationToken`. | Entity mapping and database schema verified via EF Core migration `AddCertificateEntity`. | QuestPDF server-side A4 landscape rendering engine, `CertificateService`, public verification portal, student portal, coordinator/admin ledgers, 21/21 automated tests passed (commit `0774e5c`). | **COMPLETED (21/21 TESTS PASSED)** |
| **13** | **Notifications & Announcements** | Email notification service (SMTP/SendGrid), in-app notification center, event announcement broadcasts, registration confirmation emails. | `Notification` entity and table exist in database. | Entity mapping and database schema verified. | Background email dispatch service, notification dropdown partial in navbar, event broadcast composer for coordinators. | **PENDING** |
| **14** | **Analytics & Reporting** | Administrative reports, participation trends, revenue summaries, department engagement charts, export to Excel/CSV/PDF. | Metric queries exist in Admin and SuperAdmin controllers; schema tracks historical registrations and payments. | Database queries for department and event metrics verified. | Chart.js interactive visualizations, CSV/Excel export service, executive PDF attendance and revenue summary reports. | **PENDING** |
| **15** | **System Hardening & Launch** | Security audit, rate limiting, SQL injection defense, security headers (CSP, HSTS), Redis distributed cache (optional), performance benchmarking. | Antiforgery tokens enforced across all POST forms, magic byte file validation, soft-delete query filters, parameterized EF Core queries. | Verified CSRF protection, file upload guards, and role-based authorization policies. | Security header middleware, response compression, output caching, production deployment manifests. | **PENDING** |

---

## 2. Phase 5 Verification & Stabilization Findings

### 2.1 Implemented & Fully Verified (30/32 Tests Passed)
1. **Department Management:**
   - Create department with unique code validation.
   - Code uppercase normalization and duplicate code blocking (`VTDEPT` uniqueness guard).
   - Update department metadata (name, description, head of department).
   - Active status toggles (`ToggleStatusAsync`).
   - Clean-state deletion permitted.
   - **Deletion safeguard active:** Department deletion blocked with descriptive error when linked to events or users.
2. **Category Management:**
   - Create category with auto-slug generation.
   - Update category with slug stability preserved (prevents broken URLs).
   - Active status toggles.
3. **Event Governance & Approval State Machine:**
   - Schedule validation enforces `RegistrationStartDate < RegistrationEndDate <= EventStartDate <= EventEndDate`.
   - Submission for approval transition (`Draft` &rarr; `PendingApproval`).
   - **Conflict-of-interest enforcement:** Coordinators and event creators are blocked from approving or reviewing their own events.
   - **Mandatory rejection reason:** Administrators cannot reject without specifying clear feedback.
   - Rejection feedback recorded in `Event.RejectionReason` with UTC timestamp.
   - Resubmission workflow permitted for `Rejected` events back to `PendingApproval`.
   - Formal administrative approval saves `ApprovedByUserId`, `ApprovedAt`, clears prior rejection reason, and transitions to `Approved`.
   - Publishing transition (`Approved` &rarr; `Published`).
   - Event cancellation workflow with cancellation audit note (`Status = Cancelled`).
4. **Banner Security & Magic Byte Inspection:**
   - Valid JPEG magic bytes (`FF D8 FF E0`) verified and saved with unique GUID filenames to `/uploads/banners/`.
   - Spoofed files (malicious text disguised with `.jpg` extension) blocked by binary signature verification.
   - Disallowed extensions (`.exe`, `.txt`) blocked by whitelist guard.
   - File size limit (&le; 5 MB) enforced.
5. **Audit Logging:**
   - Complete audit trail persisted in `AuditLogs` for department creation/updates, category actions, event approvals, and rejections.
6. **Regression & Security:**
   - Role authorization across all 4 roles (`SuperAdmin`, `Admin`, `Coordinator`, `Student`).
   - Password hash verification and authentication regression clean.
   - Live aggregate dashboard telemetry queries verified without SQL syntax errors.

### 2.2 Resolved Service-Level Defects (Fully Repaired & Verified)
1. **EF Core Execution Strategy Conflict with User-Initiated Transactions:**
   - **Location:** `src/CollegeEventRegistration.Infrastructure/Services/EventManagementService.cs` (`CreateEventAsync` & `UpdateEventAsync`).
   - **Fix Applied:** Wrapped transactional operations inside `var strategy = _context.Database.CreateExecutionStrategy(); await strategy.ExecuteAsync(...)`. Inside the delegate, user-initiated transactions are created, executed, committed, and rolled back safely with `_context.ChangeTracker.Clear()` to guarantee zero duplicate child records on retries.
   - **Verification:** Verified event creation with sessions/coordinators, event updates with session replacements, transaction rollbacks, and retry idempotency.
2. **Coordinator Edit Status Machine Authorization in Service Layer:**
   - **Location:** `src/CollegeEventRegistration.Infrastructure/Services/EventManagementService.cs` (`CanUserEditEventAsync`).
   - **Fix Applied:** Enforced strict status machine check: `if (!isElevatedAdmin && evt.Status != EventStatus.Draft && evt.Status != EventStatus.Rejected) return false;`.
   - **Verification:** Verified that coordinators can edit `Draft` and `Rejected` events, but are blocked from editing `PendingApproval`, `Approved`, `Published`, and `Cancelled` events. Elevated Admin/SuperAdmin permissions remain fully intact across all states.

---

## 3. Phase 6 Student Event Discovery Portal Findings

### 3.1 Implemented & Fully Verified (25/25 Tests Passed)
1. **Dedicated Student Discovery Portal (`/Student/Events`):**
   - High-impact hero section with VAP UNIVERSITY & Campus Event Hub identity.
   - Quick category pills displaying real-time published event counts.
   - Direct integration with Student profile and secure POST sign-out.
2. **Server-Side Search & Multi-Faceted Filtering:**
   - Full-text search across Title, ShortDescription, Detailed Description, and Venue.
   - Category filtering (`CategoryId`) with slug support.
   - Department filtering (`DepartmentId`).
   - Mode filtering (`Offline`, `Online`, `Hybrid`).
   - Fee structure filtering (`Free`, `Paid`).
   - Date range filters (`today`, `this_week`, `this_month`, and custom `FromDate`/`ToDate`).
   - "Registration Currently Open" boolean filter.
3. **Sorting & Server-Side Pagination:**
   - Sort criteria: `upcoming` (default), `latest`, `date_asc`, `date_desc`, `title_asc`, `title_desc`.
   - Server-side pagination via `IQueryable` (Count + Skip + Take) with total count and page guards.
   - Preservation of all active query parameters across pagination links.
   - Accessible empty-state presentation with quick "Reset All Filters" trigger.
4. **Strict Server-Side Visibility Enforcement:**
   - Only `Status == EventStatus.Published` and `!IsDeleted` events are queryable.
   - Direct URL access to `Draft`, `PendingApproval`, `Rejected`, or `Cancelled` events returns `404 Not Found`.
   - Internal administrative audit details (`RejectionReason`, `ApprovedByUserId`) are strictly sanitized before delivery.
5. **Comprehensive Event Details (`/Student/Events/Details/{id}`):**
   - Event hero banner, category/mode/department badges, and full curriculum description.
   - Chronologically ordered session schedule (`SessionDate`, then `StartTime`) with speaker affiliations.
   - Departmental, year of study, and inter-college eligibility evaluation with personalized status indicators.
   - Quick facts card (IST dates via `TimezoneHelper`, mode, venue, and fee).
   - Informational Phase 7 registration callout with disabled button (preserving strict Phase 7 boundary).

---

## 4. Git Commit Ledger

- **Commit 1 (Institutional Identity):** `08791e3` — `feat(branding): update institutional identity to VAP University`
- **Commit 2 (Phase 5 Events Management):** `a2a4b4b` — `feat(events): complete Phase 5 Event and Workshop Management`
- **Commit 3 (Phase 6 Student Event Discovery):** `bf6513a` (incorporated into Phase 6 commit) — `feat(events): implement student event discovery portal`
- **Commit 4 (Phase 7 Registration Management):** `bc6509d` — `feat(registration): implement Phase 7 student event registration and management`
- **Commit 5 (Phase 8 Payment Integration):** `abcf878` — `feat(payment): implement Phase 8 payment gateway integration and financial ledger`
- **Commit 6 (Phase 9 QR Ticket Generation):** `ed2b967` — `feat(tickets): implement Phase 9 secure QR ticket generation and validation`
- **Commit 7 (Phase 10 Attendance Management):** `e0e58f7` — `feat(attendance): implement Phase 10 attendance management, live QR scanner, and duplicate check-in protection`

---

## 5. Phase 8 Implementation & Verification Summary

### Deliverables:
1. **Razorpay Payment Gateway Integration:**
   - Production-ready `IPaymentGatewayClient` with HMAC-SHA256 signature verification (`CryptographicOperations.FixedTimeEquals`), REST order generation, and offline/test sandbox reliability.
   - User Secrets configuration (`Razorpay:KeyId`, `Razorpay:KeySecret`, `Razorpay:WebhookSecret`, `Razorpay:IsTestMode`).
2. **Atomic Verification & Seat Reservation:**
   - Free events register directly as `Confirmed` and allocate seats immediately.
   - Paid events initialize in `Pending` without taking seats until verified successful payment.
   - Atomic transaction with execution strategy locks and increments `RegisteredCount` upon verified payment.
3. **Financial Security & Anti-Fraud:**
   - Server-side amount and currency validation in paise (`Math.Round(amount * 100)`).
   - Replay attack defense using unique filtered index `UX_Payments_TransactionId`.
   - Single successful payment invariant enforced by `UX_Payments_Registration_Success`.
   - Webhook endpoint (`/api/payments/razorpay-webhook`) with cryptographic signature check and idempotent re-delivery handling.
4. **Student Experience:**
   - Checkout page (`/Student/Payments/Checkout/{id}`) with fee breakdown.
   - Official printable receipt (`/Student/Payments/Receipt/{id}`).
   - Student payment history ledger (`/Student/Payments`).
   - Dynamic payment status callout and "Pay Now" button on registration details.
5. **Staff Portals:**
   - Admin Master Payments Ledger (`/Admin/Payments`) with multi-faceted filtering.
   - Coordinator Event Payments Ledger (`/Coordinator/Payments`) with strict event-ownership scoping.
6. **Automated Verification:**
   - 59/59 automated tests in `Phase8Verifier` passed.
   - All 145 regression tests passed across Phase 1, Phase 2, Google OAuth, Phase 6, and Phase 7.

---

## 6. Phase 9 Implementation & Verification Summary

### Deliverables:
1. **Cryptographic QR Code Generator (`IQrCodeGenerator`, `QrCodeGenerator`):**
   - High-performance, memory-efficient QR matrix generation using `QRCoder` 1.8.1 with `PngByteQRCode` (0 byte temporary disk writes, fully server-side).
   - Tamper-proof, cryptographically signed payload format using HMAC-SHA256 (`Tickets:SigningSecret`).
   - Strict PII-free payload: `{"v":1,"t":"TKT-...","r":"REG-...","e":123,"s":"..."}`. Zero student personal data (no name, email, phone) embedded in QR codes.
   - Timing-attack-resistant constant-time signature verification via `CryptographicOperations.FixedTimeEquals`.
2. **Domain-Enforced Ticket Service (`ITicketService`, `TicketService`):**
   - Eligibility enforcement strictly on the server: Confirmed free events and Confirmed paid events (after verified payment) are issued tickets.
   - Pending, Waitlisted, Cancelled, and Unpaid paid-event registrations are strictly rejected with comprehensive domain reason codes.
   - 1-to-1 database invariant guaranteed by existing unique index `IX_Tickets_RegistrationId`.
   - Cryptographically collision-resistant ticket codes formatted as `TKT-YYYYMM-XXXXXXXX` enforced by unique index `IX_Tickets_TicketCode`.
   - Automatic ticket invalidation: when student or staff cancels an event registration via `EventRegistrationService`, the associated ticket is immediately deactivated (`IsActive = false`) and marked invalid.
3. **Student Experience:**
   - My Tickets view (`/Student/Tickets`) with interactive admission cards and QR code previews.
   - Ticket Details view (`/Student/Tickets/Details/{id}`) displaying official admission pass, verification hash, and status badge.
   - Printable Admission Pass (`/Student/Tickets/Print/{id}`) with clean, high-contrast `@media print` styling, tear-off admission stub, and verified QR symbol.
   - Direct ticket access seamlessly integrated into Registration Details (`/Student/Registrations/Details/{id}`).
4. **Staff Portals:**
   - Admin Master Tickets Ledger (`/Admin/Tickets`) with cross-campus searching, filtering by event, status, and registration reference, plus audit inspection (`/Admin/Tickets/Details/{id}`).
   - Coordinator Event Tickets Portal (`/Coordinator/Tickets`) with strict event-ownership scoping, event selector dropdown, and ticket inspection (`/Coordinator/Tickets/Details/{id}`).
5. **Navigation & Menus:**
   - "My Tickets" added to Student Portal navigation bar, profile dropdown, and dashboard sidebar.
   - "Event Tickets" added to Coordinator dashboard sidebar.
   - "Tickets Ledger" added to Admin dashboard sidebar.
6. **Automated Verification:**
   - 53/53 automated tests in `Phase9Verifier` passed (100% clean across all 15 scenarios).
   - 204 regression tests passed across Phase 1 (28), Phase 2 (24), Google OAuth (19), Phase 6 (25), Phase 7 (49), and Phase 8 (59).
   - Total: 257 / 257 passing automated tests with 0 failures and 0 warnings.

---

## 7. Phase 10 Implementation & Verification Summary

### Deliverables:
1. **Domain-Enforced Attendance Service (`IAttendanceService`, `AttendanceService`):**
   - Server-side cryptographic QR verification reusing Phase 9 signed payloads and constant-time HMAC validation.
   - Strict eligibility enforcement: Confirmed status, verified payment completeness, and refund rejection.
   - Strict Coordinator event-scoping: Coordinators can only scan or view attendance for events they are assigned to manage. Unassigned events are rejected with `UnauthorizedStaff` / `UnauthorizedAccessException`.
   - Admin campus-wide management authority verified.
   - Duplicate check-in protection enforced by unique index `IX_Attendances_RegistrationId` and transaction execution strategy.
   - Concurrency race-condition defense verified: concurrent scans result in exactly 1 successful admission and 4 duplicate rejections.
   - Full manual check-in workflow with mandatory reason/justification logging for audit compliance.
2. **QR Scanner Interface — Laptop & Mobile:**
   - Fully client-side, offline-capable scanner using `html5-qrcode` 2.3.8 (`wwwroot/lib/html5-qrcode.min.js`).
   - Supports laptop webcams and mobile rear cameras (`facingMode: "environment"`).
   - Camera enumeration and runtime switching dropdown.
   - Graceful camera permission denied and camera unavailable handling with clear user alerts.
   - Scan debouncing and cooling (prevents repeat requests from camera frames).
   - Real-time admission feedback cards (green success vs amber/red duplicate/tamper rejection).
   - Session scan ledger tracking recent attendees.
3. **Staff Attendance Dashboards:**
   - Coordinator Attendance Dashboard (`/Coordinator/Attendance?eventId={id}`): Event metrics, attendance table with search and filtering, modal for manual check-in, and direct launcher for live QR scanner.
   - Coordinator Live QR Scanner (`/Coordinator/Attendance/Scanner?eventId={id}`): Full-screen camera scanner with live stats updates.
   - Admin Master Attendance Ledger (`/Admin/Attendance`): Cross-campus overview, multi-faceted filtering (Event, Department, Method, Search), and detailed attendance audit view (`/Admin/Attendance/Details/{id}`).
4. **Student Experience:**
   - Official "Admitted & Checked In" badge displayed on Admission Pass (`/Student/Tickets/Details/{id}`).
   - "Attendance Verified & Recorded" callout displayed on Registration Details (`/Student/Registrations/Details/{id}`).
5. **Automated Verification:**
   - 50/50 automated tests in `Phase10Verifier` passed cleanly across all 21 target scenarios.
   - 257 regression tests passed across Phase 1 (28), Phase 2 (24), Google OAuth (19), Phase 6 (25), Phase 7 (49), Phase 8 (59), and Phase 9 (53).
   - Total: 307 / 307 passing automated tests with 0 failures and 0 warnings.

---

## 8. Phase 11 Implementation & Verification Summary

- **Git Commit:** `73869f2` (`feat(feedback): implement Phase 11 post-event feedback, rating aggregation, and coordinator analytics`)

### Deliverables:
1. **Domain-Enforced Feedback Service (`IFeedbackService`, `FeedbackService`):**
   - Server-side post-event evaluation validation requiring:
     - Confirmed registration status.
     - Verified attendance record (`Attendance != null && Attendance.Status == AttendanceStatus.CheckedIn`).
     - Event conclusion requirement (`EventEndDate <= DateTime.UtcNow || Event.Status == EventStatus.Completed`).
   - Anti-tampering & Student Tenant Isolation: Students can only submit and view evaluations for their own registrations (`reg.UserId == studentUserId`).
   - Multi-metric quantitative rating scale (1 to 5 stars for Overall Experience, Content Quality, Speaker Effectiveness, Venue & Logistics).
   - Qualitative review capture with length boundary enforcement (max 1,000 characters) and stored XSS defense.
   - Anonymity option: Participants can submit feedback anonymously to public and coordinator views; student name and roll number are masked.
   - Duplicate submission protection enforced by application logic and unique database constraint `IX_EventFeedbacks_RegistrationId`.
   - Concurrency race-condition safety guaranteed via execution strategy: simultaneous duplicate submissions result in exactly 1 persisted record and safe rejections.
   - Event-level Coordinator Scoping: Coordinators are strictly confined to feedback for their assigned events; unauthorized access throws `UnauthorizedAccessException`.
   - Admin cross-campus governance: Full institutional visibility across all departments and events.
   - Audit logging: Submissions recorded in `AuditLogs` (`SubmitEventFeedback`).
2. **Student Portal Feedback Experience:**
   - Dedicated feedback index (`/Student/Feedback`): Displays available attended events awaiting review, completed reviews, and status badges.
   - Interactive feedback submission form (`/Student/Feedback/Submit?registrationId={id}`): Star rating selectors, dimensional assessment, character-counted comments box, and anonymity toggle.
   - Submitted feedback details view (`/Student/Feedback/Details?registrationId={id}`).
   - Integration on Registration Pass view (`/Student/Registrations/Details/{id}`): Post-event feedback card prompts attended students to share feedback once the event has concluded.
3. **Coordinator Quality Analytics Portal:**
   - Coordinator Feedback Dashboard (`/Coordinator/Feedback?eventId={id}`): Event switcher, satisfaction averages (Overall, Content, Speaker, Venue), 5-star histogram distribution, and participant review comments ledger.
   - Individual feedback inspection view (`/Coordinator/Feedback/Details/{id}`).
4. **Admin Governance Feedback Ledger:**
   - Admin Master Feedback Ledger (`/Admin/Feedback`): Campus-wide overview, average campus satisfaction, feedback participation rate, and multi-criteria filters (Department, Event, Min Rating, Search Term).
   - Administrative feedback detail audit view (`/Admin/Feedback/Details/{id}`).
5. **Navigation & Shared Shell:**
   - Added active navigation links for "My Feedback", "Event Feedback", and "Feedback & Ratings" across `_DashboardLayout.cshtml` and `_StudentPortalLayout.cshtml`.
6. **Automated Verification:**
   - 47/47 automated tests in `Phase11Verifier` passed cleanly across all 18 target scenarios.
   - 307 regression tests passed across Phase 1 (28), Phase 2 (24), Google OAuth (19), Phase 6 (25), Phase 7 (49), Phase 8 (59), Phase 9 (53), and Phase 10 (50).
   - Total: 354 / 354 passing automated tests with 0 failures and 0 warnings.

---

## 9. Phase 12 Completion Summary — Automated Certificate Generation

1. **Database Schema & Migrations:**
   - Entity `Certificate` created in `CollegeEventRegistration.Core.Entities` with 1-to-1 unique mapping to `EventRegistration`, foreign keys to `Event`, `User`, `IssuedByUser`, `RevokedByUser`, and `CertificateStatus` enum (`Active = 1, Revoked = 2`).
   - Unique constraints: `IX_Certificates_RegistrationId`, `IX_Certificates_CertificateNumber`, and `IX_Certificates_VerificationToken`.
   - Migration `20261115161539_AddCertificateEntity` applied cleanly to SQL Server Express `CollegeEventRegistrationDb`.
2. **Server-Side PDF Rendering Engine (`PdfCertificateGenerator`):**
   - Implemented high-fidelity institutional A4 landscape PDF certificate generator using `QuestPDF` Community License.
   - Embeds institutional branding, VAP UNIVERSITY header, dynamic student name, roll number, event title, department, dates, certificate number, verification token, authorized signatory, and verification QR code generated via `QRCoder`.
   - Anti-traversal security enforced on all file paths in `wwwroot/certificates/{year}/`.
3. **Domain Service (`CertificateService`, `ICertificateService`):**
   - Strict eligibility enforcement: Confirmed registration, CheckedIn attendance, concluded event (`EventEndDate <= UtcNow`), successful payment if paid event, not cancelled/refunded.
   - Non-guessable certificate number generation (`VAP-CERT-{Year}-{EventId}-{RandomAlpha}`) and cryptographic verification token.
   - Idempotent issuance and concurrency race-condition handling via unique index fallback.
   - Batch issuance for event coordinators and campus administrators.
   - Revocation workflow requiring mandatory reason, recording revoker and timestamp, with immutable audit logging.
4. **Public Verification Portal (`/Certificates/Verify` and `/Certificates/Verify/{id}`):**
   - Accessible without authentication. Validates authenticity, active/revoked status, and event details while masking student PII.
5. **Student Portal (`/Student/Certificates`):**
   - List eligible & issued certificates, claim certificates for completed events, view certificate details, download genuine PDF, and print.
   - Certificate claim callout card added to registration details page (`/Student/Registrations/Details/{id}`).
6. **Coordinator Portal (`/Coordinator/Certificates`):**
   - Scoped strictly to coordinator-assigned events (`UnauthorizedAccessException` on unassigned events).
   - Batch certificate issuance and template configuration (`/Coordinator/Certificates/Template/{eventId}`).
7. **Admin Master Ledger (`/Admin/Certificates`):**
   - Campus-wide certificates ledger, department and event filters, manual issuance, revocation with mandatory reason, and template configuration.
8. **Navigation & Shared Shell:**
   - Activated "My Certificates" in `_StudentPortalLayout.cshtml` and `_DashboardLayout.cshtml`.
   - Activated "Certificates" for Coordinator and Admin roles in `_DashboardLayout.cshtml`.
9. **Automated Verification & Regression:**
   - 21/21 automated tests in `Phase12Verifier` passed cleanly with 100% compliance.
   - 428 regression tests passed across Phase 1 (28), Phase 2 (24), Public Website (33), Google OAuth (19), Phase 5 (41), Phase 6 (25), Phase 7 (49), Phase 8 (59), Phase 9 (53), Phase 10 (50), and Phase 11 (47).
   - Total: 449 / 449 passing automated tests with 0 failures and 0 warnings.

---

## 10. Strict Boundary Rules for Next Phase

- **Current Status:** Phase 12 is **COMPLETED & VERIFIED (21/21 TESTS PASSED | 449/449 FULL REGRESSION SUITE PASSED)**.
- **Strict Boundary:** No notifications (Phase 13), reports/analytics (Phase 14), or system hardening (Phase 15) was implemented in Phase 12.
- **Rule:** Phase 13 (Notifications & Announcements) will only be commenced upon explicit user instruction, maintaining step-by-step verified architectural integrity.
