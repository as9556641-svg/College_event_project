# UI Design System & 3D Interaction Guidelines

**Project:** College Event and Workshop Registration System  
**Framework:** ASP.NET Core MVC (.NET 10 LTS)  
**Status:** Permanent Project Design Standard

---

## 1. UI Design System Specifications

All user interfaces across public views, student portals, coordinator workspaces, and administrative dashboards must strictly adhere to the following permanent visual identity:

### Color Palette & Tokens
| Token | Hex Value | Usage Role |
|---|---|---|
| `--bg-canvas` | `#E8E8E8` | Primary background canvas |
| `--bg-surface` | `#FFFFFF` | Cards, modals, containers, navigation bars |
| `--color-primary` | `#3B82F6` | Electric Blue: Primary buttons, badges, active tabs, focal links |
| `--color-primary-dark` | `#1D4ED8` | Deep Blue: Hover states, headings, primary contrast text |
| `--color-success` | `#10B981` | Emerald Green: Secondary accent, confirmed registration, success actions |
| `--color-success-dark` | `#047857` | Deep Emerald: Success hover states, verified attendance badges |
| `--text-main` | `#0F172A` | Primary readable text (WCAG AA conformant) |
| `--text-muted` | `#475569` | Secondary metadata and labels |

### Depth, Elevation & 3D Styling
- **Rounded Cards:** All cards and interactive containers must use generous rounded corners (`border-radius: 1rem` to `1.25rem`).
- **Soft Layered Shadows:** Use multi-layered ambient occlusion shadows for elevated surfaces:
  ```css
  box-shadow: 0 10px 25px -5px rgba(15, 23, 42, 0.06), 0 8px 10px -6px rgba(15, 23, 42, 0.04);
  ```
- **Radial Gradients:** Subtle, non-distracting background radial gradients to add depth to canvas areas without hurting readability:
  ```css
  background-image: 
    radial-gradient(circle at 15% 15%, rgba(59, 130, 246, 0.05) 0%, transparent 40%),
    radial-gradient(circle at 85% 85%, rgba(16, 185, 129, 0.05) 0%, transparent 40%);
  ```
- **Tactile Button Interactions:** Buttons feature subtle depth, inset bevel highlights, and smooth micro-lifts on hover (`transform: translateY(-2px)`), returning to normal on active press (`transform: translateY(1px)`).
- **Polished Spacing:** Consistent padding and margin scales (8px baseline: 0.5rem, 1rem, 1.5rem, 2rem, 3rem).

### Transitions & Motion Restraint
- **Smooth Easing:** All interactive transitions use restrained timing (`transition: all 0.25s cubic-bezier(0.4, 0, 0.2, 1)`).
- **Prefers-Reduced-Motion Compliance:** Respect users with motion sensitivity by completely disabling transforms and animations when requested by the OS:
  ```css
  @media (prefers-reduced-motion: reduce) {
    *, *::before, *::after {
      animation-duration: 0.01ms !important;
      transition-duration: 0.01ms !important;
    }
    .card-3d:hover, .btn-3d-primary:hover {
      transform: none !important;
    }
  }
  ```

### Accessibility & Responsiveness
- **Layout Adaptability:** Fully responsive grid layouts accommodating desktop (1200px+), tablet (768px-1199px), and mobile (<768px).
- **Focus Indicators:** Accessible, high-visibility focus rings (`outline: 3px solid rgba(59, 130, 246, 0.6); outline-offset: 2px`) on all focusable controls.
- **Contrast Ratios:** Maintain minimum WCAG AA contrast ratio (4.5:1 for normal text, 3:1 for large text).
- **Keyboard Usability:** Full keyboard tab navigation for all modals, forms, dropdowns, and interactive tables.

---

## 2. 3D Interaction Guidelines

1. **Utility First:** Interactive 3D previews must be employed **only where genuinely useful** to the user experience (e.g. venue/seating layout exploration, interactive badge inspection).
2. **Event Details:** Event detail pages may feature interactive media galleries or 3D venue previews when suitable architectural/event models exist.
3. **No Gratuitous Features:** **Do not invent 3D models or introduce irrelevant AR features** that increase page weight or impede the registration process.
4. **Productivity Views:** For dashboards, administrative data tables, registration forms, and financial reports, use the clean 3D-inspired visual identity (shadows, cards, rounded corners, clean hierarchy) **without unnecessary 3D WebGL interactions**.
5. **Performance & Speed:** Interfaces must load instantly and maintain 60 FPS performance on standard hardware and mobile devices.

