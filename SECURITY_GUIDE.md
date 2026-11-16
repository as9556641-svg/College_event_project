# VAP UNIVERSITY — Campus Event Hub
# Comprehensive Security Architecture & Operational Hardening Guide

This document defines the security posture, cryptographic controls, authorization mechanisms, and operational compliance policies implemented in **VAP UNIVERSITY's Campus Event Hub**.

---

## 1. Authentication Architecture

### 1.1 Multi-Tier ASP.NET Core Identity
- **Password Complexity:** Minimum 8 characters, requiring at least one uppercase letter, one lowercase letter, and one numeric digit.
- **Account Lockout:** Enforced on all accounts after **5 consecutive failed login attempts**, triggering a mandatory **15-minute lockout cooldown**.
- **Password Hashing:** Standard ASP.NET Core Identity PBKDF2 with HMAC-SHA256 and per-user unique salt.
- **Cookie Security:**
  - `HttpOnly = true` (Inaccessible to JavaScript; immunizes auth cookies against client-side script theft).
  - `SameSiteMode.Lax` (Protects against Cross-Site Request Forgery while allowing top-level navigation).
  - `CookieSecurePolicy.Always` in non-development environments (Enforces HTTPS-only cookie transmission).
  - Sliding expiration set to 8 hours with secure session revocation on explicit sign-out.

### 1.2 Google OAuth 2.0 Integration & Privilege Escalation Defense
- Configured conditionally in `Program.cs` only when valid external client credentials exist.
- **Strict Role Isolation Guard:** The external OAuth callback (`AccountController.ExternalLoginCallback`) is hardcoded to provision **only the `Student` role** (`UserRole.Student`) for new external accounts. Elevated roles (`SuperAdmin`, `Admin`, `Coordinator`) can **never** be provisioned or claimed via external social logins.
- Account linking strictly verifies account active status (`user.IsActive`) before associating external provider keys.

---

## 2. Authorization & Tenant Isolation Matrix

| Module / Area | SuperAdmin | Admin | Coordinator | Student | Anonymous |
|---|:---:|:---:|:---:|:---:|:---:|
| **Public Catalog & Home** | Full | Full | Full | Full | Read-Only |
| **Public Certificate Verification** | Verify | Verify | Verify | Verify | Verify |
| **Student Registrations & Tickets** | Read | Read | Scoped (Event) | Own Records | Blocked (401) |
| **Payment Checkout & Receipts** | Read | Read | Scoped (Event) | Own Records | Blocked (401) |
| **Live Attendance QR Scanning** | Yes | Yes | Scoped (Assigned Events Only) | No | Blocked (401) |
| **Post-Event Feedback Submission** | No | No | No | Attended Only | Blocked (401) |
| **Certificate PDF Download** | Yes | Yes | Scoped (Event) | Own Records | Blocked (401/403) |
| **Campus Analytics & Reports** | Full | Full | Scoped (Assigned Events Only) | Blocked (403) | Blocked (401) |
| **Event Approval Workflow** | Full | Full (Except Self-Created) | Blocked (403) | Blocked (403) | Blocked (401) |
| **Department Management** | Full | Read-Only | Blocked (403) | Blocked (403) | Blocked (401) |

### 2.1 Conflict of Interest Enforcement
Coordinators or administrators who submit events for institutional approval are programmatically blocked by the domain state machine (`EventManagementService.CanUserApproveEventAsync`) from reviewing or approving their own event submissions.

### 2.2 IDOR & Cross-Tenant Protection
- All student query endpoints (`PaymentService`, `TicketService`, `EventRegistrationService`, `CertificateService`, `NotificationService`) validate that `currentUser.Id == record.UserId`.
- Inconsistent IDs throw `UnauthorizedAccessException` or return `null/NotFound`, preventing Insecure Direct Object References (IDOR).

---

## 3. Payment & Financial Security

### 3.1 Razorpay Test Mode & Webhook Security
- **Sandbox Boundary:** Razorpay is maintained strictly in **Test Mode** (`rzp_test_...`). Live payment processing and real-money transactions are prohibited during testing and academic evaluation.
- **Cryptographic Signature Verification:** Payment callbacks verify the authenticity of transactions by computing an HMAC-SHA256 signature using the secret key and verifying it via constant-time comparison (`CryptographicOperations.FixedTimeEquals`).
- **Idempotency & Replay Protection:**
  - Unique database index `UX_Payments_TransactionId` prevents replaying the same transaction identifier.
  - Filtered unique index `UX_Payments_Registration_Success` guarantees that no registration can have more than one successful payment.
- **Zero Plaintext Secrets:** Payment secrets and webhook signatures are never written to database tables or exposed in HTML/client-side script.

---

## 4. Cryptographic QR Code & Certificate Security

### 4.1 Zero-PII QR Code Payloads
- QR matrices contain **zero Personally Identifiable Information (PII)** (no student names, email addresses, phone numbers, or academic records).
- Payload structure:
  ```json
  {"v":1,"t":"TKT-202611-ABC12345","r":"REG-0101-20261116-XYZ","e":101,"s":"64_HEX_HMAC_SHA256_SIGNATURE"}
  ```
- Validated via constant-time HMAC-SHA256 verification using `Tickets:SigningSecret`. Tampered payloads are rejected instantly by the scanner.

### 4.2 Private Certificate PDF Storage
- Certificate PDFs are generated server-side using QuestPDF and stored in `App_Data/Certificates/` strictly **outside `wwwroot`**.
- Private PDFs cannot be fetched via static file URLs. Downloads require authenticated requests through `Student/CertificatesController.cs` or staff controllers enforcing ownership.
- The public verification portal (`/Certificates/Verify/{id}`) verifies credential authenticity and displays issued metadata without exposing the downloadable PDF document.

---

## 5. Web Application Security Defenses

### 5.1 Cross-Site Request Forgery (CSRF)
- `AutoValidateAntiforgeryTokenAttribute` is registered globally in MVC filters, enforcing anti-forgery token verification on all POST/PUT/DELETE actions.
- The external webhook endpoint (`PaymentWebhookController`) explicitly declares `[IgnoreAntiforgeryToken]` to allow signed machine-to-machine HTTP requests from Razorpay.

### 5.2 Cross-Site Scripting (XSS)
- All user-submitted text fields (announcement titles/content, feedback reviews, comments) are defensively encoded using `WebUtility.HtmlEncode` and Razor HTML encoding prior to rendering.

### 5.3 CSV Formula Injection (DDE) Defense
Spreadsheet applications (Excel, LibreOffice Calc) execute formulas if cells begin with `=`, `+`, `-`, `@`, `\t`, `\r`, or `%`. `ReportExportService` intercepts and neutralizes these characters by prepending a single quote (`'`), ensuring exports cannot be weaponized.

### 5.4 HTTP Security Headers
Every HTTP response includes standard hardening headers:
```http
X-Content-Type-Options: nosniff
X-Frame-Options: SAMEORIGIN
Referrer-Policy: strict-origin-when-cross-origin
Permissions-Policy: camera=(self), microphone=(), geolocation=()
Content-Security-Policy: default-src 'self'; script-src 'self' 'unsafe-inline' 'unsafe-eval' https://cdn.jsdelivr.net https://checkout.razorpay.com; style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; font-src 'self' https://fonts.gstatic.com data:; img-src 'self' data: https://*.googleusercontent.com; connect-src 'self' https://api.razorpay.com wss: ws:; frame-src 'self' https://api.razorpay.com; object-src 'none'; base-uri 'self'; form-action 'self';
```

### 5.5 Built-In Rate Limiting
- **AuthPolicy:** Limits authentication actions (login, student signup, password reset) to **20 requests per minute per IP**, blocking brute-force attacks with HTTP `429 Too Many Requests`.
- **GeneralPolicy:** Bounded at **120 requests per minute per IP** for general API and page traffic.

### 5.6 Production Error Handling & Information Leakage Defense
- `UseExceptionHandler("/Home/Error")` and `UseStatusCodePagesWithReExecute("/Home/StatusCode", "?code={0}")` suppress internal exception stack traces.
- Error views show only an incident reference ID (`Activity.Current?.Id ?? HttpContext.TraceIdentifier`).
- Internal ASP.NET Core environment messages and technical exception logs are strictly isolated from the end-user.
