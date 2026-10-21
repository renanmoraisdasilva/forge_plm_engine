# Forge PLM — Frontend Slice Record (Slice 7)

- **Written:** 2026-10-21
- **Owner:** Agent A (frontend). Agent B owns `IMPLEMENTATION.md`; a later session merges this into it.
- **Read alongside:** [`HANDOFF.md`](HANDOFF.md) §8, [`ECO-API-CONTRACT.md`](ECO-API-CONTRACT.md) (frozen),
  [`DECISIONS.md`](DECISIONS.md), `mockups/precision_industrial_engineering/DESIGN.md` (visual source only).

---

## 1. What shipped

The SPA lives in `frontend/` (not the spec's `web/`), per HANDOFF §8 and the staging scope for this slice.

| Area | Status |
|---|---|
| App shell, sidebar (Products, Engineering Changes), actor switch | Done |
| Screen **A** Products list — search, status filter, pager | Live API |
| Screen **B** Product detail — BOM / Revisions / Related Changes tabs | Live API |
| Screen **C** ECO list | Contract mock (live when Slice 4 lands) |
| Screen **D** ECO detail — stepper, BOM diff, audit timeline, actions | Contract mock |
| Screen **E** Create / Edit ECO — lines editor | Contract mock |
| API client — typed fetch, ProblemDetails, `code` / `conflictType` | Done |

**Stack:** Vite 8, React 19, TypeScript 7, Tailwind 4 (build-time via `@tailwindcss/vite`, per D-01),
react-router-dom 7, Vitest 5, self-hosted `@fontsource` Inter + JetBrains Mono (Inter + JetBrains
Mono per DESIGN.md).

### Live vs mock resolution

ECO mode is auto-detected at start-up by probing `GET /api/ecos?pageSize=1`: **2xx → live**,
anything else (including the 404 the backend still returns today) → `MockEcoApi`. `VITE_ECO_API_MODE=live|mock`
overrides the probe. The mock reads **live** products and BOM from the read API and only synthesises the
ECO domain, and the UI says so with a visible **“MOCK ECO API”** note on screens C/D/E. No code change is
needed to go live — the screens swap automatically once Agent B's endpoints exist.

---

## 2. Contract compliance highlights

- **FE-02:** every action button is *disabled* when invalid for the current status, never offered then
  rejected. The status matrix lives in `src/lib/ecoStatus.ts`. Release is visible only when the ECO is
  `Approved`; `Applied` is terminal.
- **FE-03:** Approve / Reject / Release each pass through a confirmation dialog that shows the **current
  actor** read from the header switch at confirmation time.
- **A-15:** UTC is stored and sent; local time is displayed; the UTC value is reachable on hover
  (`src/lib/format.ts`, `Timestamp`).
- Approval creates a revision in **Approved** status and says so explicitly — it does **not** release.
  Release is a separate action; the release dialog requires an effective date and states that release
  takes effect immediately.
- Part numbers are monospaced; tables are dense; status chips come from the DESIGN.md palette.

Every mockup element with no domain source was deleted (KPI tiles/percentages, Export CSV, Batch Audit,
CAD/spec viewer, supplier + compliance cards, CCB authority copy, the ISO/IEC 15288 checkbox, signature
badges, ERP/MES notes, notifications, avatar, global search, invented identifiers). Mockup copy that
contradicted the spec on approval/release semantics was rewritten, not ported.

---

## 3. Verification

- `npm run build` (`tsc --noEmit && vite build`) → **passes**.
- `npm test` (Vitest) → **4 files, 42 tests, all passing** (`ecoStatus`, `format`, `http`, `mockEcoApi`).
- Protected demo path (D-02c) driven in a real browser against the running app + live API, with the mock
  ECO adapter: **Products → SERVER-100 (rev B) → Create ECO (MB-020 → MB-021) → submit → approve →
  release → audit timeline.** Confirmed along the way: the BOM diff shows one changed line of six; the
  approve dialog shows “Acting as Alex Morgan” and the “does not release” copy; after approval the
  Submit/Approve/Reject buttons disable and Release appears; after release the notice reads “…released
  and the previous revision is superseded” and no further actions are offered.

Screens A and B were also verified visually against the live read API.

---

## 4. Decisions taken inside the frontend

1. **`frontend/`** as the directory (HANDOFF §8), not `web/`.
2. **ECO mode probe** instead of a hardcoded mock, so the swap to live needs no edit. See §1.
3. **`.gitignore` status chips:** `Applied` uses the info-blue token so it reads differently from the
   green `Approved`; `Retired` is neutral. The mockup had no distinct colour for `Applied`.
4. `node_modules/`, `dist/`, `.vite/` were already ignored; no separate `.gitignore` commit was needed.

---

## 5. Rough edges

- **Mock revisions are not persisted.** An ECO created in mock mode produces an Approved revision in
  memory only, so screen B's **Revisions** tab will not show it. That resolves the moment live ECO
  endpoints land.
- The mock resets on page reload; the UI warns about this.
- `src/App.tsx` has two separate imports from `react-router-dom`. Harmless; tidy later.
- Screens C/D/E have not been exercised against the real endpoints yet, because those endpoints do not
  exist. The mock is contract-faithful but a contract is not an implementation.

---

## 6. Convergence checklist (for when Slices 4–6 land)

1. Confirm the mode probe flips to live (or set `VITE_ECO_API_MODE=live`).
2. Re-run the protected demo path end to end against the real API.
3. Delete `src/api/mockEcoApi.ts` and its test once live calls are proven.
4. Merge this slice record into `IMPLEMENTATION.md`.
