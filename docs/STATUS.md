# Implementation status

Updated 2026-10-21. This page records the current state; the [specification](Forge-PLM-Spec.md)
remains the product and engineering guide.

## Implemented

- Domain model, PostgreSQL persistence, migrations, seed data, and product/BOM read APIs.
- ECO create, edit, submit, approve, reject, release, and audit APIs. Approval and release are
  separate operations; release and its audit record are transactional.
- Commit `0ae3398` adds API-only product/component creation and writes matching audit events.
  This work has not yet been verified.
- React frontend with Products and Engineering Changes screens, actor switch, live API client,
  confirmation flows, BOM comparison, and audit timeline.
- Compose demo image that builds and serves the frontend from the API host. The image change is
  committed; a post-change full image/browser verification has not been recorded.

## Last recorded verification

These results predate commits `57e674d` and `0ae3398`. The catalog-create changes in `0ae3398`
have not been re-run in this status record.

- Backend: build passed with zero warnings/errors; **155 tests passed** (104 domain, 27 PostgreSQL
  integration, 24 API).
- Frontend: build passed; **37 tests passed** in four files. V8 whole-`src/` coverage measured
  **13.35% statements, 8.07% branches, 14.92% functions, and 13.58% lines**.
- Backend coverage collection is configured, but no backend percentage has been measured.

## Remaining work

1. Verify the Compose image starts, the SPA loads at `/`, a client-side route loads, and unknown
   `/api` paths still return ProblemDetails.
2. Add API tests for product/component creation and verify their audit behavior.
3. Re-run backend build and tests after the latest code changes, then collect Cobertura coverage
   with `coverage.ps1` and record the result.
4. Add frontend tests for screens and shared components if higher whole-project coverage is wanted.

The frontend report currently measures mostly transport and domain helpers; the screens, shared
components, providers, and router have no automated coverage. Integration tests require the
Compose PostgreSQL service. Each test run uses its own database prefix so concurrent runs do not
drop each other's databases.
