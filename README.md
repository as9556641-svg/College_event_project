# College Event & Workshop Registration System

Enterprise-grade event management, student registration, QR attendance check-in, and certificate issuance platform for universities and higher-education institutions.

Built with **.NET 10.0 LTS**, **ASP.NET Core MVC**, **Entity Framework Core 10**, and **Microsoft SQL Server 2025 Express**.

---

## 1. Solution Architecture

The application is structured following **Clean Architecture** principles to enforce loose coupling, high maintainability, and domain independence:

```text
E:\CollegeEventRegistration\
├── src\
│   ├── CollegeEventRegistration.Core\           # Domain Layer (Zero external dependencies)
│   │   ├── Common\ (BaseEntity)
│   │   ├── Entities\ (ApplicationUser, ApplicationRole)
│   │   └── Enums\ (UserRole: SuperAdmin, Admin, Coordinator, Student)
│   │
│   ├── CollegeEventRegistration.Infrastructure\ # Data Access & External Services
│   │   ├── Data\ (ApplicationDbContext, Identity configuration)
│   │   └── DependencyInjection.cs (EF Core & SQL Server registration)
│   │
│   └── CollegeEventRegistration.Web\            # Presentation Layer (ASP.NET Core 10 MVC)
│       ├── Controllers\
│       ├── Views\
│       ├── wwwroot\ (3D-inspired CSS system, Bootstrap 5.3, assets)
│       ├── Program.cs
│       └── appsettings.Development.json
│
├── CollegeEventRegistration.sln
├── .gitignore
├── README.md
└── UI_DESIGN_GUIDELINES.md                      # Permanent UI Design System specs
```

### Dependency Flow:
- `Core` is independent of database engines, Web frameworks, and external APIs.
- `Infrastructure` references `Core` and implements data access and third-party integrations.
- `Web` references `Infrastructure` and `Core`, serving as the composition root and user interface.

---

## 2. Role-Based Access Control (RBAC)

The system supports four distinct operational roles:

| Role | Scope & Permissions |
|---|---|
| **Super Admin** | Complete system authority, department setup, audit logging, role delegation. |
| **Admin** | Event & workshop review/approval, category management, coordinator assignment, institutional reports. |
| **Coordinator** | Assigned event roster oversight, live mobile-friendly QR attendance check-in, Excel attendee exports. |
| **Student** | Catalog browsing, seat reservation, PDF admission tickets with dynamic QR passes, certificates, and feedback. |

---

## 3. Database Configuration

Configured for **Microsoft SQL Server 2025 Express** (`localhost\SQLEXPRESS`):

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost\\SQLEXPRESS;Database=CollegeEventRegistrationDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True"
  }
}
```

---

## 4. Building and Running

### Prerequisites
- .NET 10.0 SDK x64 (v10.0.404+)
- SQL Server 2025 Express (`localhost\SQLEXPRESS`)
- Google Antigravity IDE (with C# language extension)

### Build the Solution
```powershell
dotnet build CollegeEventRegistration.sln
```

### Run the Web Application
```powershell
dotnet run --project src\CollegeEventRegistration.Web\CollegeEventRegistration.Web.csproj
```
The application will launch on `https://localhost:5001` or `http://localhost:5000`.

---

## 5. UI Design System
Please refer to [UI_DESIGN_GUIDELINES.md](file:///E:/CollegeEventRegistration/UI_DESIGN_GUIDELINES.md) for complete visual tokens, 3D interaction constraints, elevation hierarchies, and WCAG AA accessibility standards.

