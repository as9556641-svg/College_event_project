# VAP UNIVERSITY — Campus Event Hub
# Comprehensive Frontend Architecture Audit & Visual Redesign Roadmap

**Project:** College Event and Workshop Registration System<br />
**Institution:** VAP UNIVERSITY<br />
**Workspace:** `E:\CollegeEventRegistration`<br />
**Framework:** ASP.NET Core MVC (.NET 10 LTS)<br />
**Database:** SQL Server 2025 Express (`CollegeEventRegistrationDb`)<br />
**ORM:** Entity Framework Core 10<br />
**Authentication:** ASP.NET Core Identity<br />
**Design Reference:** Modern split-screen authentication, right-side notification drawer, and smooth dashboard scrolling inspired by [bluesqx.com](https://www.bluesqx.com/).<br />
**Document Status:** Phase 0 & Phase 1 Completed — Awaiting Approval for Phase 2.

---

## 1. Executive Summary & Objectives

This document establishes the comprehensive technical audit of the existing frontend architecture for **VAP UNIVERSITY's Campus Event Hub** and provides a risk-mitigated, phase-wise implementation roadmap for a complete visual redesign.

### Key Redesign Directives:
1. **Desktop Sign In:** Modern split-screen composition featuring an approved full-height photograph of the VAP UNIVERSITY campus on one side and a clean, accessible white authentication container on the other.
2. **Mobile Sign In:** Dedicated mobile-first composition centered on institutional clarity, high-contrast inputs, and single-handed touch accessibility.
3. **Notification System:** A floating right-side slide-out drawer featuring category filters (All, Announcements, Registrations, Approvals) and dynamic notification items.
4. **Dashboard & Navigation Experience:** Inspired by the visual refinement, sleek typography, subtle elevations, and smooth scrolling aesthetics of [bluesqx.com](https://www.bluesqx.com/), adapted strictly to VAP UNIVERSITY's official blue, navy, and teal institutional identity.
5. **Strict Preservation of Business Logic:** No controllers, database tables, EF Core migrations, domain rules, role authorizations, or API routes are to be altered during frontend redesign.

---

## 2. Existing Frontend Architecture

### 2.1 Technology Stack & Assets
- **Templating Engine:** ASP.NET Core MVC Razor Views (`.cshtml`) with Razor Tag Helpers and Partial Views.
- **CSS Framework:** Bootstrap 5.3 (located at `wwwroot/lib/bootstrap/dist/css/bootstrap.min.css`).
- **Custom Stylesheets:**
  - [site.css](file:///e:/CollegeEventRegistration/src/CollegeEventRegistration.Web/wwwroot/css/site.css) (9.9 KB): Global design tokens, base typography, custom buttons, navbar styling, footer styling.
  - [dashboard.css](file:///e:/CollegeEventRegistration/src/CollegeEventRegistration.Web/wwwroot/css/dashboard.css) (12.2 KB): Administrative dashboard shell, sidebar navigation, top bar, metric cards, status pills, data tables.
  - [auth.css](file:///e:/CollegeEventRegistration/src/CollegeEventRegistration.Web/wwwroot/css/auth.css) (15.9 KB): Split-screen authentication layouts, form controls, password toggles, responsive breakpoints.
  - [discovery.css](file:///e:/CollegeEventRegistration/src/CollegeEventRegistration.Web/wwwroot/css/discovery.css) (15.3 KB): Student event discovery portal, hero banner, filter toolbar, event cards grid, session timeline.
- **Client-Side Scripts:**
  - `wwwroot/lib/jquery/dist/jquery.min.js`: jQuery library for DOM manipulation and unobtrusive validation.
  - `wwwroot/lib/bootstrap/dist/js/bootstrap.bundle.min.js`: Bootstrap 5 JavaScript components (dropdowns, collapse, modals).
  - `wwwroot/lib/jquery-validation/`: Client-side validation engine.
  - `wwwroot/lib/jquery-validation-unobtrusive/`: Unobtrusive data-val attributes support for ASP.NET Core Identity.
  - [site.js](file:///e:/CollegeEventRegistration/src/CollegeEventRegistration.Web/wwwroot/js/site.js): Global site script.
  - [dashboard.js](file:///e:/CollegeEventRegistration/src/CollegeEventRegistration.Web/wwwroot/js/dashboard.js): Sidebar toggle and backdrop interactions.
  - [auth.js](file:///e:/CollegeEventRegistration/src/CollegeEventRegistration.Web/wwwroot/js/auth.js): Password visibility toggle and submit loading states.
- **Institutional Branding Assets:**
  - [vap-university-logo.png](file:///e:/CollegeEventRegistration/src/CollegeEventRegistration.Web/wwwroot/images/branding/vap-university-logo.png) (Official crest & logo, PNG format).
  - [vap-university-logo.jpg](file:///e:/CollegeEventRegistration/src/CollegeEventRegistration.Web/wwwroot/images/branding/vap-university-logo.jpg) (Official crest & logo, JPEG format).
  - [vap-university-campus.jpg](file:///e:/CollegeEventRegistration/src/CollegeEventRegistration.Web/wwwroot/images/branding/vap-university-campus.jpg) (Official approved photograph of VAP UNIVERSITY campus).

### 2.2 Layout Hierarchy & Inheritance Chain
The application utilizes three primary Razor layouts:
```
1. _Layout.cshtml (Public & Root Auth Views)
   ├── _LoginPartial.cshtml
   └── @RenderBody()

2. _DashboardLayout.cshtml (Role-based Dashboards: SuperAdmin, Admin, Coordinator, Student)
   ├── Aside: .dash-sidebar (Header, Navigation Links, Profile Footer)
   ├── Header: .dash-topbar (Toggle Hamburger, Role Badge, System Status, User Menu)
   ├── Main: .dash-content -> @RenderBody()
   └── Footer: .dash-footer

3. _StudentPortalLayout.cshtml (Student Event Discovery Portal)
   ├── Header: .portal-navbar (Logo, Discover Events link, Dashboard link, Student Pill Dropdown)
   ├── Main: @RenderBody()
   └── Footer: Institutional Copyright & Policy Footer
```

ViewStart discovery across workspaces:
- `Views/_ViewStart.cshtml` &rarr; `Layout = "_Layout"`
- `Areas/SuperAdmin/Views/_ViewStart.cshtml` &rarr; `Layout = "_DashboardLayout"`
- `Areas/Admin/Views/_ViewStart.cshtml` &rarr; `Layout = "_DashboardLayout"`
- `Areas/Coordinator/Views/_ViewStart.cshtml` &rarr; `Layout = "_DashboardLayout"`
- `Areas/Student/Views/_ViewStart.cshtml` &rarr; `Layout = "_DashboardLayout"`

---

## 3. Complete Screen Inventory

The application consists of **29 distinct screens** across public, authentication, and 4 role-based areas:

| # | Screen Name | Razor View File | Controller & Action | Route | Active Layout | Role Restriction | Primary UI Functionality |
|:---:|:---|:---|:---|:---|:---|:---|:---|
| **01** | **Public Catalog** | `Views/Home/Index.cshtml` | `HomeController.Index` | `/` or `/Home` | `_Layout` | Public | Hero welcome, category navigation cards, upcoming published events grid. |
| **02** | **About & Support** | `Views/Home/Privacy.cshtml` | `HomeController.Privacy` | `/Home/Privacy` | `_Layout` | Public | Institutional governance, data privacy policies, academic integrity guidelines. |
| **03** | **Global Error** | `Views/Shared/Error.cshtml` | `HomeController.Error` | `/Home/Error` | `_Layout` | Public | Friendly error screen with RequestId diagnostics. |
| **04** | **Sign In** | `Views/Account/Login.cshtml` | `AccountController.Login` | `/Account/Login` | `null` (Self-contained) | Anonymous | Split-screen desktop login, password show/hide, remember me, return URL support. |
| **05** | **Student Enrollment** | `Views/Account/Register.cshtml` | `AccountController.Register` | `/Account/Register` | `_Layout` | Anonymous | Student registration form, department selection, client/server validation. |
| **06** | **Forgot Password** | `Views/Account/ForgotPassword.cshtml` | `AccountController.ForgotPassword` | `/Account/ForgotPassword` | `_Layout` | Anonymous | Email recovery form with anti-forgery protection. |
| **07** | **Password Confirmation** | `Views/Account/ForgotPasswordConfirmation.cshtml` | `AccountController.ForgotPasswordConfirmation` | `/Account/ForgotPasswordConfirmation` | `_Layout` | Anonymous | Informational post-dispatch confirmation screen. |
| **08** | **Access Denied (403)** | `Views/Account/AccessDenied.cshtml` | `AccountController.AccessDenied` | `/Account/AccessDenied` | `_Layout` | Authenticated | Institutional 403 Forbidden screen with recovery navigation. |
| **09** | **SuperAdmin Dashboard** | `Areas/SuperAdmin/Views/Dashboard/Index.cshtml` | `SuperAdmin/DashboardController.Index` | `/SuperAdmin` | `_DashboardLayout` | `SuperAdmin` | System telemetry (Depts, Categories, Events, Users), live audit trail log feed. |
| **10** | **Department List** | `Areas/SuperAdmin/Views/Departments/Index.cshtml` | `SuperAdmin/DepartmentsController.Index` | `/SuperAdmin/Departments` | `_DashboardLayout` | `SuperAdmin` | Department data table, code badge, active status toggles, deletion guard actions. |
| **11** | **Department Create** | `Areas/SuperAdmin/Views/Departments/Create.cshtml` | `SuperAdmin/DepartmentsController.Create` | `/SuperAdmin/Departments/Create` | `_DashboardLayout` | `SuperAdmin` | Department creation form, unique code uppercase validation, HOD field. |
| **12** | **Department Edit** | `Areas/SuperAdmin/Views/Departments/Edit.cshtml` | `SuperAdmin/DepartmentsController.Edit` | `/SuperAdmin/Departments/Edit/{id}` | `_DashboardLayout` | `SuperAdmin` | Department update form with code immutability protection. |
| **13** | **Department Details** | `Areas/SuperAdmin/Views/Departments/Details.cshtml` | `SuperAdmin/DepartmentsController.Details` | `/SuperAdmin/Departments/Details/{id}` | `_DashboardLayout` | `SuperAdmin` | Department dossier, affiliated faculty/coordinators, active organized events. |
| **14** | **Admin Dashboard** | `Areas/Admin/Views/Dashboard/Index.cshtml` | `Admin/DashboardController.Index` | `/Admin` | `_DashboardLayout` | `Admin, SuperAdmin` | Campus administrative metrics, pending approval queue, quick actions. |
| **15** | **Category List** | `Areas/Admin/Views/Categories/Index.cshtml` | `Admin/CategoriesController.Index` | `/Admin/Categories` | `_DashboardLayout` | `Admin, SuperAdmin` | Category management table, icon preview, active state toggles, event count. |
| **16** | **Category Create** | `Areas/Admin/Views/Categories/Create.cshtml` | `Admin/CategoriesController.Create` | `/Admin/Categories/Create` | `_DashboardLayout` | `Admin, SuperAdmin` | Category creation form, slug auto-generation from name, Bootstrap icon picker. |
| **17** | **Category Edit** | `Areas/Admin/Views/Categories/Edit.cshtml` | `Admin/CategoriesController.Edit` | `/Admin/Categories/Edit/{id}` | `_DashboardLayout` | `Admin, SuperAdmin` | Category update form with URL slug stability enforcement. |
| **18** | **Admin Event List** | `Areas/Admin/Views/Events/Index.cshtml` | `Admin/EventsController.Index` | `/Admin/Events` | `_DashboardLayout` | `Admin, SuperAdmin` | Master event governance grid, multi-criteria filter toolbar, status transition actions. |
| **19** | **Admin Event Create** | `Areas/Admin/Views/Events/Create.cshtml` | `Admin/EventsController.Create` | `/Admin/Events/Create` | `_DashboardLayout` | `Admin, SuperAdmin` | 6-Stage Event Wizard (Basic Info, Schedule, Eligibility, Sessions, Coordinators, Banner). |
| **20** | **Admin Event Edit** | `Areas/Admin/Views/Events/Edit.cshtml` | `Admin/EventsController.Edit` | `/Admin/Events/Edit/{id}` | `_DashboardLayout` | `Admin, SuperAdmin` | Edit existing event proposal across all 6 wizard stages. |
| **21** | **Admin Event Details** | `Areas/Admin/Views/Events/Details.cshtml` | `Admin/EventsController.Details` | `/Admin/Events/Details/{id}` | `_DashboardLayout` | `Admin, SuperAdmin` | Administrative event dossier, approval/rejection modal, audit history, sessions list. |
| **22** | **Coordinator Dashboard** | `Areas/Coordinator/Views/Dashboard/Index.cshtml` | `Coordinator/DashboardController.Index` | `/Coordinator` | `_DashboardLayout` | `Coordinator, SuperAdmin` | Coordinator workspace metrics, assigned events, live attendee check-in counts. |
| **23** | **Coordinator Events List** | `Areas/Coordinator/Views/Events/Index.cshtml` | `Coordinator/EventsController.Index` | `/Coordinator/Events` | `_DashboardLayout` | `Coordinator, SuperAdmin` | Scoped list of events assigned to the logged-in coordinator. |
| **24** | **Coordinator Event Create** | `Areas/Coordinator/Views/Events/Create.cshtml` | `Coordinator/EventsController.Create` | `/Coordinator/Events/Create` | `_DashboardLayout` | `Coordinator, SuperAdmin` | 6-Stage event proposal creation for coordinators. |
| **25** | **Coordinator Event Edit** | `Areas/Coordinator/Views/Events/Edit.cshtml` | `Coordinator/EventsController.Edit` | `/Coordinator/Events/Edit/{id}` | `_DashboardLayout` | `Coordinator, SuperAdmin` | Edit proposal with service-level restriction to `Draft` and `Rejected` events only. |
| **26** | **Coordinator Event Details**| `Areas/Coordinator/Views/Events/Details.cshtml` | `Coordinator/EventsController.Details` | `/Coordinator/Events/Details/{id}` | `_DashboardLayout` | `Coordinator, SuperAdmin` | Coordinator event overview, session track schedules, and participant roster. |
| **27** | **Student Dashboard** | `Areas/Student/Views/Dashboard/Index.cshtml` | `Student/DashboardController.Index` | `/Student` | `_DashboardLayout` | `Student, SuperAdmin` | Enrolled workshops list, digital pass codes, attendance history, certificate count. |
| **28** | **Student Event Discovery**| `Areas/Student/Views/Events/Index.cshtml` | `Student/EventsController.Index` | `/Student/Events` | `_StudentPortalLayout` | `Student, SuperAdmin` | Dedicated discovery catalog, category pills with count badges, faceted filter bar, pagination. |
| **29** | **Student Event Details** | `Areas/Student/Views/Events/Details.cshtml` | `Student/EventsController.Details` | `/Student/Events/Details/{id}` | `_StudentPortalLayout` | `Student, SuperAdmin` | Student event details, chronological sessions timeline, personalized eligibility indicator. |

---

## 4. Shared Component Inventory

1. **`_Layout.cshtml`**
   - Header with VAP University branding, catalog links, and `_LoginPartial.cshtml`.
   - Content wrapper (`<main role="main">`).
   - Sticky footer with policy links and system platform tags.
2. **`_DashboardLayout.cshtml`**
   - Collapsible desktop/mobile sidebar (`.dash-sidebar`) with role-scoped navigation groups.
   - Top action bar (`.dash-topbar`) with status pill and user dropdown.
   - Central content pane (`.dash-content`) with fluid responsive constraints.
   - Footer metadata container.
3. **`_StudentPortalLayout.cshtml`**
   - Student navigation bar with direct links to "Discover Events" and "My Dashboard".
   - Student profile capsule with dynamic user avatar initial and sign-out trigger.
   - Clean institutional footer.
4. **`_LoginPartial.cshtml`**
   - Evaluates authentication state (`SignInManager.IsSignedIn`).
   - Displays role-tailored dashboard navigation link when authenticated.
   - Displays "Sign In" and "Register" call-to-actions when unauthenticated.
5. **`_UniversityBranding.cshtml`**
   - Centralized brand renderer supporting customizable logo heights, classes, and subheadings.
6. **`_ValidationScriptsPartial.cshtml`**
   - Standard unobtrusive validation script loader (`jquery.validate`).

---

## 5. Known UI Issues & Technical Risks

### Issue 1: Duplicate Dashboard Layout on Edit Event Page (Root Cause Identified)
- **Observed Defect:**
  When navigating to `/Admin/Events/Edit/{id}` or `/Coordinator/Events/Edit/{id}`, the page renders the entire dashboard shell twice (two headers, two sidebars, nested content shells).
- **Exact Root Cause:**
  In `Areas/Admin/Views/Events/Edit.cshtml`:
  ```cshtml
  @{
      Layout = "_DashboardLayout";
      ViewData["Title"] = "Edit Event: " + Model.Title;
  }
  <partial name="Create" model="Model" />
  ```
  Meanwhile, `Areas/Admin/Views/Events/Create.cshtml` also explicitly defines:
  ```cshtml
  @{
      Layout = "_DashboardLayout";
      ...
  }
  ```
  And `Areas/Coordinator/Views/Events/Create.cshtml` and `Edit.cshtml` both execute:
  ```cshtml
  @{
      Layout = "_DashboardLayout";
  }
  <partial name="~/Areas/Admin/Views/Events/Create.cshtml" model="Model" />
  ```
  When Razor renders the `<partial name="Create">`, it invokes `Create.cshtml`, which executes its own `Layout = "_DashboardLayout"` directive, causing ASP.NET Core to wrap the already-rendered layout body inside a second, nested instance of `_DashboardLayout.cshtml`.
- **Architectural Solution:**
  Extract the 6-stage wizard form body into a shared partial view `_EventWizardForm.cshtml` (which specifies `Layout = null;`). Both `Create.cshtml` and `Edit.cshtml` in Admin and Coordinator areas will then simply render `@await Html.PartialAsync("_EventWizardForm", Model)` inside their single, outer `_DashboardLayout`.

### Issue 2: CSS Token Inconsistency Across Legacy and Discovery Stylesheets
- The background canvas variable `--bg-canvas` is declared as `#E8E8E8` in `site.css` / `dashboard.css`, but as `#F8FAFC` in `discovery.css`.
- The redesign must unify global tokens under a single master design system file without modifying backend or Razor models.

### Issue 3: Wizard State & Dynamic JavaScript Bindings
- The 6-stage wizard in `Create.cshtml` utilizes client-side JavaScript for step transitions (`goToStep(n)`), dynamic session row insertion (`addSessionRow()`), and dynamic coordinator primary assignment.
- Visual restructuring must strictly retain all element IDs (`#step-1` through `#step-6`), input names (`Sessions[i].SessionTitle`), and CSRF token inputs.

---

## 6. Phase-Wise UI Redesign Implementation Plan

```
┌────────────────────────────────────────────────────────────────────────┐
│                        PHASE 0: AUDIT & ROADMAP                        │
│                   (Completed — UI_REDESIGN_ROADMAP.md)                 │
└──────────────────────────────────┬─────────────────────────────────────┘
                                   │
                                   ▼
┌────────────────────────────────────────────────────────────────────────┐
│          PHASE 1: LAYOUT DE-DUPLICATION & DESIGN SYSTEM TOKENS         │
│  - Refactor _EventWizardForm.cshtml (Fixes duplicate layout bug)       │
│  - Establish unified design tokens (typography, colors, shadows)      │
└──────────────────────────────────┬─────────────────────────────────────┘
                                   │
                                   ▼
┌────────────────────────────────────────────────────────────────────────┐
│           PHASE 2: AUTHENTICATION & IDENTITY SCREENS REDESIGN          │
│  - Desktop Split-Screen Login (photographic campus + clean white form) │
│  - Mobile-First Centered Login composition                             │
│  - Register, Forgot Password, Access Denied aesthetic upgrades         │
└──────────────────────────────────┬─────────────────────────────────────┘
                                   │
                                   ▼
┌────────────────────────────────────────────────────────────────────────┐
│          PHASE 3: DASHBOARD SHELL & NOTIFICATION DRAWER (BLUESQX)      │
│  - Sleek modern topbar & refined sidebar navigation                    │
│  - Floating right-side notification slide-out drawer                   │
│  - SuperAdmin, Admin, Coordinator Dashboard homepages                  │
└──────────────────────────────────┬─────────────────────────────────────┘
                                   │
                                   ▼
┌────────────────────────────────────────────────────────────────────────┐
│            PHASE 4: EVENT MANAGEMENT & 6-STAGE WIZARD REDESIGN         │
│  - All Events governance tables & status transitions                   │
│  - Tactile 6-stage wizard UI with preserved JavaScript state           │
│  - Administrative Event Details dossier & approval/rejection modal     │
└──────────────────────────────────┬─────────────────────────────────────┘
                                   │
                                   ▼
┌────────────────────────────────────────────────────────────────────────┐
│             PHASE 5: DEPARTMENT & CATEGORY GOVERNANCE VIEWS            │
│  - SuperAdmin Departments (Index, Create, Edit, Details)               │
│  - Admin Event Categories (Index, Create, Edit)                        │
└──────────────────────────────────┬─────────────────────────────────────┘
                                   │
                                   ▼
┌────────────────────────────────────────────────────────────────────────┐
│           PHASE 6: STUDENT PORTAL & EVENT DISCOVERY REFINEMENT         │
│  - Harmonize _StudentPortalLayout.cshtml with new design language      │
│  - Student Discovery catalog (/Student/Events) & Details view          │
│  - Student Dashboard (/Student)                                        │
└──────────────────────────────────┬─────────────────────────────────────┘
                                   │
                                   ▼
┌────────────────────────────────────────────────────────────────────────┐
│           PHASE 7: END-TO-END ACCESSIBILITY & REGRESSION AUDIT         │
│  - Automated regression test execution (25 Phase 6 + Phase 5 tests)   │
│  - Cross-device responsive testing (Desktop, Tablet, Mobile)           │
│  - WCAG AA accessibility, contrast & focus indicators verification     │
└────────────────────────────────────────────────────────────────────────┘
```

---

### Detailed Phase Specifications

#### Phase 1: Layout De-Duplication & Unified Design Tokens
- **Objective:** Eliminate the duplicate dashboard layout bug and establish consistent design system tokens.
- **Files Modified/Created:**
  - `src/CollegeEventRegistration.Web/Areas/Admin/Views/Events/_EventWizardForm.cshtml` (New shared wizard partial).
  - `src/CollegeEventRegistration.Web/Areas/Admin/Views/Events/Create.cshtml` (Render partial with single layout).
  - `src/CollegeEventRegistration.Web/Areas/Admin/Views/Events/Edit.cshtml` (Render partial with single layout).
  - `src/CollegeEventRegistration.Web/Areas/Coordinator/Views/Events/Create.cshtml` (Render partial with single layout).
  - `src/CollegeEventRegistration.Web/Areas/Coordinator/Views/Events/Edit.cshtml` (Render partial with single layout).
  - `src/CollegeEventRegistration.Web/wwwroot/css/site.css` (Harmonized color tokens: Navy `#1F2C58`, Electric Blue `#3B82F6`, Teal `#0BA9AC`, Canvas `#F8FAFC`, Surface `#FFFFFF`, soft ambient occlusion shadows).
- **Functionality Preserved:** Full 6-stage wizard state, form submission, field validation, and navigation.
- **Acceptance Criteria:** Both Create and Edit views render exactly one topbar, one sidebar, and one content area across SuperAdmin, Admin, and Coordinator roles.

#### Phase 2: Authentication & Identity Screens Redesign
- **Objective:** Implement the referenced desktop split-screen login and mobile-first login composition.
- **Files Modified:**
  - `src/CollegeEventRegistration.Web/Views/Account/Login.cshtml`
  - `src/CollegeEventRegistration.Web/Views/Account/Register.cshtml`
  - `src/CollegeEventRegistration.Web/Views/Account/ForgotPassword.cshtml`
  - `src/CollegeEventRegistration.Web/Views/Account/ForgotPasswordConfirmation.cshtml`
  - `src/CollegeEventRegistration.Web/Views/Account/AccessDenied.cshtml`
  - `src/CollegeEventRegistration.Web/wwwroot/css/auth.css`
  - `src/CollegeEventRegistration.Web/wwwroot/js/auth.js`
- **Design Reference Implementation:**
  - Desktop: 50% left photographic panel showing `vap-university-campus.jpg` with subtle gradient depth overlay; 50% right panel with VAP University crest, typography, lead icons, password toggle, and loading state.
  - Mobile: Clean, centered single-column composition with high touch targets (min 44px) and prominent institutional crest.
- **Functionality Preserved:** ASP.NET Core Identity authentication, CSRF token validation, error alerts, return URL redirection.

#### Phase 3: Administrative Dashboard Shell & Notification Drawer
- **Objective:** Redesign `_DashboardLayout.cshtml` with bluesqx.com-inspired aesthetics and introduce the floating right-side notification drawer.
- **Files Modified/Created:**
  - `src/CollegeEventRegistration.Web/Views/Shared/_DashboardLayout.cshtml`
  - `src/CollegeEventRegistration.Web/Views/Shared/_NotificationDrawer.cshtml` (New partial).
  - `src/CollegeEventRegistration.Web/wwwroot/css/dashboard.css`
  - `src/CollegeEventRegistration.Web/wwwroot/js/dashboard.js`
  - `src/CollegeEventRegistration.Web/Areas/SuperAdmin/Views/Dashboard/Index.cshtml`
  - `src/CollegeEventRegistration.Web/Areas/Admin/Views/Dashboard/Index.cshtml`
  - `src/CollegeEventRegistration.Web/Areas/Coordinator/Views/Dashboard/Index.cshtml`
- **Design Reference Implementation:**
  - Topbar bell icon with unread badge triggering the floating slide-out notification drawer from the right edge.
  - Notification tabs: "All", "Announcements", "Events", "System".
  - Sleek modern KPI card grids with subtle glass borders, micro-elevation hover lifts, and real SQL telemetry.
- **Functionality Preserved:** Role-based sidebar navigation, active area indicators, live audit trail feeds, and secure sign-out.

#### Phase 4: Event Management & 6-Stage Wizard Redesign
- **Objective:** Modernize master event governance data tables and elevate the 6-stage wizard experience.
- **Files Modified:**
  - `src/CollegeEventRegistration.Web/Areas/Admin/Views/Events/Index.cshtml`
  - `src/CollegeEventRegistration.Web/Areas/Admin/Views/Events/Details.cshtml`
  - `src/CollegeEventRegistration.Web/Areas/Admin/Views/Events/_EventWizardForm.cshtml`
  - `src/CollegeEventRegistration.Web/Areas/Coordinator/Views/Events/Index.cshtml`
  - `src/CollegeEventRegistration.Web/Areas/Coordinator/Views/Events/Details.cshtml`
- **Design Enhancements:**
  - Filter toolbar with smooth dropdowns, search debounce, and badge filters.
  - Tactile 6-stage stepper with progress bar, active pulse indicators, and clear stage validation feedback.
  - Interactive session track cards with speaker avatars and timing pills.
- **Functionality Preserved:** Event approval workflow (Approve, Reject with mandatory reason modal, Publish, Cancel), conflict-of-interest guards, coordinator editing status machines.

#### Phase 5: Department & Category Governance Views
- **Objective:** Modernize SuperAdmin Department and Admin Category management views.
- **Files Modified:**
  - `src/CollegeEventRegistration.Web/Areas/SuperAdmin/Views/Departments/Index.cshtml`, `Create.cshtml`, `Edit.cshtml`, `Details.cshtml`
  - `src/CollegeEventRegistration.Web/Areas/Admin/Views/Categories/Index.cshtml`, `Create.cshtml`, `Edit.cshtml`
- **Design Enhancements:**
  - Clean data tables with inline status pills, quick action buttons, and deletion protection alerts.
- **Functionality Preserved:** Department unique code normalization, category slug generation, and safe deletion guards.

#### Phase 6: Student Portal & Discovery Experience Polish
- **Objective:** Harmonize the student event discovery experience with the newly redesigned visual identity.
- **Files Modified:**
  - `src/CollegeEventRegistration.Web/Views/Shared/_StudentPortalLayout.cshtml`
  - `src/CollegeEventRegistration.Web/Areas/Student/Views/Events/Index.cshtml`
  - `src/CollegeEventRegistration.Web/Areas/Student/Views/Events/Details.cshtml`
  - `src/CollegeEventRegistration.Web/Areas/Student/Views/Dashboard/Index.cshtml`
  - `src/CollegeEventRegistration.Web/wwwroot/css/discovery.css`
- **Design Enhancements:**
  - Bluesqx.com-inspired hero container, quick category scroller, and 16:9 card aspect ratios with clear hierarchy.
- **Functionality Preserved:** Server-side Published-only visibility rules, multi-criteria filtering, IST date conversions, and eligibility evaluation.

#### Phase 7: Comprehensive Regression Verification & Accessibility Audit
- **Objective:** Verify zero regressions across all business logic and validate WCAG AA compliance.
- **Tasks:**
  - Execute automated test suite (all 25 Phase 6 test scenarios + Phase 5 scenarios).
  - Verify zero database modifications or uncleaned test records.
  - Validate responsive layout behavior at 320px (Mobile), 768px (Tablet), 1024px (Laptop), and 1440px+ (Desktop).
  - Verify keyboard focus outlines (`:focus-visible`), contrast ratios (&ge; 4.5:1), and screen-reader accessibility.

---

## 7. Functionality Preservation Checklist

During each phase of redesign, the following critical functional baselines must be maintained with zero regression:

- [ ] **Institutional Branding:** VAP UNIVERSITY crest, campus imagery, and name must be preserved without alteration.
- [ ] **Authentication State:** Multi-role Identity authentication (`SuperAdmin`, `Admin`, `Coordinator`, `Student`), password hashing, lockout policies, and remember-me cookies must function seamlessly.
- [ ] **CSRF Defense:** All POST forms must retain `@Html.AntiForgeryToken()` and validate successfully.
- [ ] **Server-Side Authorization:** Every controller and action must preserve its `[Authorize(Roles = "...")]` attributes.
- [ ] **Published Visibility Guard:** Students must only be able to view events with `Status == EventStatus.Published`. Unpublished events must return `404 Not Found`.
- [ ] **6-Stage Wizard Integrity:** All 6 wizard steps (Basic Info, Schedule, Eligibility, Sessions, Coordinators, Banner Upload) must collect and submit correct DTO data.
- [ ] **Execution Strategy Pattern:** Database transactions with retry strategies in `EventManagementService` must remain intact.
- [ ] **Database Integrity:** No migrations or schema changes are permitted during UI redesign. `CollegeEventRegistrationDb` must remain untouched.

---

## 8. Implementation History & Verification Status

### Phase 1 — Layout De-Duplication & Unified Design Tokens: **COMPLETED & VERIFIED**
- **Executed Actions:**
  1. Extracted the complete 6-stage event creation & editing wizard into `_EventWizardForm.cshtml` with `Layout = null;`.
  2. Updated `Areas/Admin/Views/Events/Create.cshtml` and `Edit.cshtml` to declare exactly one `_DashboardLayout` and render `_EventWizardForm`.
  3. Updated `Areas/Coordinator/Views/Events/Create.cshtml` and `Edit.cshtml` to declare exactly one `_DashboardLayout` and render `_EventWizardForm`.
  4. Fully eliminated the duplicate nested layout bug on `/Admin/Events/Edit/{id}` and `/Coordinator/Events/Edit/{id}`.
  5. Established standardized semantic design tokens in `wwwroot/css/site.css` (surfaces, brand, actions, feedback states, modular typography, borders, shadows, spacing) with 100% backward compatibility aliases.
  6. Preserved all 6 wizard stages, field IDs, client validation, dynamic sessions, coordinators, and draft/submit actions.
- **Verification Results:**
  - `Phase1Verifier`: **28/28 tests passed** (including live HTTP verification of single dashboard shell and sidebar).
  - `Phase6Verifier`: **25/25 tests passed** (zero regressions in discovery or database services).
  - Solution build: **0 warnings, 0 errors**.

---

## 9. Next Action & Confirmation

Phase 1 is completed and verified.

**In strict compliance with the scope rules, development is paused. Awaiting explicit user approval before beginning Phase 2 (Authentication & Identity Screens Redesign).**
