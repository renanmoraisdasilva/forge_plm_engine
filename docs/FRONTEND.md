# Forge PLM — Frontend Slice Record (Slice 7)

- **Written:** 2026-10-21
- **Owner:** Agent A (frontend). Agent B owns `IMPLEMENTATION.md`; the merge is still open (§6).
- **Status:** converged — every screen calls the live API. The contract mock, the mode probe and
  the `VITE_ECO_API_MODE` switch were removed once Slices 4–6 landed.
- **Read alongside:** [`HANDOFF.md`](HANDOFF.md) §8, [`ECO-API-CONTRACT.md`](ECO-API-CONTRACT.md),
  [`DECISIONS.md`](DECISIONS.md), `mockups/precision_industrial_engineering/DESIGN.md` (visual source only).

---

## 1. What shipped

The SPA lives in `frontend/` (not the spec's `web/`), per HANDOFF §8.

| Screen | State |
|---|---|
| App shell, sidebar (Products, Engineering Changes), actor switch | Done |
| **A** Products list — search, status filter, pager | Live API |
| **B** Product detail — BOM / Revisions / Related Changes | Live API |
| **C** ECO list — search, status filter, pager | Live API |
| **D** ECO detail — stepper, BOM diff, audit timeline, actions | Live API |
| **E** Create / Edit ECO — lines editor | Live API |

**Stack:** Vite 8, React 19, TypeScript 7, Tailwind 4 (build-time via `@tailwindcss/vite`, D-01),
react-router-dom 7, Vitest 5, self-hosted `@fontsource` Inter + JetBrains Mono.

### Live wiring

`src/api/index.ts` binds `LiveProductsApi` and `LiveEcoApi` directly; there is no mode detection
and no environment variable. Both use relative `/api` paths. In development Vite proxies `/api`
to `http://localhost:5199` (`vite.config.ts`), so there is no CORS configuration anywhere.

The client DTOs in `src/api/types.ts` match the backend records in
`src/ForgePlm.Api/Contracts/EcoModels.cs` one-for-one, including the server-resolved
`ecoNumber`, `baseReleasedRevisionId` and `baseRevisionCode`.

---

## 2. Contract compliance

- **FE-02:** invalid actions are *disabled* per status, never offered then rejected. The matrix
  is in `src/lib/ecoStatus.ts`. Release is visible only when the ECO is `Approved`; `Applied` is
  terminal.
- **FE-03:** Approve / Reject / Release each pass through a confirmation dialog showing the
  **current actor** from the header switch at confirmation time.
- **A-15:** UTC is stored and sent; local time is displayed; UTC is exposed on hover
  (`src/lib/format.ts`, `Timestamp`).
- Approval creates a revision in **Approved** status and says so explicitly — it does **not**
  release. Release is separate and requires an effective date; the dialog states that release
  takes effect immediately even with a future date.
- Part numbers are monospaced; tables are dense; status chips use the DESIGN.md palette.
- The mockup-only surface with no domain source stays deleted (KPI tiles, Export CSV, Batch Audit,
  CAD/spec viewer, supplier/compliance cards, CCB/ISO copy, signatures, ERP/MES notes,
  notifications, avatar, global search, invented identifiers).

---

## 3. Verification

Run from `frontend/`:

- `npm run build` (`tsc --noEmit && vite build`) → **passes**.
- `npm test` → **4 files, 37 tests, all passing** (`ecoStatus`, `format`, `http`, `ecoApi`).
  `ecoApi.test.ts` pins each live endpoint path, method and body now that the mock contract
  test is gone.

The API container was rebuilt from the committed tree; `/api/products`, `/api/ecos` and
`/api/audit` all return 200. The protected demo path (D-02c) was then driven in a real browser:

**Products → SERVER-100 (rev B) → Create ECO (MB-020 → MB-021) → submit → approve → release →
audit timeline.**

Observed live, with no mock note anywhere:

- The approve dialog shows “Acting as Alex Morgan” plus the “it is not released” copy.
- After approval Submit/Approve/Reject disable and Release appears; after release the ECO is
  **Applied**, the stepper is fully checked, and no action remains.
- Product detail now advances to **revision C**, its BOM shows **MB-021**, the Revisions tab shows
  C `Released` and B `Superseded`, and Related Changes lists ECO-2026-0001.
- A second ECO exercised **draft edit (PUT)** (quantity 3 → 4, persisted), **submit**, and
  **reject** with a required reason (banner “Rejected by Alex Morgan: …”, no revision created).
- The ECO list shows both ECOs with correct statuses, formatting and pager.

---

## 4. Decisions taken inside the frontend

1. **`frontend/`** as the directory (HANDOFF §8), not `web/`.
2. **Live-only binding.** The probe existed only to survive a missing backend; with Slices 4–6
   landed it was deleted rather than left as dead configuration.
3. **No environment file.** The API base is relative and the dev proxy is in `vite.config.ts`.
4. **`.gitignore` status chips:** `Applied` uses info-blue to distinguish it from green
   `Approved`; `Retired` is neutral.

---

## 5. Known limitations (inside the frontend)

- The UI has not been exercised for: a future effective date (the `Scheduled` label), a stale
  base revision / 409 conflict surfaced from a real race, status and search filters beyond
  rendering, and pagination past one page. The code paths exist; only the happy path and reject
  were driven end to end.
- Approval, rejection and release do not enforce separation of duties. The actor switch is
  client-side by design (D-06); the backend accepts the same actor for every step.
- Verification is manual browser work; there is no Playwright/E2E suite.
- `resolveApi` is still `async` although the binding is synchronous. Harmless, kept to avoid
  churn in the provider.

---

## 6. What is missing (handover)

1. **The container does not serve the SPA.** `src/ForgePlm.Api/Dockerfile` still has the
   placeholder “SLICE 7 goes here” build stage, and `Program.cs` only maps the fallback when a
   `wwwroot/index.html` exists. `http://localhost:5199/` is a 404; the SPA runs only through the
   Vite dev server. Wiring a node stage that builds `frontend/` into `wwwroot` closes demo mode
   (A-16). That file is in Agent B's ownership, so it was left untouched.
2. **`docs/IMPLEMENTATION.md` is stale.** It still says “Slice 7 (frontend) next”; this record
   needs merging into it by whoever owns that file.
3. **`Product.UpdatedAtUtc` is still creation-only**, the known rough edge from HANDOFF §10.
4. Optional hardening: an E2E test for the protected path, and UI coverage of the 409/stale-base
   branches.

---

## 7. Read this before changing the ECO screens

- Never send an action the status matrix disallows; disable it instead (FE-02).
- Keep Approve / Reject / Release behind `ConfirmDialog` and keep the actor in it (FE-03).
- Keep the approval notice explicit that the revision is **not** released.
- The base BOM comparison reads revision lines from the product endpoints, not from the ECO
  payload; the payload deliberately carries only the proposed lines and denormalised refs.
