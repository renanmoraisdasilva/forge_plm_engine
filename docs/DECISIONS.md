# Forge PLM — Decision Register

Companion to `Forge-PLM-Spec.md`. Maintained from the Phase 0 interview (2026-10-21).

**Status values**

- `Confirmed` — the product owner chose this explicitly.
- `Assumed` — proposed by the agent, accepted implicitly (the owner did not object). Any of these can be reopened; doing so is cheap *only* before the slice that implements it.
- `Superseded` — a decision that was replaced. Never silently edited; the replacement is recorded and the original stays visible.

A confirmed decision is never changed unilaterally. If new information conflicts with one, it goes back to the owner.

---

## Confirmed decisions

### D-01 — Three projects, Minimal APIs, separate SPA

| Field | Value |
|---|---|
| **Area** | Architecture |
| **Status** | Confirmed |
| **Decision** | Three backend projects: `ForgePlm.Api` (endpoints, application services, EF Core, migrations, seed, served SPA), `ForgePlm.Domain` (aggregates, value objects, enums — no EF/HTTP references), `ForgePlm.Tests` (domain + Postgres integration tests). Minimal APIs grouped by feature. Frontend is a separate Vite + React + TypeScript app with Tailwind compiled at build time. |
| **Rationale** | Primary goal is an interview-ready demo in a few focused sessions (D-02). The domain must be isolated and explainable without 5-project ceremony. The spec's §4 layout was written for a scaffold that does not exist. |
| **Consequences** | No Application/Infrastructure project split; no MediatR, AutoMapper or repository layer. EF `DbContext`, migrations and seed live in `ForgePlm.Api`. Domain purity is enforced by project references alone. Tailwind must be a real build step — the mockups' CDN script is not acceptable in the app. |
| **Spec sections** | §4 |

### D-02 — Goals and budget

| Field | Value |
|---|---|
| **Area** | Product |
| **Status** | Confirmed |
| **Decision** | Primary success criteria: **interview-ready demo** and **demonstrating PLM concepts correctly**. Learning DDD for its own sake is *not* a goal. Budget: a few focused sessions. |
| **Rationale** | Stated by the owner during the interview. |
| **Consequences** | Domain correctness still matters, because a wrong domain is not a credible demo — but DDD *pedagogy* does not get to buy extra ceremony. Strongly-typed IDs, per-aggregate domain projects and value objects that only rename a string are justified case by case or dropped. Scope is cut aggressively when time runs short. |
| **Spec sections** | §1, §15 |

### D-02c — Protected demo path

| Field | Value |
|---|---|
| **Area** | Product |
| **Status** | Confirmed |
| **Decision** | This journey must work end-to-end through the UI, and is cut last: Products list → `SERVER-100` detail (released rev B) → create ECO replacing `MB-020` with `MB-021` → submit → approve (rev C `Approved`, rev B untouched) → release (rev B `Superseded`, effective date recorded) → audit timeline shows the whole story. |
| **Rationale** | It exercises every concept the demo needs to prove: revisions, immutability of history, controlled approval, explicit effectivity, auditability. |
| **Consequences** | Anything not on this path is a candidate for cutting: rejection flow, concurrency demo, component browsing, create-product UI. Anything *above* it — error handling, empty states, confirmations — is part of the path, not optional polish. |
| **Spec sections** | §1 (demo scenario), §16 |

### D-03 — Screens and mockup fidelity

| Field | Value |
|---|---|
| **Area** | UX / scope |
| **Status** | Confirmed |
| **Decision** | Implement §9's five screens in the mockups' visual language (tokens, density, status chips, ECO stepper, before/after BOM diff, audit timeline). Delete all out-of-scope chrome: KPI tiles, Export CSV, Batch Audit, CAD/Spec-sheet viewer, AML + compliance cards, CCB and ISO wording, cryptographic signatures, ERP notes, notifications, avatar, global search. Navigation is exactly **Products** and **Engineering Changes**. |
| **Rationale** | Mockup credibility per hour spent. The mockups' richness is decorative, not domain data; porting it would invent a domain that does not exist. |
| **Consequences** | Components keeps its API (the ECO form needs a picker) but gets no screen. `POST /api/products` and `POST /api/components` remain API-only, with no UI. Mockup copy contradicting the domain is rewritten, not ported. Reuse is limited to small primitives: `DataTable`, `StatusChip`, `ConfirmDialog`, form fields, `AuditTimeline`, `BomDiff`, `Stepper`. |
| **Spec sections** | §2, §9 |

### D-04a — Approval and release are separate

| Field | Value |
|---|---|
| **Area** | Domain |
| **Status** | Confirmed |
| **Decision** | The spec is authoritative over the mockups: approval creates a revision in `Approved` status and changes nothing else. Release is a separate explicit action with its own button and confirmation dialog. |
| **Rationale** | §1 and §15.7 call the separation intentional and require it to be consistent across API and UI. The ECO-details mockup's "approval authorizes the configuration change immediately" copy is wrong and is rewritten. |
| **Consequences** | The ECO pipeline renders as Draft → Submitted → Approved/Rejected → Applied, not the mockup's four-step CCB flow. The demo shows two distinct concepts instead of collapsing them into one. |
| **Spec sections** | §1, §6, §9.D, §15.7 |

### D-04b — Effective date is a recorded label

| Field | Value |
|---|---|
| **Area** | Domain |
| **Status** | Confirmed |
| **Decision** | Release always takes effect immediately: the new revision becomes `Released` and current, the previous one becomes `Superseded`, the ECO becomes `Applied`, and `EffectiveAtUtc` is set — all in one transaction. A future timestamp is accepted and stored as intent, and is displayed as "Scheduled". No background job exists. |
| **Rationale** | A scheduler would make "current released revision" depend on wall-clock time, complicate every query and the concurrency test, and is not needed to demonstrate the concept. |
| **Consequences** | `EffectiveAtUtc` is metadata, not a trigger. The release action's date field is pre-filled from the ECO's `RequestedEffectiveAtUtc`. If genuine scheduled effectivity is ever needed, the "at most one Released revision" rule and the partial index must both be revisited — a revision can be `Released` while not yet effective, which the current model does not express. |
| **Spec sections** | §3, §8, §15.5 |

### D-05 — PostgreSQL 18 in Docker Compose

| Field | Value |
|---|---|
| **Area** | Persistence |
| **Status** | Confirmed — **owner override** of the agent's recommendation (which was SQL Server, already installed on the machine) |
| **Decision** | PostgreSQL 18 via a committed `docker-compose.yml` (named volume, healthcheck). Integration and concurrency tests run against a dedicated database on the same container, created and dropped per test run. |
| **Rationale** | Explicit instruction from the owner: "use postgres with docker, you can download the latest stable version". Version verified 2026-10-15: 18 is the current stable major; 19 is only a release candidate. |
| **Consequences** | EF provider is Npgsql. `xmin` concurrency tokens and partial unique indexes are available and used. SQL Server specifics are removed from the spec. Tests are honest relational tests — the In-memory provider is forbidden for transaction/concurrency claims. The already-installed SQL Server Express instance goes unused. |
| **Spec sections** | §4, §11, §15.1 |

### D-06 — Two-person actor switch

| Field | Value |
|---|---|
| **Area** | Security / demo integrity |
| **Status** | Confirmed |
| **Decision** | The UI has a small persistent "Acting as" selector with two seeded identities: `Alex Morgan` (engineer/author) and `Jordan Lee` (reviewer). Every state-changing request carries the selected name, read by the domain through an `IActorProvider` boundary. No authentication infrastructure. |
| **Rationale** | Approval from the identity that created the ECO is the first thing an interviewer pokes at. Costs little; removes a credibility hole. The boundary keeps the demo shortcut replaceable by authenticated claims later. |
| **Consequences** | The actor is still client-supplied and must never be presented as a security control (§16). Audit events record whichever actor was selected. The boundary is one interface — do not build a full identity abstraction around it. |
| **Spec sections** | §7, §9, §15.4 |

### D-07 — One command starts everything; Docker is the demo mode

| Field | Value |
|---|---|
| **Area** | Delivery / operations |
| **Status** | Confirmed |
| **Decision** | `docker compose up -d --wait` builds and starts the whole application: PostgreSQL 18 and the API, as **two** containers. The API image serves the built SPA alongside `/api`, so there is no separate web server and no CORS. Docker is the reproducible **demo/run mode only** — development keeps using `dotnet run` and `npm run dev` for hot reload. |
| **Rationale** | Explicit instruction from the owner: "docker should be used to run the backend and frontend as well, so a single docker compose command starts everything". Two containers rather than three follows A-16, which already split behaviour by mode (API serves the SPA in demo, Vite dev server in development). Keeping dev outside Docker preserves iteration speed and avoids volume-mount and file-watcher complexity that would buy nothing for a demo. |
| **Consequences** | Host port is **5199** for both modes, so the demo link never changes. The container runs as `Production`, so migrate-on-startup is gated on the `Database:ApplyOnStartup` config flag rather than on `IsDevelopment()`; the container sets it to `true` explicitly. A real deployment would set it `false` and run migrations as a separate release step. The API container owns its schema. The SPA stage in the Dockerfile is deliberately absent until Slice 7 — a build stage with nothing to build fails. |
| **Spec sections** | §4, §9, §13 (Slice 7) |

---

## Assumed defaults

Proposed by the agent, accepted by silence. Each is cheap to reverse **before** the slice that implements it, and increasingly expensive afterwards.

| ID | Area | Assumption | Why it is low risk | Reversal cost | Spec |
|---|---|---|---|---|---|
| A-01 | Domain | Quantity is a positive **integer** | No fractional part appears in the seed data or the demo path | Low — one property type | §5.3, §15.3 |
| A-02 | Persistence | Part numbers trimmed and uppercased; uniqueness enforced by a unique index on the stored column, with case-insensitivity coming from the domain's normalisation | §5 requires case-insensitive uniqueness. **Amended during Slice 2:** the original wording was `upper(part_number)`, a functional index. EF Core cannot express one, and a hand-edited migration would leave the model snapshot disagreeing with the database. Because every write enters through the domain, which upper-cases first, a plain unique index on the normalised value is equivalent in practice. Residual risk: a direct SQL insert bypassing the domain could introduce a case variant — bounded, because no such path exists. Pinned by `IT09_CaseVariantPartNumbersCollideBecauseTheDomainNormalises` and `IT09_PartNumberUniquenessIsEnforcedByTheDatabaseItself` | Low — the migration is the only cost | §5.1, §5.2 |
| A-03 | Domain | `Guid` primary keys; no strongly-typed IDs | §6 explicitly permits ordinary GUIDs | Low | §6 |
| A-04 | Domain | ECO numbers generated as `ECO-{yyyy}-{NNNN}`, zero-padded, restarting yearly, with a unique index and retry on conflict | Matches the §5.4 example; makes creation concurrency-safe without a sequence table | Low before migrations | §5.4 |
| A-05 | Domain | Revision codes `A`…`Z`; the code fails with a domain error rather than rolling over | §5.3 says explicitly not to implement rollover | Low | §5.3 |
| A-06 | Scope | **No DELETE endpoints anywhere**; a draft ECO cannot be cancelled | §2 excludes deletion; documented as a known limitation | Medium — new endpoint + UI | §16 |
| A-07 | Domain | Product and component name/category/part number are **immutable in v1** (no update endpoints) | This is precisely what makes reference-only history truthful. If editing is ever introduced, historical snapshots become mandatory — that is a real design fork, flagged here | High if editing is wanted | §5.1, §5.2, §16 |
| A-08 | Domain | Lifecycle enums exist, but no endpoint transitions product/component status; `Retired` is unreachable in v1 | Nothing on the demo path needs it | Low | §16 |
| A-09 | API | `page` 1-based default 1; `pageSize` default 50, max 200; responses carry `total` | §7 requires the parameters; these are conventional values | Low | §7 |
| A-10 | API | RFC 7807 `ProblemDetails`; every 409 carries a machine-readable `conflictType` | §7 requires a consistent error shape; codes let the UI react without string matching. **Amended during the 2026-10-21 review:** the exception-to-HTTP mapping is now fixed before any endpoint is written — `DomainRuleViolationException` → 400 with `code`; `DomainConflictException` → 409 with `conflictType = Code`; Npgsql `23505` → 409 with a `conflictType` derived from the violated index name; `DbUpdateConcurrencyException` → 409 with `concurrency-conflict`. Written down so endpoints are not invented ad hoc | Low | §7 |
| A-21 | Domain | Error codes name the object that actually failed; no code is shared across aggregates | §7 requires `conflictType` so the UI can react without prose matching. **Added during the 2026-10-21 review:** `blank-part-number` was being returned for six unrelated failures (missing ECO number, base revision, change target, replacement part, revision link, empty BOM), so the contract misdescribed the request. 8 codes added under three groups — field validation, missing required references, state conflicts — plus `duplicate-part-number` / `duplicate-eco-number` / `concurrency-conflict` declared for the API's 23505 mapping. Also: `revision-limit-reached` moved 400 → 409 because §7 lists it among the `409` conflict types, and an empty BOM revision reports `bom-lines-required` rather than `eco-lines-required`. Changing a code is an API contract change, which is why this landed before any endpoint existed | Low, contract-visible | §7 |
| A-11 | Delivery | EF migrations committed; `Database.Migrate()` plus idempotent seed on startup in Development | Simplest correct story for a demo | Low | §12 |
| A-12 | Delivery | Seed uses **fixed GUIDs** so re-application is a no-op | §10 requires idempotence | Low | §10 |
| A-13 | Domain | Audit covers the seven §5.5 actions, written in the same transaction, read-only endpoint | §5.5 is explicit | Low | §5.5 |
| A-14 | UX | Routes: `/products`, `/products/:id`, `/ecos`, `/ecos/new`, `/ecos/:id` | §9's five screens map one-to-one | Low | §9, §14 |
| A-15 | UX | UTC stored, local time displayed, UTC on hover | §3 already mandates this | Low | §3 |
| A-16 | Delivery | Dev: Vite proxy to API. Demo: SPA served by the API host, single origin | Removes CORS from the live-demo risk list | Low | §4 |
| A-17 | Persistence | `xmin` token on `Product` + partial unique index `bom_revisions(product_id) WHERE status='Released'` | §8 authorises exactly this strategy; PostgreSQL supports partial indexes natively | Low, but must be *proved* by IT-06/IT-10, not asserted | §8 |
| A-18 | Domain | `BomRevisionStatus.Draft` exists but is never produced in v1 | Keeps the spec's enum consistent while being honest about unused values | Low | §5.3, §16 |
| A-19 | API | No API versioning, no idempotency keys, no cancellation plumbing | Out of §2 scope | Low | §2 |
| A-20 | Delivery | Gates: `dotnet build` + `dotnet test` + `npm run build` (tsc + vite) | Minimum honest gates for this scope | Low | §11, §12 |

## Superseded

Nothing yet. When a confirmed decision is replaced, the original entry moves here with the date, the reason, and the new ID.
