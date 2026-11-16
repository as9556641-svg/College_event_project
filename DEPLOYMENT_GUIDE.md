# VAP UNIVERSITY — Campus Event Hub
# Staging & Production Deployment Manual

This document details the operational deployment procedures, infrastructure prerequisites, database lifecycle management, reverse-proxy configurations, and disaster recovery strategies for **VAP UNIVERSITY's Campus Event Hub**.

---

## 1. Hosting Prerequisites

### 1.1 Server & Runtime Environment
- **Operating System:** Windows Server 2022+ (IIS 10) or Linux (Ubuntu 22.04 LTS / Debian 12 / Rocky Linux 9) with Kestrel behind Nginx / Apache.
- **Runtime:** Microsoft .NET 10.0 Runtime (ASP.NET Core Hosting Bundle 10.0+).
- **Process Model:** In-Process hosting under IIS or systemd daemon on Linux.
- **Hardware Minimums:**
  - 2 vCPU, 4 GB RAM (Small campus deployment: &le; 1,000 active concurrent users).
  - 4 vCPU, 8 GB RAM (Enterprise university campus: &ge; 5,000 active concurrent users).

### 1.2 Database Host
- **RDBMS:** Microsoft SQL Server 2022 / 2025 Standard or Enterprise, or Azure SQL Database / AWS RDS for SQL Server.
- **Collation:** `SQL_Latin1_General_CP1_CI_AS` (Default).
- **Authentication:** SQL Server Authentication or Active Directory / Managed Identity.

---

## 2. Production Configuration

### 2.1 Configuration File Structure
In production, sensitive settings are supplied via **Environment Variables** or secure cloud vaults (e.g., Azure Key Vault, AWS Secrets Manager), never plain-text files.

Create a production template `appsettings.Production.json` with structural keys:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=tcp:sql.vap.edu,1433;Database=CollegeEventRegistrationDb;User Id=AppUser;Password=__ENV_SQL_PASSWORD__;Encrypt=True;TrustServerCertificate=False;MultipleActiveResultSets=True;"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Warning",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore.Database.Command": "Warning"
    }
  },
  "EmailSettings": {
    "SmtpHost": "smtp.sendgrid.net",
    "SmtpPort": 587,
    "SmtpUsername": "apikey",
    "SmtpPassword": "__ENV_SMTP_PASSWORD__",
    "EnableSsl": true,
    "FromEmail": "notifications@vap.edu",
    "FromName": "VAP UNIVERSITY Event Hub"
  },
  "AllowedHosts": "events.vap.edu"
}
```

### 2.2 Environment Variable Overrides
Configure system environment variables or container variables using the standard double-underscore syntax:

| Environment Variable | Description | Example / Note |
|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | Application Environment | `Production` |
| `ConnectionStrings__DefaultConnection` | Production SQL Server Connection String | Encrypted, TrustServerCertificate=False |
| `Razorpay__KeyId` | Razorpay Merchant Key ID | `rzp_test_...` (Keep in Test Mode until approved) |
| `Razorpay__KeySecret` | Razorpay Merchant Secret Key | Cryptographic secret |
| `Razorpay__WebhookSecret` | Webhook verification secret | Cryptographic secret |
| `Tickets__SigningSecret` | HMAC secret for QR ticket payloads | Must be &ge; 32 characters |
| `Authentication__Google__ClientId` | Google OAuth Client ID | Registered with Google Console |
| `Authentication__Google__ClientSecret` | Google OAuth Client Secret | Private OAuth secret |
| `EmailSettings__SmtpPassword` | Institutional SMTP relay secret | App Password / API Key |

---

## 3. Database Deployment & Migration Workflow

### 3.1 Idempotent SQL Migration Script (Recommended)
Generate an idempotent SQL script containing all database schema updates:

```powershell
dotnet ef migrations script `
  --project src/CollegeEventRegistration.Infrastructure `
  --startup-project src/CollegeEventRegistration.Web `
  --idempotent `
  --output ./Deployments/Migration_Production.sql
```
- Review `Migration_Production.sql` with the Lead DBA.
- Execute against the production database within a scheduled maintenance window.

### 3.2 Automated Startup Migration (Alternative)
The application automatically checks for unapplied migrations on startup via `DbInitializer.SeedAsync`. Ensure the database user has `CREATE TABLE`, `ALTER TABLE`, and `CREATE INDEX` privileges.

---

## 4. HTTPS, Reverse-Proxy & Security Headers

### 4.1 Reverse Proxy (Nginx / Linux Configuration)
When hosting behind Nginx or an Application Load Balancer:

```nginx
server {
    listen 80;
    server_name events.vap.edu;
    return 301 https://$host$request_uri;
}

server {
    listen 443 ssl http2;
    server_name events.vap.edu;

    ssl_certificate /etc/letsencrypt/live/events.vap.edu/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/events.vap.edu/privkey.pem;
    ssl_protocols TLSv1.2 TLSv1.3;
    ssl_ciphers HIGH:!aNULL:!MD5;

    location / {
        proxy_pass         http://127.0.0.1:5000;
        proxy_http_version 1.1;
        proxy_set_header   Upgrade $http_upgrade;
        proxy_set_header   Connection "upgrade"; # Required for SignalR WebSocket push
        proxy_set_header   Host $host;
        proxy_cache_bypass $http_upgrade;
        proxy_set_header   X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header   X-Forwarded-Proto $scheme;
    }

    # Restrict direct access to protected private directories
    location ~* /App_Data/ {
        deny all;
        return 404;
    }
}
```

### 4.2 Forwarded Headers in ASP.NET Core
In production behind a reverse proxy, enable Forwarded Headers in `Program.cs`:
```csharp
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});
```

---

## 5. Storage & File System Permissions

### 5.1 Directory Access Requirements
The application requires write permissions to two localized file system stores:
1. `src/CollegeEventRegistration.Web/wwwroot/uploads/banners/`
   - Public event banners.
   - Max size limit: 5 MB. Allowed magic bytes: JPEG, PNG, WEBP.
2. `src/CollegeEventRegistration.Web/App_Data/Certificates/`
   - **Private storage.** Strictly outside `wwwroot`.
   - Never exposed via static file middleware. Served solely through authorized controller actions (`/Student/Certificates/Download/{id}`).

Ensure the application pool identity (e.g. `IIS AppPool\CollegeEventRegistration` or `www-data`) has read/write permissions to these directories.

---

## 6. Backup, Restoration & Rollback

### 6.1 Database Backup Command
```sql
BACKUP DATABASE [CollegeEventRegistrationDb]
TO DISK = N'/var/backups/CollegeEventRegistrationDb_Full.bak'
WITH FORMAT, INIT, NAME = N'CollegeEventRegistrationDb-Full Backup', STATS = 10, COMPRESSION;
```

### 6.2 Database Restoration Command
```sql
USE master;
ALTER DATABASE [CollegeEventRegistrationDb] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
RESTORE DATABASE [CollegeEventRegistrationDb]
FROM DISK = N'/var/backups/CollegeEventRegistrationDb_Full.bak'
WITH REPLACE, RECOVERY;
ALTER DATABASE [CollegeEventRegistrationDb] SET MULTI_USER;
```

### 6.3 Rollback Strategy
1. **Application Code Rollback:** Deploy previous verified Release artifact or rollback container tag.
2. **Database Schema Rollback:** If a migration introduced breaking changes, execute `dotnet ef database update <PreviousMigrationName>` or restore the pre-deployment database backup.

---

## 7. Health Check & Uptime Monitoring

Configure your monitoring platform (e.g., Azure Monitor, Datadog, Prometheus) to poll the built-in health check endpoint:
- **URL:** `https://events.vap.edu/health`
- **Expected Status:** HTTP `200 OK`
- **Response Payload:** `Healthy`
- **Monitored Subsystems:** SQL Server database connectivity, connection pooling, and disk write accessibility.
