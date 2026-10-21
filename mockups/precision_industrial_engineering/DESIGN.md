---
name: Precision Industrial Engineering
colors:
  surface: '#f8f9ff'
  surface-dim: '#cbdbf5'
  surface-bright: '#f8f9ff'
  surface-container-lowest: '#ffffff'
  surface-container-low: '#eff4ff'
  surface-container: '#e5eeff'
  surface-container-high: '#dce9ff'
  surface-container-highest: '#d3e4fe'
  on-surface: '#0b1c30'
  on-surface-variant: '#3f4850'
  inverse-surface: '#213145'
  inverse-on-surface: '#eaf1ff'
  outline: '#707881'
  outline-variant: '#bfc7d2'
  surface-tint: '#006398'
  primary: '#006194'
  on-primary: '#ffffff'
  primary-container: '#007bb9'
  on-primary-container: '#fdfcff'
  inverse-primary: '#93ccff'
  secondary: '#565e74'
  on-secondary: '#ffffff'
  secondary-container: '#dae2fd'
  on-secondary-container: '#5c647a'
  tertiary: '#006195'
  on-tertiary: '#ffffff'
  tertiary-container: '#287ab3'
  on-tertiary-container: '#fdfcff'
  error: '#ba1a1a'
  on-error: '#ffffff'
  error-container: '#ffdad6'
  on-error-container: '#93000a'
  primary-fixed: '#cce5ff'
  primary-fixed-dim: '#93ccff'
  on-primary-fixed: '#001d31'
  on-primary-fixed-variant: '#004b73'
  secondary-fixed: '#dae2fd'
  secondary-fixed-dim: '#bec6e0'
  on-secondary-fixed: '#131b2e'
  on-secondary-fixed-variant: '#3f465c'
  tertiary-fixed: '#cde5ff'
  tertiary-fixed-dim: '#94ccff'
  on-tertiary-fixed: '#001d32'
  on-tertiary-fixed-variant: '#004b74'
  background: '#f8f9ff'
  on-background: '#0b1c30'
  surface-variant: '#d3e4fe'
typography:
  headline-xl:
    fontFamily: Inter
    fontSize: 24px
    fontWeight: '600'
    lineHeight: 32px
    letterSpacing: -0.02em
  headline-lg:
    fontFamily: Inter
    fontSize: 18px
    fontWeight: '600'
    lineHeight: 24px
    letterSpacing: -0.015em
  headline-md:
    fontFamily: Inter
    fontSize: 15px
    fontWeight: '600'
    lineHeight: 20px
    letterSpacing: -0.01em
  headline-sm:
    fontFamily: Inter
    fontSize: 13px
    fontWeight: '600'
    lineHeight: 18px
    letterSpacing: -0.005em
  body-lg:
    fontFamily: Inter
    fontSize: 14px
    fontWeight: '400'
    lineHeight: 20px
    letterSpacing: 0em
  body-md:
    fontFamily: Inter
    fontSize: 13px
    fontWeight: '400'
    lineHeight: 18px
    letterSpacing: 0em
  body-sm:
    fontFamily: Inter
    fontSize: 12px
    fontWeight: '400'
    lineHeight: 16px
    letterSpacing: 0em
  label-md:
    fontFamily: Inter
    fontSize: 12px
    fontWeight: '500'
    lineHeight: 16px
    letterSpacing: 0.01em
  label-sm:
    fontFamily: Inter
    fontSize: 11px
    fontWeight: '600'
    lineHeight: 14px
    letterSpacing: 0.02em
  code-sm:
    fontFamily: JetBrains Mono
    fontSize: 12px
    fontWeight: '500'
    lineHeight: 16px
    letterSpacing: -0.01em
rounded:
  sm: 0.125rem
  DEFAULT: 0.25rem
  md: 0.375rem
  lg: 0.5rem
  xl: 0.75rem
  full: 9999px
spacing:
  gutter: 0.75rem
  margin: 1rem
  space-xs: 0.25rem
  space-sm: 0.375rem
  space-md: 0.5rem
  space-lg: 0.75rem
  space-xl: 1rem
---

## Brand & Style

This design system is engineered for mission-critical hardware product lifecycle management, multi-tier bill-of-materials (BOM) auditing, and complex engineering change order (ECO/ECN) orchestration. The aesthetic reflects absolute operational precision, utilitarian discipline, and high information throughput.

Drawing from modern technical minimalism and structural enterprise tooling, the interface rejects decorative gradients, excessive padding, oversized hero cards, and whimsical animations. Instead, it prioritizes maximum visible density, crisp visual hierarchy, deterministic contrast, and scannable workflows. The system evokes feelings of surgical accuracy, dependability, and authoritative control, empowering systems architects, release managers, and hardware engineers to inspect deep part trees and navigate multi-step validation gates without cognitive fatigue.

## Colors

The palette is anchored in technical slate neutrals with a disciplined engineering blue accent and deterministic semantic lifecycle signals.

### Color Tokens & Usage
- **Primary Canvas Background (`#F8FAFC`):** Base neutral canvas for all main viewport areas and application framing.
- **Surface Clean (`#FFFFFF`):** High-contrast structural backing for data grids, inspection drawers, form panels, and metadata cards.
- **Muted Surface (`#F1F5F9`):** Dedicated to table headers, segmented control tracks, disabled inputs, and inactive tabs.
- **Structural Borders (`#E2E8F0`):** Hairline separation (1px) for grid cells, toolbars, splitters, and panel partitions.
- **Interactive Border (`#CBD5E1`):** Default state for editable fields, dropdown triggers, and interactive cards.
- **Primary Interactive (`#0284C7`):** Primary actions, active navigation states, selected table rows, and focused control halos.
- **Primary Interactive Pressed / Active (`#0369A1`):** Hover/press state for primary triggers and focus indicators.
- **Deep Slate Typography (`#0F172A`):** Dominant text token for part numbers, headers, revisions, and critical attributes.
- **Subtle Slate Typography (`#475569`):** Secondary metadata, column labels, unit indicators, and timestamps.
- **Muted Slate Typography (`#64748B`):** Placeholder text, breadcrumb dividers, disabled values, and metadata keys.

### Semantic Lifecycle Status Colors
Semantic colors are strictly pair-coded (solid text/icon on tinted background) for instant visual parsing across dense trees:
- **Released / Approved:** Fill `#DCFCE7`, Text/Border `#16A34A`
- **In Review / Pending Action:** Fill `#FEF3C7`, Text/Border `#D97706`
- **Rejected / Blocked / Obsolete:** Fill `#FEE2E2`, Text/Border `#DC2626`
- **Draft / In-Work / Superseded:** Fill `#F1F5F9`, Text/Border `#64748B`

## Typography

Typography is calibrated strictly for high-density tabular consumption, precise technical nomenclature, and rapid alphanumeric scanning.

### Core Guidelines
- **Systematic Font Stack:** Standardizes on `Inter`, with fallback to `system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif`. For part numbers, revision identifiers, CAGE codes, hashes, and serial strings, `JetBrains Mono` or tabular monospaced numbers must be utilized.
- **Tabular Numerics:** Enable `font-feature-settings: "tnum" 1, "cv05" 1` across all body, cell, and label tokens to guarantee vertical alignment of numeric dimensions, tolerances, and dates.
- **Scale Restraint:** The primary interface hovers between `11px` and `14px`. Sizes above `18px` are strictly reserved for module headers, change request titles, and root viewport breadcrumbs.

## Layout & Spacing

The layout is desktop-first, calibrated specifically for viewports of `1440x1000` and above. Space is treated as a finite technical resource.

### Layout Philosophy
- **Split-Pane Architecture:** Uses a collapsible 3-column paradigm:
  1. Primary Global & Context Tree Sidebar: `240px` default, collapsible to `48px`.
  2. Central Workbench / Data Grid: Fluid width (`1fr`), minimum `640px`.
  3. Inspector / Audit / Change Preview Drawer: `360px` or `440px`, docked right.
- **Viewport Locking:** The outer application frame locks to `100vh`. Scrolling occurs exclusively within individual data containers, tree lists, or inspection panes.
- **Compact Spacing Scale:** Component padding strictly relies on `4px`, `6px`, `8px`, and `12px` intervals. This ensures 24–32 rows of BOM items remain visible above the fold without pagination.
- **Vertical Alignment:** Toolbar items maintain a rigid height of `32px` or `36px`. Table rows maintain a compact height of `28px` (ultra-dense) or `34px` (standard dense).

## Elevation & Depth

Visual hierarchy is communicated via clean structural boundaries and deliberate surface stacking, completely avoiding blurred drop shadows or soft ambient clouds.

### Elevation Principles
- **Tier 0 (Root Level):** `#F8FAFC` base application background.
- **Tier 1 (Surface Canvas):** `#FFFFFF` cards, data grid containers, and tab bodies, outlined by a 1px solid `#E2E8F0` border. Zero shadow.
- **Tier 2 (Sticky Headers & Toolbar Layer):** `#FFFFFF` with a 1px bottom border `#CBD5E1`. On scroll, an imperceptible edge line is preserved without casting blurry drop-shadows.
- **Tier 3 (Flyouts, Menus & Autocomplete):** `#FFFFFF` with 1px solid `#CBD5E1` and a precise, compact technical shadow: `0 2px 4px rgba(15, 23, 42, 0.08)`.
- **Tier 4 (Modal Dialogs & Impact Modals):** `#FFFFFF` with 1px solid `#94A3B8`, accompanied by a direct directional shadow: `0 8px 16px -2px rgba(15, 23, 42, 0.12)`, anchored over a backdrop of `rgba(15, 23, 42, 0.45)`.

## Shapes

The geometric personality of this design system is tight, linear, and utilitarian.

- **Base Radius (3px–4px):** Applied to inputs, buttons, filter chips, table selection rows, and badge tokens.
- **Panel Radius (4px–6px):** Applied to top-level containers, floating toolbars, inspection cards, and modal dialogs.
- **Pill Exceptions (Fully Rounded):** Permitted only for status tags and revision phase indicators to distinguish semantic states from actionable button controls.

## Components

### Buttons
- **Primary:** Background `#0284C7`, text `#FFFFFF`, border none, hover `#0369A1`, active `#075985`. Height `28px` (compact) or `32px` (regular). Padding `0 10px`. Font `12px`, weight 500.
- **Secondary (Standard):** Background `#FFFFFF`, text `#0F172A`, border `1px solid #CBD5E1`, hover background `#F8FAFC`.
- **Destructive:** Background `#FFFFFF`, text `#DC2626`, border `1px solid #FCA5A5`, hover background `#FEF2F2`.
- **Ghost / Icon Action:** Transparent background, text `#475569`, hover background `#F1F5F9`, hover text `#0F172A`. Size `28x28px`.

### Tables & BOM Grids
- **Header:** Height `32px`, background `#F1F5F9`, border-bottom `1px solid #CBD5E1`. Font `11px`, weight 600, uppercase with slight tracking (`0.04em`), color `#475569`.
- **Row:** Height `30px` (dense) or `36px` (comfortable). Border-bottom `1px solid #E2E8F0`. Hover state `#F8FAFC`. Selected state `#F0F9FF` with a 2px left border in `#0284C7`.
- **Cell Content:** Tabular alignment. Numerical attributes right-aligned; part numbers and descriptions left-aligned. Monospaced rendering for part IDs (`JetBrains Mono`, 12px).

### Lifecycle Status Chips
- Height `20px`, padding `0 6px`, radius `3px`, font size `11px`, font weight 600.
- **Released:** Text `#15803D`, background `#DCFCE7`, border `1px solid #86EFAC`.
- **Pending Review:** Text `#B45309`, background `#FEF3C7`, border `1px solid #FCD34D`.
- **Rejected:** Text `#B91C1C`, background `#FEE2E2`, border `1px solid #FCA5A5`.
- **Draft:** Text `#475569`, background `#F1F5F9`, border `1px solid #CBD5E1`.

### Form Inputs & Controls
- **Text Inputs & Selects:** Height `30px`. Background `#FFFFFF`, border `1px solid #CBD5E1`, radius `4px`, padding `0 8px`. Font size `12px`, color `#0F172A`. Focused state: border `#0284C7` with a `0 0 0 1px #0284C7` focus ring.
- **Checkboxes & Radios:** Size `14x14px`. Checkbox radius `3px`, border `1px solid #94A3B8`. Selected state: `#0284C7` background with a crisp white check vector.

### Navigation: Breadcrumbs, Tabs & Workflow Steppers
- **Breadcrumbs:** Height `24px`. Level divider `/` or chevron `#94A3B8`. Current branch bold `#0F172A`, ancestor nodes `#64748B` with hover underline.
- **Segmented / Underline Tabs:** Height `34px`. Tab spacing `16px`. Active tab has 2px solid bottom line `#0284C7` and weight 600 text `#0F172A`. Inactive text `#64748B`.
- **Workflow / ECO Pipeline Stepper:** Linear sequence of connected horizontal pills. Completed stages feature `#16A34A` text with check icons; current stage is filled with `#0284C7` and white text; future stages are `#F1F5F9` with `#64748B` text. Connected by a 1px solid horizontal spine `#CBD5E1`.