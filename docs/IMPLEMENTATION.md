# Forge PLM — Implementation Status

**Living document.** Updated at the end of every slice. Never claims a test passed unless it was actually run.

- **Last updated:** 2026-10-21 (end of Slice 7)
- **Current phase:** Slices 1–7 complete — ECO write path, audit read, and the live frontend; Slice 8 (tests, errors, README) remains
- **Build state:** `dotnet build ForgePlm.slnx` — succeeded, 0 warnings, 0 errors (`TreatWarningsAsErrors` is on). `npm run build` in `frontend/` — passes.
- **Test state:** `dotnet test ForgePlm.slnx` — **137 passed, 0 failed** (1 m 34 s; 94 domain + 22 integration against real PostgreSQL + 21 API). Frontend: `npm test` — **4 files, 37 tests passed** (Vitest).
- **Run state:** `docker compose up -d --wait` — both containers healthy on port 5199. The backend image was rebuilt from the committed tree for Slice 7, and `/api/products`, `/api/ecos` and `/api/audit` all answer 200. It predates the uncommitted backend edits now in the working tree (see §10). The protected path was driven end to end in a browser against this container.

> Note: .NET 10 writes the solution as `ForgePlm.slnx` (XML solution format), not `.sln`. Use `dotnet build ForgePlm.slnx`.

---

## 1. End goal

A small, credible PDM / Engineering Change Management prototype, built to be **demoed in an interview** and to **demonstrate PLM concepts correctly** (D-02).

The protected journey that must work end-to-end through the UI:

```text
Products list → SERVER-100 detail (released rev B)
  → create ECO replacing MB-020 with MB-021
  → submit
  → approve      (rev C becomes Approved; rev B untouched)
  → release      (rev C Released + effective date; rev B Superseded)
  → audit timeline shows the whole story
```

Explicitly *not* the goal: DDD pedagogy for its own sake, breadth of features, production readiness (D-02, D-03).

Definition of done: `Forge-PLM-Spec.md` §12.

---

## 2. Phases and steps

| Phase | Steps | Status |
|---|---|---|
| **0. Requirements & architecture interview** | Interview (D-01…D-06 + A-01…A-20); revise spec; write ADRs; write decision register | ✅ complete |
| **1. Domain model** | Solution + projects; `Product`, `Component`, `BomRevision`, `EngineeringChangeOrder`, `AuditEvent`; enums; transition methods; domain unit tests | ✅ **complete — 84 tests passing** |
| **2. Persistence** | Npgsql + `ForgePlmDbContext`; entity configurations and constraints; migrations; idempotent seed; `docker-compose.yml` | ✅ **complete — 7 integration tests passing** |
| **2b. Containerised delivery** | Multi-stage Dockerfile for the API; `backend` service in compose; config-gated migrate-on-startup; `.dockerignore`; SPA-serving guard | ✅ **complete — cold start verified** |
| **3. Product & BOM read endpoints** | Products list/detail, BOM revisions, current revision, revision lines; pagination; ProblemDetails | ✅ **complete — 10 API tests passing** |
| **4. ECO creation & submission** | Create ECO (captures base released revision), edit draft, submit; actor switch plumbing | ✅ **complete** |
| **5. Approval transaction** | Approve ECO → copy base lines → apply changes → new `Approved` revision, all in one transaction; stale-base 409 | ✅ **complete** |
| **6. Rejection & release** | Reject with reason; release → supersede + effective date + `Applied`; concurrency conflict test | ✅ **complete** |
| **7. Frontend** | Vite + React + TS + Tailwind; shell, Products list, Product detail, ECO list, ECO detail, Create/Edit ECO | ✅ **complete — live API; `npm run build` and 37 unit tests pass; protected path verified in a browser** |
| **8. Tests, errors, README** | Postgres-backed integration tests; error states; README with commands, decisions, limitations | 🟡 backend tests complete; `frontend/README.md` added; root README and error-state polish pending |

Slices are delivered in order. Nothing in the protected journey is cut to save a slice.

---

## 3. What's going right

- **The spec was already good.** `Forge-PLM-Spec.md` anticipated most domain traps (approval ≠ release, immutable released revisions, stale-base detection, same-transaction audit). Phase 0 mostly *confirmed* it rather than rewriting it.
- **The one real conflict was caught before code existed.** The ECO-details mockup said approval releases automatically; the spec said approval and release are separate. D-04 settled it in favour of the spec, before any UI copy was ported.
- **Every deferred decision has been resolved.** §15's seven open decisions are closed, including the database, which the spec left genuinely open.
- **The environment is known and verified, not assumed.** .NET 10 SDK, Node 22, Docker all confirmed working; PostgreSQL chosen and version-checked against the current stable release.
- **Scope is protected by a written rule** (D-02c), so "just one more feature" has something concrete to push back with.
- **The mockup surface was deliberately shrunk** from 6 screens + dashboards to 5 screens + 2 nav entries, deleting every widget with no domain source. Credibility per hour spent.

### Risks to watch

| Risk | Mitigation |
|---|---|
| Demo pressure expands scope (mockups make everything look in-scope) | D-02c protected path; §16 of the spec; this document's phase table |
| "Effective date is only a label" is challenged as unrealistic | ADR-003 records the trade-off and the exact reason (no scheduler in scope) |
| Concurrency claims outrun the tests | ADR-004; integration tests run against real PostgreSQL, InMemory provider forbidden for such claims |
| `xmin` + partial index behaves differently than expected | Slice 6 proves it with the competing-ECO test before the UI depends on it |
| Few sessions available → frontend is the squeeze point | Backend slices 1–6 are strictly smaller than the frontend; keep slice 7 thin (no component library, no router extras) |

---

## 4. Unit and integration tests against the spec

Status: ⬜ not written · 🟡 written, not run · ✅ run and passing · ❌ run and failing

### Domain tests (spec §11, "Minimum domain tests") — `ForgePlm.Tests`, no database

| # | Spec requirement | Test | Status |
|---|---|---|---|
| DT-01 | Blank part numbers and names are rejected | `PartNumberTests.DT01_*`, `ProductTests.DT01_*`, `ComponentTests.DT01_DT14_*` | ✅ |
| DT-02 | BOM line quantity must be positive | `BomRevisionTests.DT02_*`, `QuantityTests.DT02_*`, `EngineeringChangeOrderTests.DT02_*` | ✅ |
| DT-03 | Released BOM revision cannot be modified | `BomRevisionTests.DT03_*` (approved + released + double-release) | ✅ |
| DT-04 | Only a draft ECO can be submitted | `EngineeringChangeOrderTests.DT04_*` | ✅ |
| DT-05 | Only a submitted ECO can be approved or rejected | `EngineeringChangeOrderTests.DT05_*` | ✅ |
| DT-06 | Rejection requires a reason | `EngineeringChangeOrderTests.DT06_*` (null / empty / whitespace) | ✅ |
| DT-07 | Approved ECO cannot be approved again | `EngineeringChangeOrderTests.DT07_*` | ✅ |
| DT-08 | ECO cannot be approved if its base revision is no longer current | `EngineeringChangeOrderTests.DT08_*` | ✅ |

### Additional domain rules worth pinning (spec §5, §6)

| # | Rule | Test | Status |
|---|---|---|---|
| DT-09 | Retired component cannot be added to a newly proposed or released BOM | `ComponentTests.DT09_*` | ✅ |
| DT-10 | At most one component per BOM revision | `BomRevisionTests.DT10_*` | ✅ |
| DT-11 | Revision codes advance `A → B → C`; past `Z` fails instead of rolling over | `RevisionCodeTests.DT11_*` | ✅ |
| DT-12 | Only a `Submitted` ECO may be approved; only a `Draft` ECO may be edited | `EngineeringChangeOrderTests.DT12_*` | ✅ |
| DT-13 | Release requires an effective date; future dates are accepted | `BomRevisionTests.DT13_*` | ✅ |
| DT-14 | Blank part number / name rejected after normalisation (whitespace-only) | folded into DT-01 cases | ✅ |

### Further domain behaviour pinned by Slice 1 (not required by §11, kept because it protects the demo)

| Rule | Test | Status |
|---|---|---|
| Product part number normalised to upper case and trimmed | `ProductTests.PartNumberIsStoredNormalised` | ✅ |
| Retired product cannot receive a newly released revision | `ProductTests.RetiredProductCannotReceiveANewlyReleasedRevision` | ✅ |
| Only an active product can receive an ECO | `ProductTests.OnlyAnActiveProductCanReceiveAnEco` | ✅ |
| Blank ECO title / blank actor rejected | `EngineeringChangeOrderTests.ABlankEcoTitleIsRejected`, `ABlankActorIsRejected` | ✅ |
| Replacing a component with itself rejected | `EngineeringChangeOrderTests.ReplacingAComponentWithItselfIsRejected` | ✅ |
| Target must exist in the base revision at submission | `EngineeringChangeOrderTests.SubmissionRejectsATargetThatIsNotInTheBaseRevision` | ✅ |
| Approval builds the new revision and leaves the base untouched | `EngineeringChangeOrderTests.ApprovalRevisionAppliesTheReplacementAndPreservesEveryOtherLine`, `BuildingTheApprovedRevisionLeavesTheBaseRevisionUntouched` | ✅ |
| Replacement inherits the existing line's quantity | same as above (`Quantity == 1`) | ✅ |
| A stale replacement that would duplicate an existing component is rejected | `EngineeringChangeOrderTests.AStaleReplacementThatDuplicatesAnExistingComponentIsRejected` | ✅ |
| Quantity change keeps the component and sets the new quantity | `EngineeringChangeOrderTests.AQuantityChangeProducesTheNewQuantityAndKeepsTheComponent` | ✅ |
| Approved revision links back to its ECO | `TheApprovedRevisionLinksBackToItsEco` | ✅ |
| Only an approved ECO can be applied | `OnlyAnApprovedEcoCanBeApplied`, `MarkingAppliedKeepsTheApprovedRevisionLink` | ✅ |
| A draft ECO can be edited; a submitted one cannot; a rejected edit changes nothing | `ADraftEcoCanBeEdited`, `DT12_ASubmittedEcoCannotBeEdited`, `ABadDraftEditDoesNotChangeTheEco`, `ABadDraftLineSetDoesNotChangeTheEco` | ✅ |
| Audit event records actor/action/entity/time; actor is mandatory | `AuditEventTests.*` | ✅ |

### Integration tests (spec §11, "Minimum application/integration tests") — real PostgreSQL

| # | Spec requirement | Status |
|---|---|---|
| IT-01 | Creating an ECO captures the current released revision | ✅ `EcoWorkflowTests.IT01_*`, `EcoApiTests.AStaleClientCannotChooseTheBaseRevisionOrTheEcoNumber` |
| IT-02 | Approval creates a new revision with the requested change applied | ✅ `EcoWorkflowTests.IT02_IT03_*` |
| IT-03 | The old released revision remains unchanged after approval | ✅ `EcoWorkflowTests.IT02_IT03_*` |
| IT-04 | Release supersedes the previous revision and releases the new one atomically | ✅ `EcoWorkflowTests.IT04_*` |
| IT-05 | Rejecting an ECO does not create a new BOM revision | ✅ `EcoWorkflowTests.IT05_*` |
| IT-06 | Two competing ECOs based on the same revision cannot both release | ✅ `EcoWorkflowTests.IT06_*` (2 tests: competing release + xmin), `EcoApiTests.TwoCompetingEcosCannotBothRelease` |
| IT-07 | Failed approval/release rolls back all associated changes | ✅ `EcoWorkflowTests.IT07_*` (2 tests, PostgreSQL triggers) |
| IT-08 | Seed data is idempotent (second application changes nothing) | ✅ |
| IT-09 | Unique part-number constraint is enforced case-insensitively by the database | ⚠️ see note |
| IT-10 | At most one `Released` revision per product, enforced by the partial unique index | ✅ |
| IT-11 | Release ordering: supersede-then-release is load-bearing and must be sequenced | ✅ |

> **IT-09 correction.** The spec's phrasing implies the *database* is case-insensitive. It is not:
> the index is a plain unique index on the stored column and is therefore case-sensitive.
> Case-insensitivity comes from the domain normalising part numbers to upper case at every entry
> point, so `server-100` and `SERVER-100` reach the column as the same value and collide.
> Slice 2 found this by writing the case variant straight through EF and getting *no* exception.
> Two tests now pin both halves — `IT09_CaseVariantPartNumbersCollideBecauseTheDomainNormalises`
> and `IT09_PartNumberUniquenessIsEnforcedByTheDatabaseItself`. See decision **A-02** for the
> recorded mechanism and its residual risk.

> **IT-11 — the release-ordering question, measured.** A code review predicted that releasing a
> new revision while the old one was still `Released` would hit the partial unique index, because
> EF Core gives no ordering guarantee for two updates to the same table. That is the right *risk*
> but the wrong *prediction*: measured on 2026-10-21 against EF Core 10 + PostgreSQL 18, a single
> `SaveChanges` doing both updates did **not** fail — stably across five runs — because EF happened
> to issue the supersede first. That is an accident of batching, not a contract, so Slice 6
> sequences the two statements inside one explicit transaction. Both halves are now pinned by
> `IT11_ReleasingTheNewRevisionBeforeSupersedingTheOldOneViolatesTheIndex` (the constraint is real)
> and `IT11_OrderedTwoStepReleasePersistsAtomicallyInOneTransaction` (the design that satisfies it).
> See [`adr/ADR-004`](adr/ADR-004-concurrency-xmin-partial-index.md).

> **IT-06 and IT-10 ran against PostgreSQL, not InMemory.** The suite has no InMemory provider at
> all (ADR-001/ADR-004), so every integration test creates a real database, applies the real
> migration, and asserts from a fresh `DbContext`. The evidence is in the run, not the claim:
> `dotnet test --filter "FullyQualifiedName~IT06|FullyQualifiedName~IT10"` passed 3/3 (2 IT-06
> tests + IT-10) on 2026-10-21. IT-06 has two halves: two ECOs approved from the same base cannot
> both release (`stale-base-revision`), and two contexts that read the same product cannot both
> commit because `xmin` rejects the loser with `DbUpdateConcurrencyException`. IT-10 proves the
> partial unique index refuses a second `Released` row even when the domain is bypassed.

### Frontend

| # | Check | Status |
|---|---|---|
| FE-01 | `npm run build` (tsc + vite) passes | ✅ `npm run build` passes; `npm test` 4 files / 37 tests |
| FE-02 | Invalid actions disabled per ECO status (no fake status changes in the client) | ✅ matrix in `frontend/src/lib/ecoStatus.ts`; checked on Draft, Submitted, Approved, Rejected and Applied |
| FE-03 | Approve / reject / release require confirmation | ✅ `ConfirmDialog` shows the current actor; checked for approve and reject |

> Verified in a browser against the rebuilt container: create ECO (MB-020 → MB-021) → submit →
> approve → release → **Applied**; the product advanced to revision C, its BOM shows MB-021, and
> its Revisions tab shows C `Released` / B `Superseded`. A second ECO covered draft edit (PUT),
> submit and reject. Screens C/D/E no longer use a mock adapter.

### Gates run before every slice is called done

`dotnet build` · `dotnet test` · `npm run build`

---

## 5. Decision summary

Full register with rationale and consequences: [`DECISIONS.md`](DECISIONS.md).
Rationale for the load-bearing choices: [`adr/`](adr/).

- **Confirmed (9):** D-01 structure · D-02 goals/budget · D-02c protected path · D-03 screens + mockup look · D-04a approve/release separation · D-04b effective date as a label · D-05 PostgreSQL 18 in Docker · D-06 two-person actor switch · D-07 one command starts everything (D-07 was added in the containerisation step, after Phase 0)
- **Assumed (21):** A-01…A-21, low-risk engineering defaults, documented in `DECISIONS.md`. A-21 was added during the 2026-10-21 code review, so the count was 20 at Phase 0 — which is why the session log entry below still says 20.

---

## 6. Slice 1 — what was built (2026-10-21)

```text
ForgePlm.slnx                                # .NET 10 writes .slnx, not .sln
src/ForgePlm.Domain/
  Common/DomainException.cs                   # DomainRuleViolation (400) vs DomainConflict (409) + 20 error codes
  Common/PartNumber.cs                        # trimmed, upper-cased, ≤50 chars
  Common/Quantity.cs                          # positive int
  Common/RevisionCode.cs                      # A…Z, fails past Z
  Products/Product.cs                         # + EnsureCanReceiveNewlyReleasedRevision, EnsureIsActiveForChangeProposal
  Components/Component.cs                     # + EnsureCanBeUsedInNewBom
  BillOfMaterials/BomRevision.cs              # immutable lines; Release, MarkSuperseded, CreateApproved, CreateReleasedBaseline
  BillOfMaterials/BomLine.cs
  EngineeringChanges/EngineeringChangeOrder.cs # Submit, Approve, Reject, MarkApplied, BuildApprovedRevision, staleness check
  EngineeringChanges/EngineeringChangeLine.cs  # Replace / ChangeQuantity + ApplyTo
  Audit/AuditEvent.cs                         # append-only, 7 action constants
tests/ForgePlm.Tests/                         # 84 tests
```

### Design decisions taken inside Slice 1 (beyond the register)

These are small, but each one is a place where a later slice could quietly disagree with the domain, so they are written down.

| Decision | Why |
|---|---|
| **Two exception types, not one.** `DomainRuleViolationException` → 400, `DomainConflictException` → 409, each carrying a stable `Code` | §7 requires 400 vs 409 to be distinguishable, and A-10 requires a machine-readable `conflictType`. Two types make the mapping structural instead of a string match on a message. |
| **The 20 error codes are a closed set in `DomainErrorCodes`** | The UI keys off them. Adding one is an API contract change, which is the point. |
| **Value objects normalise at the boundary, entities store plain types.** `PartNumber.From()` returns a value object, but `Product.PartNumber` is a `string` | Keeps EF Core free of value conversion in Slice 2, while keeping normalisation and validation in exactly one place. Cheapest enforcement that still holds. |
| **`BomRevision.CreateApproved` is the only way to create a revision**, and it requires non-empty lines | An empty revision cannot exist, so no later code path has to check for one. |
| **`EngineeringChangeOrder.BuildApprovedRevision` owns "apply the changes to a copy"** | Keeps the copy-and-modify rule in the aggregate that has the lines, rather than in an application service that would have to reach into two aggregates' internals. |
| **Component activity checks stay out of the domain's cross-aggregate path** | `BuildApprovedRevision` cannot see `Component` rows (different aggregate). The application service checks activity in Slice 5; the domain checks everything decidable from the base revision alone. This is the §5.4 "where does cross-aggregate validation live" answer, and it is deliberate. |
| **`CreateReleasedBaseline` exists only for the seed**, and sets no `SourceEcoId` | The seeded revision B genuinely has no originating ECO. Pretending otherwise would put a lie in the audit trail. |

### Known rough edges (not bugs, recorded so they are not rediscovered)

- `Product.Category` and `Component.Category` default to **empty string** when omitted. The UI will need to decide whether to render a placeholder; the domain deliberately does not invent a category.
- `EngineeringChangeOrder.MarkApplied` uses `DecidedAtUtc ??= utcNow`, so approval time wins over release time. Correct, but subtle.
- **No test covers a *concurrent* approval yet** — that is IT-06/IT-10, delivered in Slice 6 and
  now proved against PostgreSQL (see §4 and §10).

---

## 7. Slice 2 — persistence (2026-10-21)

```text
docker-compose.yml                             # postgres:18, healthcheck, named volume
.config/dotnet-tools.json                      # dotnet-ef pinned as a local tool
src/ForgePlm.Api/
  Program.cs                                   # DI, Npgsql, dev-only OpenAPI, migrate+seed on startup
  appsettings.json                             # connection string matching the compose file
  Persistence/ForgePlmDbContext.cs             # 7 DbSets, snake_case naming pass
  Persistence/Configurations.cs                # IEntityTypeConfiguration per aggregate
  Persistence/Migrations/                      # 20261021173325_InitialCreate
  Seed/DemoSeed.cs                             # fixed-GUID, idempotent
tests/ForgePlm.Tests/Integration/
  PostgresFixture.cs                           # one database per test, created + dropped around it
  PersistenceTests.cs                          # IT-08, IT-09 (×2), IT-10 + 3 supporting
```

### What the schema enforces

The constraints that carry a domain rule are in the database, not only in code:

| Constraint | Mechanism | Why it is in the schema |
|---|---|---|
| At most one `Released` revision per product | partial unique index on `product_id` `WHERE status = 'Released'` | ADR-004 — the invariant protecting the demo's historical-integrity claim. A code path could be bypassed; the index cannot. |
| Revision codes unique per product | unique `(product_id, revision_code)` | §5.3 — `A → B → C` cannot fork. |
| One component per revision | unique `(bom_revision_id, component_id)` | Belt and braces with the domain rule in §5.3. |
| `ECO-2026-0042` never reused | unique `eco_number` | A-04. |
| Part numbers unique | unique `part_number` | §5.1, via domain normalisation (see **A-02** below). |
| Optimistic concurrency on `Product` | `xmin` as a concurrency token | ADR-004 — the guard behind Slice 6's competing-ECO test. |

### Design decisions taken inside Slice 2

| Decision | Why |
|---|---|
| **`xmin` mapped by hand, not by an extension.** Three plain EF calls (`IsConcurrencyToken` + `ValueGeneratedOnAddOrUpdate` + `HasDefaultValueSql("xmin")`) on a shadow property | Npgsql EF Core 10 **removed** `UseXminAsConcurrencyToken()`. Its implementation was exactly those three calls, so replicating them keeps ADR-004's guarantee rather than quietly dropping it. Verified in the generated migration: `type: "xid", rowVersion: true, defaultValueSql: "xmin"` — byte-for-byte what the removed helper produced. The shadow property keeps `xmin` out of the domain model. |
| **`bom_revisions.source_eco_id` has no foreign key** | The schema has a genuine cycle: `ecos.approved_revision_id → bom_revisions` and `bom_revisions.source_eco_id → ecos`. Only the ECO→revision direction is kept as a real FK, which is also the direction that guarantees the relationship. `source_eco_id` stays an audit back-reference. |
| **Enums stored as strings** | `HasConversion<string>()` throughout. The partial index's `WHERE status = 'Released'` reads correctly in `psql`, and there is no ordinal/enum-order mismatch to get wrong. |
| **Snake_case naming applied as a model pass, not a package** | A naming-convention package would be a fifth dependency for one job. A 20-line `ToSnakeCase` keeps the schema readable in `psql` without it. |
| **One database per test, not one per run** | The first version shared a database across the whole test collection; a test inserting its own `SERVER-100` then made every other test's seed fail. Isolation is what lets tests be independent of execution order. Verified: only `forge_plm` remains on the container after a run. |
| **No ECO is seeded** | D-02c makes creating the ECO the first act of the demo. Seeding one would either duplicate the user's action or have to be deleted first. |

### Bugs Slice 2 found (both caught by tests that existed to catch them)

1. **The seed duplicated its audit row on every startup.** The helper took an `id` for the
   existence check and a separate `Func<T>` factory that minted its own `Guid`, so the check
   looked for an id the created row never had. Components and the product were fine — they
   passed the id through — which is why only `audit` doubled. Fixed *and* made
   unrepresentable: the factory now receives the id (`Func<Guid, T>`), so a call site cannot
   supply one id to the check and another to the entity. Caught by **IT-08**.
2. **IT-09's own claim was false.** Asserting that the *index* catches a case variant written
   directly through EF produced "no exception was thrown" — the index is case-sensitive. The
   test was corrected to pin both real facts (see the IT-09 note in §4), and **A-02** was
   amended rather than the test weakened.

### One command starts everything (D-07)

`docker compose up -d --wait` builds and starts the whole application from an empty machine.
Verified by a genuine cold start — `down -v` first, so nothing was carried over:

```text
Network  forge_plm_engine_default   Created
db       forge-plm-db               Started → Healthy
backend  forge-plm-backend          Started → Healthy
$ curl http://localhost:5199/health
{"status":"ok"}
```

**Two containers, not three.** The API image serves the built SPA alongside `/api` (A-16), so
there is no separate web server and no CORS to configure. The SPA stage is deliberately absent
from the Dockerfile until Slice 7 — a build stage with nothing to build fails, and the guard in
`Program.cs` (`MapFallbackToFile` only registered when `wwwroot/index.html` exists) means the
backend starts cleanly before the frontend does.

**Port 5199 in both modes.** The container maps host `5199` → container `8080`, matching the
local dev URL, so the demo link never changes between running it in Docker and running it with
`dotnet run`.

| Piece | Why |
|---|---|
| Multi-stage Dockerfile | Restore is cached on its own layer, so editing C# does not re-download packages. `UseAppHost=false` drops the platform-specific launcher. |
| Build context is the repo root | The API references `..\ForgePlm.Domain`, so both projects must be inside the context. `.dockerignore` excludes `bin/`, `obj/`, `tests/`, `docs/`, `mockups/`. |
| `depends_on: condition: service_healthy` | The API migrates at startup; connecting to a database still initialising is the classic first-run failure. |
| `curl` in the runtime image | The aspnet image ships no HTTP client. `--wait` plus the healthcheck is what makes the command return with the app *ready* rather than mid-migration. |
| `Database__ApplyOnStartup=true` | The container runs as `Production`, so the migrate gate had to move from `IsDevelopment()` to a config flag. Explicit here so it is visible that the container owns its schema. |

Container startup is idempotent: restarting `forge-plm-backend` left a checksum of every table
count unchanged, because `Migrate()` is a no-op once applied and the seed checks by primary key.

### Known rough edges carried forward

- **A-02's mechanism differs from its original wording.** The spec said uniqueness would be
  enforced on `upper(part_number)`. It is enforced on the stored column instead, and
  case-insensitivity comes from domain normalisation. Residual risk: a direct SQL insert that
  bypassed the domain could introduce a case variant. All writes in this system go through the
  domain, so the risk is bounded and recorded.
- `Product.UpdatedAtUtc` was creation-only after Slice 2; Slice 5/6 now maintains it
  through `MarkConfigurationChanged`.
- No concurrency test in Slice 2. IT-06/IT-10 were delivered in Slice 6 and pass against
  PostgreSQL.
- `dotnet ef database update` logs a red-looking `fail:` line on a genuinely fresh database
  (the history-table query runs before the table exists), then succeeds. Harmless, but noisy
  enough to be worth knowing about.

---

## 8. Code review response (2026-10-21)

An independent review raised four items. Each was checked against the code and the spec before
acting on it; the outcome of each is recorded here rather than being silently absorbed.

| # | Claim | Verdict | Action |
|---|---|---|---|
| 1 | Releasing a new revision in the same `SaveChanges` as superseding the old one hits the partial unique index | **Right risk, wrong prediction** | Measured; documented in ADR-004 and IT-11. Slice 6 ships the ordered design. |
| 2 | `blank-part-number` is returned for six unrelated failures, so the error contract lies to the UI | **Credible, verified** | Fixed: 8 specific codes added, 6 throw sites corrected. |
| 3 | Two ECO lines targeting the same component are silently merged; approval loses one of them | **Credible, verified** | Fixed: duplicate targets rejected at `ReplaceLines`, with a test. |
| 4 | `Lines` exposes the backing list, so a "released, immutable forever" revision can be mutated through a cast | **Credible, verified** | Fixed: `_lines.AsReadOnly()`, with tests. |

### Item 1 — release ordering, and why the review's prediction was wrong

The review predicted a single `SaveChanges` would throw. That was tested rather than debated: a
released `B` and an approved `C` for one product, both persisted, then `B → Superseded` and
`C → Released` in one call. **It did not throw** — stably across five runs — because EF Core
happened to issue the supersede first.

That does not make the review moot. EF Core guarantees ordering only for *dependent* commands; two
updates to the same table carry no such guarantee, so the result is an accident of batching that a
patch release could change. And the constraint itself is real and cannot be deferred: PostgreSQL
allows `DEFERRABLE` only on table constraints, not on a partial index, so there is no schema-side
escape. Slice 6 therefore sequences supersede → save → release → save inside one explicit
transaction, and both halves are pinned by tests:

- `IT11_ReleasingTheNewRevisionBeforeSupersedingTheOldOneViolatesTheIndex` — the constraint is live,
  including for the release-time shape (a second `Released` row), which IT-10 does not cover because
  IT-10 conflicts on an `INSERT`.
- `IT11_OrderedTwoStepReleasePersistsAtomicallyInOneTransaction` — the design that satisfies it,
  verified against a fresh context so the assertions read the database rather than the change
  tracker.

Recording the failed prediction matters more than agreeing with it: the next person to read the
review should see what was actually measured.

### Item 2 — the error taxonomy was lying

`blank-part-number` was returned for a missing ECO number, a missing base revision, a missing change
target, a missing replacement part, a missing revision link, and an empty BOM revision. None of
those is a part number, and the UI is specified to key off these strings, so the contract was
actively misleading. Eight codes were added under three groups — field validation, missing required
references, and state conflicts — and the six throw sites corrected.

Three related fixes came out of the same pass:

- **`duplicate-part-number` now exists.** Spec §7 names it as a `conflictType`, but nothing could
  emit it. It is declared for the API layer to use when PostgreSQL `23505` maps to a `409`, alongside
  `duplicate-eco-number` and `concurrency-conflict`. None is thrown by the domain; they exist so the
  mapping has one home and the contract stays closed before any endpoint is written.
- **`revision-limit-reached` moved from `400` to `409`.** Spec §7 lists it among the `409`
  `conflictType` examples, but it was thrown as a `DomainRuleViolationException`, which the API maps
  to `400`. The request is well-formed; the product's revision history is simply exhausted — that is
  a conflict. Spec wins, per the precedent set by D-04a.
- **An empty BOM revision reports `bom-lines-required`,** not `eco-lines-required`. Two different
  aggregates were sharing one code, so the taxonomy could not say which object was at fault.

**Mapping plan for Slice 3 onward** (spec §7, §11): `PostgresException` with `SqlState = 23505` →
`409` with the `conflictType` derived from the violated index name; `DbUpdateConcurrencyException`
→ `409` with `concurrency-conflict`; `DomainRuleViolationException` → `400` with `code`;
`DomainConflictException` → `409` with `conflictType = Code`. Written down now so the endpoints are
not invented ad hoc.

### Item 4 — the immutability claim was only skin deep

`BomRevision.Lines` and `EngineeringChangeOrder.Lines` returned the backing `List<T>` typed as
`IReadOnlyList<T>`, so a caller could cast back and mutate a released revision — the exact thing the
demo's historical-integrity claim rests on not being possible. Both now return `_lines.AsReadOnly()`.

EF is unaffected: the configuration sets `HasField("_lines")` with `PropertyAccessMode.Field`, so
EF populates the field directly and never touches the property. The integration tests confirm it.

One correction to the *test*, not the fix: the tests initially expected a `NotSupportedException`
on mutation. The actual behaviour is stronger — `ReadOnlyCollection<T>` cannot be cast to `List<T>`
at all, so the cast throws `InvalidCastException` and there is never an object to mutate. The
assertions were corrected to match the real, better behaviour rather than the designed-for one.

---

## 9. Slice 3 — product & BOM read endpoints (2026-10-21)

The first endpoints, and the first place the API contract becomes real.

```text
src/ForgePlm.Api/Contracts/Paging.cs          # PageRequest + PagedResponse<T>
src/ForgePlm.Api/Contracts/ReadModels.cs      # DTOs and their projections
src/ForgePlm.Api/Http/ErrorResponseMapper.cs  # one failure -> RFC 7807 mapping + handler
src/ForgePlm.Api/Endpoints/ListQuery.cs       # shared filter parsing
src/ForgePlm.Api/Endpoints/ProductEndpoints.cs
src/ForgePlm.Api/Endpoints/ComponentEndpoints.cs
src/ForgePlm.Api/Endpoints/BomRevisionEndpoints.cs
src/ForgePlm.Api/Program.cs                   # exception handler + endpoint registration
tests/ForgePlm.Tests/Api/ProductApiTests.cs   # 10 HTTP tests on the real pipeline
```

Endpoints implemented (spec §7):

| Method | Path |
|---|---|
| GET | `/api/products?search=&status=&page=&pageSize=` |
| GET | `/api/products/{productId}` |
| GET | `/api/components?search=&status=&page=&pageSize=` |
| GET | `/api/components/{componentId}` |
| GET | `/api/products/{productId}/bom-revisions` |
| GET | `/api/products/{productId}/bom-revisions/current` |
| GET | `/api/bom-revisions/{revisionId}` |
| GET | `/api/bom-revisions/{revisionId}/lines` |

`POST /api/products` and `POST /api/components` are deferred to Slice 8: the spec marks them as
API-completeness only ("No `Create Product` UI in v1"), and nothing on the demo path calls them.

### The error mapping was built before the endpoints, on purpose

The review's third finding was that the error contract lied. The fix that matters is not more
codes — it is that the *mapping* exists in one place before anything can bypass it.
`ErrorResponseMapper.For` owns it, and `ForgePlmExceptionHandler` is registered as the framework's
`IExceptionHandler`, so an endpoint cannot forget to use it: it simply lets exceptions propagate.

| Failure | Status | Payload |
|---|---|---|
| `DomainRuleViolationException` | 400 | `code` |
| `DomainConflictException` | 409 | `conflictType = Code` |
| `DbUpdateConcurrencyException` | 409 | `concurrency-conflict` |
| `23505` unique violation | 409 | `conflictType` from the violated **index name** |
| anything else | 500, logged | — |

Two details worth recording:

- **Index name, not message text.** PostgreSQL reports which index was violated. Translating that
  keeps the mapping stable across locale and PostgreSQL releases; matching on `err.message` would
  not. Six indexes are mapped, and an unknown one degrades to `duplicate-value` rather than
  throwing inside the error handler.
- **`app.UseExceptionHandler()` is called explicitly.** `AddExceptionHandler` alone was not
  enough: `WebApplication.CreateBuilder` adds the *developer exception page* in Development, and
  it sits outer in the pipeline, so it swallowed every exception first. The symptom was a 500
  with a stack trace where the contract promised a 400 — two different behaviours for one
  endpoint, selected by an environment flag. Found by a test, not by inspection.

### Design decisions taken inside Slice 3

- **Bad filter values are 400s, not empty lists.** `?status=Retierd` returns
  `invalid-filter-value`. A 200 with zero items would tell the UI "nothing has that status", which
  is a different fact from "you misspelled it".
- **Paging is clamped, not rejected.** `pageSize=1000` becomes 200; `page=-3` becomes 1. The cap
  protects the server, which clamping achieves; refusing punishes a caller for a value the server
  could simply answer.
- **Search uses `ToLower().Contains()`, not Npgsql's `ILike`.** `ILike` treats `%` and `_` in the
  user's input as wildcards, so searching for `SERVER_100` would match `SERVER-100`. Escaping that
  correctly costs more code than it is worth, and ADR-001 rules out a second provider.
- **`/bom-revisions/current` returns its lines too.** The spec splits current and lines into two
  endpoints, and both still exist — but the product detail screen, the first stop on the protected
  path (D-02c), cannot render anything useful from a revision header. Additive, so nothing breaks.
- **Enums go out as strings.** The database already stores them that way, and strings keep the
  contract readable if a value is ever renamed; a numeric enum would change meaning silently.
- **Never EF entities.** Spec §7 says so outright, and it matters here: serialising a
  `BomRevision` would hand the client the aggregate's internal line collection.

### A test-infrastructure bug worth knowing about

The first run of the API tests failed intermittently — `3D000: database does not exist`, from the
startup seed, roughly two times in three. It was not the code under test.

`PostgresFixture.DisposeAsync` sweeps **every** `forge_plm_test_%` database on the server, which
is correct for a collection fixture disposed once at the end. The new API tests constructed their
own `PostgresFixture` per test method, and xUnit runs test classes in parallel — so each test's
disposal was dropping the database a concurrently running test was still using.

Fixed by putting the class in `PostgresCollection` so it shares the one fixture and the sweep runs
once, when the collection finishes. Verified over five consecutive full-suite runs.

This is the second time a test's own isolation mechanism has been the bug rather than the
subject — the same shape as the Slice 2 seed-duplication finding. Worth remembering that
"flaky" usually means "two tests share something", not "one test is wrong".

### Known rough edges carried forward

- `Product.UpdatedAtUtc` was still creation-only at the end of Slice 3. Slice 5/6 now maintains it
  through `MarkConfigurationChanged`, and approval/release write the product row so `xmin`
  participates.
- The 409 paths (`23505`, `DbUpdateConcurrencyException`) were implemented but unreachable until a
  write endpoint existed. They are now exercised by `EcoApiTests.TwoCompetingEcosCannotBothRelease`
  and `EcoWorkflowTests.IT06_*`.
- No OpenAPI document is committed. It is generated in Development only (spec §4) and is not part
  of the contract.

---

## 10. Slices 4–6 — ECO write path and audit read (2026-10-21)

The backend half of the demo is complete: a draft can be created, edited, submitted, approved,
rejected and released, and every action writes its audit row in the same transaction as the
state change.

```text
src/ForgePlm.Api/Application/
  EcoCommands.cs               # NewEco / NewEcoLine / EcoEdit, line factory, UTC normalisation
  EcoNumberGenerator.cs        # ECO-{yyyy}-{NNNN} from the year's highest number
  EcoRules.cs                  # current released revision, replacement activity, next revision code
  CreateEngineeringChange.cs   # server-resolved base revision + bounded number retry
  UpdateEngineeringChange.cs   # draft-only PUT with explicit line reconciliation
  SubmitEngineeringChange.cs
  ApproveEngineeringChange.cs  # ten-step transaction
  RejectEngineeringChange.cs
  ReleaseEngineeringChange.cs  # supersede -> save -> release -> save
  EcoQueries.cs                # list/detail projections with denormalised component refs
  AuditQueries.cs
src/ForgePlm.Api/Contracts/EcoModels.cs
src/ForgePlm.Api/Endpoints/EcoEndpoints.cs
src/ForgePlm.Api/Endpoints/AuditEndpoints.cs
src/ForgePlm.Domain/EngineeringChanges/EngineeringChangeOrder.cs   # + UpdateDraft, EnsureTitle
src/ForgePlm.Domain/Products/Product.cs                            # + MarkConfigurationChanged
tests/ForgePlm.Tests/Integration/EcoWorkflowTests.cs               # IT-01…IT-07 (11 tests)
tests/ForgePlm.Tests/Api/EcoApiTests.cs                            # 11 HTTP tests
```

Endpoints implemented (`docs/ECO-API-CONTRACT.md`):

| Method | Path |
|---|---|
| GET | `/api/ecos?search=&status=&productId=&page=&pageSize=` |
| GET | `/api/ecos/{ecoId}` |
| POST | `/api/ecos` |
| PUT | `/api/ecos/{ecoId}` |
| POST | `/api/ecos/{ecoId}/submit` |
| POST | `/api/ecos/{ecoId}/approve` |
| POST | `/api/ecos/{ecoId}/reject` |
| POST | `/api/ecos/{ecoId}/release` |
| GET | `/api/audit?entityType=&entityId=&page=&pageSize=` |

There is still no DELETE endpoint anywhere, and still no authentication: `actor` is the demo's
two-person switch (D-06), not an identity.

### The two server-owned values

`baseReleasedRevisionId` is **not** a request field. Creation resolves the product's current
released revision in the same read that validates the product is active, so a stale client cannot
propose a change against a superseded revision. IT-01 is true by construction, and
`AStaleClientCannotChooseTheBaseRevisionOrTheEcoNumber` proves a client cannot smuggle either
value in through an unexpected JSON property.

The ECO number is generated server-side as `ECO-{yyyy}-{NNNN}`. The generator reads the highest
number for the current year and increments it, but the **unique index is the guarantee**, not the
read: creation catches a `23505` on `ux_ecos_eco_number`, clears the change tracker and retries,
up to five attempts. A collision means another transaction committed between the read and the
insert; the next attempt sees it.

### The approval and release transactions

Approval is the ten steps of spec §8 inside one explicit transaction: load the ECO and its lines,
check status and staleness, re-check that replacements are still active, build the new revision
from a copy of the base lines, add it, record the decision and its revision link, write the audit
event, then one `SaveChanges` and one commit. The new revision is `Approved`, never `Released`
(D-04a). Any failure leaves neither a partial revision nor an approval record.

Release sequences `supersede -> save -> release -> save -> commit` in one explicit transaction.
The partial unique index cannot be deferred, so the old revision must stop being `Released` before
the new one becomes `Released` (ADR-004, IT-11). A future effective date is stored and displayed
as "Scheduled"; release still takes effect immediately, and there is no scheduler (D-04b).

### Design decisions taken inside Slices 4–6

| Decision | Why |
|---|---|
| **One application service class per use case**, registered scoped, with a thin endpoint that binds, delegates and returns a DTO | The endpoint cannot become the place where business rules accumulate, and each use case is directly drivable from an integration test without HTTP. |
| **Endpoints never catch exceptions** | `ForgePlmExceptionHandler` already owns status mapping. A `catch` in a handler is how the same failure starts returning two different statuses. |
| **A `null` use-case result means 404** | "Not found" is not an error code in the domain taxonomy; it is the absence of an aggregate. Every endpoint maps it the same way. |
| **`JsonStringEnumConverter(allowIntegerValues: false)` is global** | The contract says enums are strings, and rejecting integers prevents a client sending `1` and silently getting `ChangeQuantity`. Applied to request binding and response serialisation in one option. |
| **Draft editing is `UpdateDraft` on the aggregate**, not a set of property setters in the service | Title, reason, impact, requested date and lines all change together; the aggregate validates the whole edit and raises `invalid-transition` for a non-draft before anything is written. |
| **`Product.MarkConfigurationChanged(now)`** | Approval and release must load and write the product row, otherwise `xmin` is not part of the transaction and ADR-004's guard does not exist. It also fixes Slice 2's carried-forward issue: `UpdatedAtUtc` is now maintained. |
| **`effective-date-required` was added** | The release body requires a date, and no existing code meant "you omitted the effective date". It is an additive clarification, not a shape change; the frozen contract named only `no-released-revision` as a required new code. |
| **`component-not-active` and `target-component-not-in-base-revision` are 400s** | The contract lists both under 400, but they were thrown as `DomainConflictException` (409). The throw sites in `Component.EnsureCanBeUsedInNewBom` and `EngineeringChangeLine.EnsureTargetExistsIn` were aligned to `DomainRuleViolationException`; the existing tests that expected 409 were corrected with them. |

### Bugs the tests found

1. **A new child with an application-assigned key is tracked as `Modified`, not `Added`.**
   Replacing a draft's lines through the tracked navigation made EF treat each new line as an
   existing row: it issued an `UPDATE` that matched nothing, and `SaveChanges` threw
   `DbUpdateConcurrencyException` on a request that should have succeeded. The fix states the
   intent explicitly in `UpdateEngineeringChange` — `RemoveRange` the old lines, `AddRange` the new
   ones — instead of relying on navigation change detection. Caught by
   `ADraftCanBeEditedAndASubmittedEcoCannot`.
2. **`UpdateDraft` could half-apply an edit.** It assigned title/reason/impact before replacing the
   lines, so a line set rejected for `duplicate-target-component` left the aggregate with a new
   title and the old lines. Validation and line replacement now happen before any field is
   assigned; `ABadDraftLineSetDoesNotChangeTheEco` pins it.
3. **PostgreSQL timestamps are microsecond precision.** IT-04's effective-date assertion initially
   failed by two ticks after a round trip. The test now compares within 1 ms, which is the
   precision the database actually stores, rather than weakening the rule being tested.

### Protected demo path

`EcoApiTests.TheProtectedDemoPathWorksEndToEnd` drives the real HTTP pipeline:

```text
Products -> SERVER-100 (released B)
  -> POST /api/ecos      (replace MB-020 with MB-021)
  -> POST .../submit
  -> POST .../approve    (revision C is Approved; B is still Released)
  -> POST .../release    (C Released, B Superseded, ECO Applied)
  -> GET /api/audit      (EcoCreated -> EcoSubmitted -> EcoApproved -> RevisionReleased)
```

### Known rough edges carried forward

- **`reasonForChange` is contract-required but not enforced as non-blank.** The domain stores an
  omitted value as an empty string. There is no code for it in the frozen contract, and inventing
  a second validation vocabulary at the API layer was judged worse than accepting the gap until
  Slice 8. The UI marks the field required.
- **Text longer than 4000 characters reaches PostgreSQL** and surfaces as a 500
  (`22001 string_data_right_truncation`) rather than a 400. The length limit is a database column,
  not a domain check, for `reasonForChange` and `impactAssessment`.
- **A draft ECO cannot be cancelled.** There is no DELETE endpoint (A-06), so an unwanted draft
  stays in the list. The contract says so; the UI should not imply otherwise.
- **No scheduler exists.** A future effective date is a label; "current" is decided by revision
  status, never by the date.
- **The running container predates the uncommitted backend edits.** The image was rebuilt from
  the committed tree at the end of Slice 7 and serves `/api` correctly; the working tree has since
  gained backend changes, including the `AddEcoIntegrityConstraints` migration.
- **The container does not serve the SPA.** The Dockerfile's Slice 7 node stage is still a
  placeholder and `Program.cs` registers the SPA fallback only when `wwwroot/index.html` exists,
  so `http://localhost:5199/` is a 404. The frontend runs on the Vite dev server; adding the build
  stage closes demo mode (A-16).

---

## 11. Session log

| Date | What happened |
|---|---|
| 2026-10-21 | **Phase 0 interview.** 6 confirmed decisions, 20 documented assumptions. Found the repository contains no scaffold at all — the spec's "adapt to the generated scaffold" instructions had nothing to adapt to. Mockup/spec conflicts catalogued (approval-vs-release copy, lifecycle vocabulary, out-of-scope chrome). Verified .NET 10, Node 22, Docker; PostgreSQL chosen over the installed SQL Server at the owner's direction. Wrote revised spec, 5 ADRs, decision register, this document. |
| 2026-10-21 | **Slice 1 — domain model.** Solution (`ForgePlm.slnx`), `ForgePlm.Domain` (no EF/HTTP references), `ForgePlm.Tests`. Aggregates: Product, Component, BomRevision (+BomLine), EngineeringChangeOrder (+line), AuditEvent. Value objects PartNumber/Quantity/RevisionCode. Two exception types with 20 stable error codes. **84 domain tests pass, 0 fail; build clean with warnings-as-errors.** Two build fixes along the way: entity properties shadowed value-object type names (fixed with `Common.` qualification), and the test project needed `<Using Include="Xunit" />`. |
| 2026-10-21 | **Containerised delivery (D-07).** Owner instruction: one `docker compose` command must start everything. Added a multi-stage `Dockerfile` for the API, a `backend` service in compose (`depends_on: service_healthy`, env-var connection string pointing at `db`, healthcheck via `curl`), and `.dockerignore`. Migrate-on-startup moved from an `IsDevelopment()` check to the `Database:ApplyOnStartup` config flag, because the container runs as `Production` and still needs its schema. Added the SPA static-file pipeline with a guard so `MapFallbackToFile` is only registered when `wwwroot/index.html` exists — the backend starts cleanly before Slice 7 builds a frontend. Two containers, not three: the API serves the SPA (A-16). Host port 5199 matches dev. **Verified by a true cold start** (`down -v`, then `up -d --wait`): both containers healthy, seed applied, `/health` answering. Restart confirmed idempotent. 91 tests still passing. |
| 2026-10-21 | **Slice 2 — persistence.** `ForgePlm.Api` (Minimal API host), `ForgePlmDbContext` + per-aggregate configurations, snake_case naming pass, `InitialCreate` migration, fixed-GUID idempotent seed, `docker-compose.yml` for PostgreSQL 18. **91 tests pass, 0 fail** (84 domain + 7 integration against real PostgreSQL); build clean. Four real findings along the way: (1) PostgreSQL 18's Docker image **refuses to start** with data at the old `/var/lib/postgresql/data` mount and wants `/var/lib/postgresql` — fixed in compose and commented; (2) Npgsql EF Core 10 **removed `UseXminAsConcurrencyToken()`**, so ADR-004's three underlying calls were replicated by hand and verified against the generated migration; (3) the seed duplicated its audit row every startup because the helper took an id for the existence check while the factory minted its own — fixed and made unrepresentable; (4) **IT-09's claim was false** — the unique index is case-sensitive, so A-02 was amended and the test split into the two facts that are actually true. Two NuGet advisories were fixed by overriding transitive packages rather than suppressing the audit. |
| 2026-10-21 | **Code review response.** Four items from an independent review. Two were verified and fixed in the domain: the error taxonomy returned `blank-part-number` for six unrelated failures (8 codes added, 6 throw sites corrected, `duplicate-part-number` added to close the contract before endpoints exist, `revision-limit-reached` moved to `409` per spec �7); and `Lines` exposed the backing list, so a released revision could be mutated through a cast (now `_lines.AsReadOnly()`, EF unaffected because it uses field access). Two were tested rather than taken on trust: the predicted release-ordering failure **did not reproduce** � a single `SaveChanges` superseding and releasing did not throw across five runs, though the constraint is real and cannot be deferred, so Slice 6 still ships the ordered design and both halves are pinned by IT-11; and duplicate ECO targets were confirmed to be silently merged by `FirstOrDefault`, now rejected at `ReplaceLines`. **101 tests pass, 0 fail; build clean.** |
| 2026-10-21 | **Slice 3 - product & BOM read endpoints.** Products and components list/detail, BOM revision history, current revision, revision lines - 8 endpoints, all returning DTOs rather than EF entities, with the `{ items, total, page, pageSize }` shape �7 requires. Built the RFC 7807 mapping **before** the endpoints: `ErrorResponseMapper` plus an `IExceptionHandler`, so a new endpoint cannot forget it. Two findings: (1) registering `AddExceptionHandler` alone was **not enough** - `WebApplication.CreateBuilder` adds the developer exception page in Development, which sits outer in the pipeline and swallowed every exception, so a bad filter returned a 500 with a stack trace where the contract promised a 400; fixed with an explicit `UseExceptionHandler()`. (2) The new API tests failed intermittently (`3D000: database does not exist`, ~2 in 3 runs) because each test constructed its own `PostgresFixture`, whose `DisposeAsync` sweeps **every** test database on the server - so each test dropped the database a parallel test was using. Fixed by joining `PostgresCollection`; verified over five consecutive full-suite runs. **111 tests pass, 0 fail; build clean.** |
| 2026-10-21 | **Slices 4–6 — ECO write path and audit read.** Create/edit/submit/approve/reject/release plus `GET /api/audit`, implemented to the frozen `ECO-API-CONTRACT.md`. ECO numbers and base revisions are server-resolved; creation retries a number collision up to five times. Approval runs the ten-step §8 transaction; release sequences supersede → save → release → save because the partial unique index cannot be deferred. Every action writes its audit row in the same transaction. **137 tests pass, 0 fail** (94 domain + 22 integration against real PostgreSQL + 21 API); IT-06 and IT-10 were re-run explicitly against PostgreSQL (3/3). Findings: EF tracks a new child with an application-assigned key as `Modified`, so line replacement needs explicit `RemoveRange`/`AddRange`; `UpdateDraft` had to validate lines before assigning fields; PostgreSQL stores microseconds, so the IT-04 date assertion uses a 1 ms tolerance. The frozen contract's 400s for `component-not-active` and `target-component-not-in-base-revision` required changing two domain throw sites; `effective-date-required` was added as an additive code. `Product.UpdatedAtUtc` is now maintained. **Build clean, 0 warnings.** |
| 2026-10-21 | **Slice 7 — frontend.** Vite 8 + React 19 + TS 7 + Tailwind 4 in `frontend/`: app shell, sidebar with exactly Products and Engineering Changes, actor switch, Products list/detail (live read API), and ECO list/detail/create-edit. Screens C/D/E were first built against the frozen contract behind an in-memory mock and a start-up probe; once Slices 4–6 landed, the probe, the mock, `EcoModeNote` and `VITE_ECO_API_MODE` were deleted and every screen bound directly to `LiveEcoApi`/`LiveProductsApi`. FE-02 is a status matrix in `lib/ecoStatus.ts`; FE-03 is one `ConfirmDialog` showing the current actor. Timestamps are stored/sent in UTC and shown locally with UTC on hover; part numbers are monospaced. **`npm run build` passes; `npm test` 37/37** (ecoStatus, format, http, ecoApi). The protected path was driven in a browser against the rebuilt container (create → submit → approve → release → revision C), plus draft edit (PUT) and reject. Known gap: the Dockerfile still has no SPA build stage, so the container serves `/api` only. |
