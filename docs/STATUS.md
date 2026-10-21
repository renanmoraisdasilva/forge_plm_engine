# Implementation status

Updated 2026-10-21. This page records the current state; the [specification](Forge-PLM-Spec.md)
remains the product and engineering guide.

## Implemented

- Domain model, PostgreSQL persistence, migrations, seed data, and product/BOM read APIs.
- ECO create, edit, submit, approve, reject, release, and audit APIs. Approval and release are
  separate operations; release and its audit record are transactional. Release applies
  immediately in the prototype; future requested effectivity requires confirmation, and actual
  release/effective timestamps are recorded separately. Draft edits write an `EcoDraftUpdated`
  event with before/after snapshots and the editing actor in the same save as the ECO and lines.
- API-only product/component creation writes matching audit events. A new active product with no
  revision history can initialize its first released BOM as revision A; later revisions require
  ECOs. ECO writes use PostgreSQL `xmin` concurrency tokens so stale state transitions conflict.
- React frontend with Products and Engineering Changes screens, actor switch, live API client,
  confirmation flows, BOM comparison, and audit timeline.
- Compose demo image that builds and serves the frontend from the API host. The image change is
  built and started successfully for the verification below.

## Last recorded verification

- Backend: `dotnet test ForgePlm.slnx --no-restore` and `coverage.ps1` passed: **163 tests**, including API coverage
  for component creation and draft-edit audit snapshots, and real PostgreSQL coverage for
  initial-BOM setup and ECO concurrency. API test-host logging avoids the Windows Event Log
  permission issue. Cobertura reports **89.00% lines** and **49.31% branches** for the backend test
  run.
- Frontend: production build passed; **39 tests passed** in five files. V8 whole-`src/` coverage
  measured **13.59% statements, 8.36% branches, 15.42% functions, and 13.85% lines**.
- Compose: `docker compose up -d --build --wait` reached healthy status. HTTP smoke checks returned
  the SPA shell for `/` and `/products`, JavaScript/CSS assets loaded, the seeded product and
  current BOM APIs returned 200, and an unknown `/api` route returned ProblemDetails 404.
- A rendered browser check remains unavailable on this Windows host: headless Chrome/Edge failed
  to initialize the GPU process. Direct route and asset HTTP checks passed.

## Remaining work

1. Run a visual browser check on a host where headless Chrome/Edge can initialize; the HTTP fallback
   and asset checks passed here.
2. Add frontend tests for screens and shared components if higher whole-project coverage is wanted.

The frontend report currently measures mostly transport and domain helpers; the screens, shared
components, providers, and router have no automated coverage. Integration tests require the
Compose PostgreSQL service. Each test run uses its own database prefix so concurrent runs do not
drop each other's databases.
