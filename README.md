# VAP UNIVERSITY — Campus Event Hub
## College Event & Workshop Registration System

Enterprise-grade event governance, student discovery, seat reservation, payment processing, cryptographic QR ticketing, live attendance check-in, automated certificates, notifications, and analytics platform for higher-education institutions.

Built with **.NET 10 LTS (ASP.NET Core MVC)**, **Entity Framework Core 10**, and **Microsoft SQL Server 2025 Express**, strictly conforming to **Clean Architecture**.

---

## 1. Key System Capabilities

- **Institutional Identity & Portals:** Dedicated web portals for Students, Event Coordinators, Faculty Administrators, and System Super Administrators.
- **Event Lifecycle Governance:** 6-stage wizard for event creation with multi-session schedules, speakers, department tags, categories, conflict-of-interest checks, and formal administrative approval workflows.
- **Discovery & Discovery Catalog:** Full-text search across titles, descriptions, and venues, multi-faceted filtering (date presets, department, category, mode, fee), server-side pagination, and personalized student eligibility evaluation.
- **Seat Reservation & Concurrency:** Atomic database seat reservation with optimistic concurrency (`RowVersion`), capacity limits, and waitlist auto-promotion.
- **Payment Processing (Test Sandbox):** Integrated Razorpay Test Sandbox with server-to-server HMAC-SHA256 signature verification, replay protection, and official printable tax receipts.
- **Cryptographic QR Tickets:** Low-overhead QRCoder generation of tamper-proof admission passes using HMAC-SHA256 signatures with zero PII exposure in the QR matrix.
- **Live Attendance & Scanner:** Browser-based HTML5 QR camera scanner (laptop webcam and mobile rear cameras), duplicate check-in protection, and manual roll number check-in with mandatory audit justification.
- **Post-Event Feedback & Ratings:** 5-star multi-dimensional surveys (overall, content, speaker, venue) with optional anonymity and coordinator rating histograms.
- **Automated Certificate Generation:** High-fidelity A4 landscape PDF certificates rendered server-side via QuestPDF, stored in secure private directory, with a public authenticity verification portal.
- **Notifications & Broadcasts:** In-app notification center, SignalR real-time web push, background email queue with graceful SMTP fallback, and targeted audience announcements.
- **Analytics & Reporting:** Campus-wide analytics dashboard, event-scoped coordinator metrics, Chart.js visualizations, RFC 4180 CSV exports with formula injection (DDE) defenses, and downloadable Executive Summary PDFs.
- **Security Hardening:** Anti-forgery validation (`AutoValidateAntiforgeryTokenAttribute`), Content Security Policy (CSP), rate limiting (`Microsoft.AspNetCore.RateLimiting`), health checks (`/health`), account lockout, and branded error status pages.

---

## 2. Technology Stack

- **Framework:** .NET 10.0 LTS (C# 14 / ASP.NET Core MVC)
- **Database:** Microsoft SQL Server 2025 Express (`CollegeEventRegistrationDb`)
- **ORM:** Entity Framework Core 10 (Code-First Migrations, ExecutionStrategy, AsNoTracking queries)
- **Identity & Security:** ASP.NET Core Identity, PBKDF2 Password Hashing, Google OAuth 2.0
- **Document & Media Generation:** QuestPDF 2025 (Community License), QRCoder 1.8.1
- **Real-Time Push:** ASP.NET Core SignalR
- **Frontend Assets:** Bootstrap 5.3, Chart.js 4.4, html5-qrcode 2.3.8, jQuery 3.7

---

## 3. Solution Structure (Clean Architecture)

```text
E:\CollegeEventRegistration\
├── src\
│   ├── CollegeEventRegistration.Core\           # Domain Layer (Zero external dependencies)
│   │   ├── Common\ (BaseEntity, PagedResult)
│   │   ├── DTOs\ (Analytics, Attendance, Certificates, Events, Feedback, Payments, Registration)
│   │   ├── Entities\ (Event, EventRegistration, Payment, Ticket, Attendance, Certificate, etc.)
│   │   ├── Enums\ (UserRole, EventStatus, RegistrationStatus, PaymentStatus, etc.)
│   │   └── Interfaces\ (Core service contracts and abstractions)
│   │
│   ├── CollegeEventRegistration.Infrastructure\ # Data Access, Identity & Integrations
│   │   ├── Configuration\ (EmailSettings, RazorpaySettings)
│   │   ├── Data\ (ApplicationDbContext, DbInitializer)
│   │   ├── Identity\ (ApplicationUser, ApplicationRole, SuperAdminProtection)
│   │   ├── Migrations\ (EF Core migration history)
│   │   └── Services\ (Analytics, Attendance, Certificates, Emails, Payments, Tickets, etc.)
│   │
│   └── CollegeEventRegistration.Web\            # Presentation Layer (ASP.NET Core MVC)
│       ├── Areas\ (Admin, Coordinator, Student, SuperAdmin)
│       ├── Controllers\ (Account, Certificates, Home, Api/PaymentWebhook)
│       ├── Hubs\ (NotificationHub)
│       ├── Services\ (DatabaseHealthCheck, SignalRNotificationPublisher)
│       ├── Views\ (Razor views, status code pages, layouts)
│       └── wwwroot\ (CSS, JS, branding assets, static libraries)
│
├── CollegeEventRegistration.slnx                # Modern Visual Studio / .NET Solution
├── PROJECT_ROADMAP.md                           # Verification ledger & roadmap
├── DEPLOYMENT_GUIDE.md                          # Staging and production deployment manual
├── SECURITY_GUIDE.md                            # Comprehensive security & secrets manual
└── SYSTEM_ARCHITECTURE.md                       # Architectural design & data flow document
```

---

## 4. Prerequisites

1. **.NET 10.0 SDK** (v10.0.404 or newer)
2. **Microsoft SQL Server 2025 Express** running locally at `localhost\SQLEXPRESS`
3. Modern Web Browser (Chrome, Edge, Firefox)

---

## 5. Local Setup & Configuration

### 5.1 Database Setup & Migrations
Ensure SQL Server Express is running. The database connection string defaults to Windows Authentication (`Trusted_Connection=True;TrustServerCertificate=True`):

```powershell
# From repository root:
dotnet ef database update --project src/CollegeEventRegistration.Infrastructure --startup-project src/CollegeEventRegistration.Web
```

### 5.2 User Secrets Configuration
Sensitive credentials (such as Google OAuth client secrets, Razorpay test keys, and SMTP credentials) must never be committed to source control. Configure them using .NET User Secrets:

```powershell
cd src/CollegeEventRegistration.Web

# Razorpay Test Sandbox Keys
dotnet user-secrets set "Razorpay:KeyId" "rzp_test_YOUR_KEY_ID"
dotnet user-secrets set "Razorpay:KeySecret" "YOUR_KEY_SECRET"
dotnet user-secrets set "Razorpay:WebhookSecret" "YOUR_WEBHOOK_SECRET"

# Cryptographic QR Ticket Signing Key
dotnet user-secrets set "Tickets:SigningSecret" "YOUR_SECURE_HMAC_SECRET_AT_LEAST_32_CHARS"

# Google OAuth 2.0 (Optional)
dotnet user-secrets set "Authentication:Google:ClientId" "YOUR_GOOGLE_CLIENT_ID"
dotnet user-secrets set "Authentication:Google:ClientSecret" "YOUR_GOOGLE_CLIENT_SECRET"

# SMTP Email Dispatch (Optional)
dotnet user-secrets set "EmailSettings:SmtpHost" "smtp.gmail.com"
dotnet user-secrets set "EmailSettings:SmtpPort" "587"
dotnet user-secrets set "EmailSettings:SmtpUsername" "notifications@vap.edu"
dotnet user-secrets set "EmailSettings:SmtpPassword" "YOUR_APP_PASSWORD"
```

---

## 6. Running the Application

```powershell
dotnet run --project src/CollegeEventRegistration.Web/CollegeEventRegistration.Web.csproj
```

Open your browser and navigate to:
- **Public Homepage:** `http://localhost:5000` or `https://localhost:5001`
- **Health Check Endpoint:** `http://localhost:5000/health`
- **Certificate Verification:** `http://localhost:5000/Certificates/Verify`

### Default Seed Accounts
| Role | Email | Password |
|---|---|---|
| **SuperAdmin** | `superadmin@college.edu` | `SuperAdmin@123` |
| **Admin** | `admin@college.edu` | `Admin@123` |
| **Coordinator** | `coordinator@college.edu` | `Coordinator@123` |
| **Student** | `student@college.edu` | `Student@123` |

---

## 7. Testing & Verification

The solution includes automated verifiers for each roadmap milestone. All 15 verifiers can be run independently:

```powershell
# Run Phase 15 System Hardening & Launch Readiness Verifier (58 checks):
dotnet run --project scratch/Phase15Verifier/Phase15Verifier.csproj

# Run Complete Solution Release Build:
dotnet build CollegeEventRegistration.slnx -c Release
```

---

## 8. License & Governance
Developed for **VAP UNIVERSITY**. All institutional rights reserved. QuestPDF Community License compliant.
