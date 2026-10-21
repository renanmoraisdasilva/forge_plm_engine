# Forge PLM — Parallel Build Handoff Brief

- **Written:** 2026-10-21
- **Purpose:** let two agents work simultaneously without colliding or duplicating work.
- **Read alongside:** [`ECO-API-CONTRACT.md`](ECO-API-CONTRACT.md) (the frozen contract),
  [`IMPLEMENTATION.md`](IMPLEMENTATION.md) (status), [`DECISIONS.md`](DECISIONS.md) (register).

---

## 1. The split

| | **Agent A — Frontend** | **Agent B — Backend** |
|---|---|---|
| Owns | `frontend/` (new directory) | `src/`, `tests/` |
| Scope | Slice 7: shell, actor switch, screens A & B, API client, ECO screens built against the frozen contract | Slices 4–6: ECO create/submit, approval transaction, reject/release, audit read, concurrency proof |
| Gate | `npm run build` + own unit tests | `dotnet build` + `dotnet test` |
| Working dir | a **git worktree** (see §3) | the main tree |

Screens (spec §9): **A** Products list · **B** Product detail · **C** ECO list · **D** ECO detail ·
**E** Create/Edit ECO.

**Agent A builds A, B and all plumbing now.** Those are backed by a live API from Slice 3.
**Agent A builds C, D, E against the frozen contract**, with a mock adapter, and swaps to live
calls when Agent B lands Slices 4–6.

---

## 2. Ground rules for both

1. **Never commit another agent's files.** Check `git status` before staging; stage explicit paths,
   never `git add -A`.
2. **Commit messages are single one-liners.** No body, no trailers.
3. **Commit as** `Renan Silva <81625697+renanmoraisdasilva@users.noreply.github.com>` (already set).
4. **Do not claim tests passed unless you actually ran them.** This is a standing project rule and
   it has already caught two false claims in this repo.
5. **`docs/IMPLEMENTATION.md` is contested.** See §4.
6. **No secrets, no `.env`, no `bin/`, no `obj/`, no `node_modules/`** in any commit.

---

## 3. Agent A must use a git worktree

The two agents share one git index in the main tree. Use `worktree_create` to give Agent A its own
directory. The only genuine merge point then becomes the docs.

Agent A should also add to `.gitignore` before its first commit: `node_modules/`, `dist/`,
`.vite/`. Do that as its own commit so the diff is reviewable.

---

## 4. Parallel test runs — fixed during Slices 4–6

**Agent A's gate is still `npm run build` plus its own frontend tests.** It does not need
`dotnet test`, and should not run it.

The old hazard is gone. `PostgresFixture` previously swept every `forge_plm_test_%` database,
so two `dotnet test` processes could drop each other's databases. It now names databases with
a per-fixture run prefix (`forge_plm_test_{runId}_{sequence}`) and sweeps only that prefix.
Concurrent backend runs are safe; each run still cleans up its own leftovers.

If Agent A needs real data, the seeded container is enough:

```bash
docker compose up -d --wait
# app + API on http://localhost:5199, seeded with SERVER-100 rev B
```

**Only Agent B runs the .NET suite.**

---

## 5. Docs ownership

`docs/IMPLEMENTATION.md` will conflict if both agents edit it.

- **Agent B owns it.** It records test counts, phases and slice records, which only the backend
  agent can verify.
- **Agent A writes `docs/FRONTEND.md`** — its own slice record, decisions taken inside the
  frontend, and rough edges. A later session merges the two.

Neither agent edits the other's file.

---

## 6. Current state — what is true right now

Verified on the committed tree:

- `dotnet build ForgePlm.slnx` → **0 warnings, 0 errors**
- `dotnet test ForgePlm.slnx` → **155 passed, 0 failed** (104 domain + 27 integration + 24 API,
  against real PostgreSQL 18 in Docker)
- `docs/` is tracked; `IMPLEMENTATION.md` is the authoritative slice record.

**Done:** Phase 0 · Slice 1 (domain) · Slice 2 (persistence + seed) · Slice 2b (containerised,
D-07) · Slice 3 (read endpoints + ProblemDetails mapping) · Slice 4 (ECO create/edit/submit and
reads) · Slice 5 (approval transaction) · Slice 6 (reject, release, audit read) · the code-review
fixes recorded in `IMPLEMENTATION.md`.

**Not done:** Slice 7 (frontend) and Slice 8 (remaining write endpoints, final polish).

### Demo entry point

```bash
docker compose up -d --wait     # db + API, single container pair
# http://localhost:5199 — serves /api and (once built) the SPA
```

Development keeps `dotnet run` + `npm run dev` for hot reload (D-07: Docker is demo mode only).

---

## 7. Agent B — backend scope, completed

Slices 4–6 are implemented and tested. The implementation record, including the ten-step approval
transaction and the ordered release transaction, is in `IMPLEMENTATION.md`. The remaining backend
work is Slice 8: `POST /api/products` and `POST /api/components`, final error states, and README.

### Error mapping already exists — do not rebuild it

`ErrorResponseMapper` + `ForgePlmExceptionHandler` are registered and tested. Endpoints let
exceptions propagate; they do not catch them. Add new codes to `DomainErrorCodes` and, if they come
from a constraint, to the index-name table in the mapper.

One trap already found: `app.UseExceptionHandler()` must stay **explicit** in `Program.cs`.
`AddExceptionHandler` alone is not enough — the developer exception page sits outer in Development
and swallows everything first, turning a 400 into a 500 with a stack trace.

---

## 8. Agent A — frontend scope

**Do now** (live API):

- Scaffold Vite + React + TS + Tailwind (build-time Tailwind, per D-01).
- App shell, sidebar with exactly two entries: **Products**, **Engineering Changes** (§9).
- **Actor switch** (D-06): Alex Morgan / Jordan Lee. Client-side state, no endpoint.
- Screen **A** — Products list: search, status filter, pager.
- Screen **B** — Product detail: header, tabs BOM / Revisions / Related Changes, `Create ECO`
  button.
- API client: typed fetch wrapper handling ProblemDetails and surfacing `code` / `conflictType`.
- Mock adapter implementing `ECO-API-CONTRACT.md`, so screens C/D/E build before the backend lands.

**Then** screens C, D, E against the contract, swapping mocks for live calls as slices land.

### Visual language

From `mockups/precision_industrial_engineering/DESIGN.md`:
dense tables, status chips, **monospaced part numbers**, the ECO lifecycle stepper, the
before/after BOM comparison, the audit timeline, Inter + JetBrains Mono.

**Delete every mockup element with no domain source** (§9): KPI tiles and percentages, Export CSV,
Batch Audit, CAD/Spec-sheet viewer, supplier (AML) and compliance (RoHS/REACH) cards, CCB authority
and the "ISO/IEC 15288" checkbox, signature badges, ERP/MES notes, notifications, user avatar,
global search bar, pagination beyond the standard pager, and invented identifiers like
`CCB-REQ-77401`.

The mockups **contradict the spec** on approval/release semantics and lifecycle vocabulary. The
spec wins (D-04a). Rewrite mockup copy; do not port it.

### Hard requirements

- **FE-02:** invalid actions **disabled** per ECO status — never offered and then rejected. The
  status matrix is in the contract.
- **FE-03:** Approve / Reject / Release require confirmation showing the **current actor**.
- Release is visible **only** when the ECO is `Approved`.
- UTC stored, local displayed, UTC on hover (A-15).
- No `Create Product` UI (§9A).

---

## 9. Convergence

When Agent B has landed Slices 4–6:

1. Agent A deletes the mock adapter and points the ECO screens at the live API.
2. Run the protected demo path end to end (D-02c):
   **Products → SERVER-100 (rev B) → create ECO (MB-020 → MB-021) → submit → approve → release →
   audit timeline.**
3. Merge `docs/FRONTEND.md` into `IMPLEMENTATION.md`.
4. Slice 8: `POST /api/products` + `POST /api/components`, error states, README.

Nothing below the protected path may be cut to save a slice.

---

## 10. Known rough edges (both agents)

- `Product.UpdatedAtUtc` is set at creation only. Either maintain it or drop the column.
- The 409 paths are implemented and mapped but not yet reachable through an endpoint, because no
  write endpoint exists. They become real in Slices 5–6.
- `dotnet ef database update` logs a scary `fail:` line on a genuinely fresh database (the
  history-table query runs before the table exists) and then succeeds. Harmless.
- No OpenAPI document is committed; it is generated in Development only.
