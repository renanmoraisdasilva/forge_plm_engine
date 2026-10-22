# Forge PLM — Implementation Reference

Current status, the ECO/audit HTTP contract, implementation decisions that are easy to misstate, and
the concurrency design (ADR-004). Read [ARCHITECTURE.md](ARCHITECTURE.md) for how the code is put
together and [SPECIFICATION.md](SPECIFICATION.md) for product scope and domain rules.

Keep the contract sections aligned with `src/ForgePlm.Api/Endpoints/` and
`src/ForgePlm.Api/Contracts/`.

---

## Status

Status last verified 2026-10-21.

### Implemented

- Domain model, PostgreSQL persistence, EF Core migrations, idempotent demo seed, and
  product/component/BOM read APIs.
- ECO create, draft edit, submit, approve, reject, release, and audit APIs. Approval and release are
  separate operations; release and its audit record are transactional. Release applies immediately
  in the prototype, and actual release and effective timestamps are recorded separately. Draft edits
  write an `EcoDraftUpdated` event with before/after snapshots in the same save as the ECO and lines.
- API-only product/component creation writes matching audit events. A new active product with no
  revision history can initialize its first released BOM as revision `A`; later revisions require
  ECOs. ECO writes use PostgreSQL `xmin` concurrency tokens so stale transitions conflict.
- React screens for products and engineering changes, actor switch, confirmation flows, BOM
  comparison, and audit timeline.
- Compose demo image that builds and serves the frontend from the API host.

### Last recorded verification

- **Backend:** `dotnet test ForgePlm.slnx --no-restore` and `coverage.ps1` passed with **163 tests**;
  Cobertura reports **89.00% lines** and **49.31% branches**.
- **Frontend:** production build passed; **105 tests** in 13 files. V8 whole-`src/` coverage measured
  **59.20% statements, 48.97% branches, 58.70% functions, and 60.84% lines**; excluding
  bootstrap/composition files, line coverage is **61.7%**.
- **Compose:** `docker compose up -d --build --wait` reached healthy. HTTP smoke checks returned the
  SPA shell for `/` and `/products`, loaded the JavaScript/CSS assets, returned 200 for the seeded
  product and current BOM APIs, and returned ProblemDetails 404 for an unknown `/api` route.
- A rendered browser check was not possible on the Windows host used: headless Chrome/Edge failed to
  initialize the GPU process.

### Remaining work

1. Run a visual browser check on a host where headless Chrome/Edge can initialize.
2. Add tests for `EcoForm`, `ProductDetail`, `AppShell`, and the router, then consider an E2E suite.

The frontend report measures all of `src`. Transport, pure helpers, providers, the lists, and ECO
detail now have component coverage; `EcoForm` and `ProductDetail` are the remaining large gaps.
Integration tests require the Compose PostgreSQL service and use a per-run database prefix so
concurrent runs do not drop each other's databases.

---

## API contract

### Conventions

- JSON bodies. Enums are **strings** on the wire in both directions; numeric values are rejected.
- Timestamps are ISO-8601 UTC. Stored UTC, displayed local, UTC on hover.
- Every list endpoint takes `page` (1-based, default 1) and `pageSize` (default 50, max 200) and
  returns `{ items, total, page, pageSize }`.
- Every error is RFC 7807 `ProblemDetails` with a `code` extension. Every `409` also carries
  `conflictType`.
- There are **no DELETE endpoints**. A draft ECO cannot be cancelled.
- `actor` is a demo identity supplied in the body. It is not authentication or authorization.

### Initial BOM revision

`POST /api/products/{productId}/bom-revisions/initial` → `201 BomRevisionDto`

```jsonc
{
  "actor": "string (required)",
  "lines": [{ "componentId": "guid", "quantity": 1, "unit": "Each" }]
}
```

Creates revision `A` as the product's first released BOM. The product must be active with no existing
revision history; every component must exist and be active, and lines must be non-empty with unique
components and positive quantities. Release is effective immediately, and an
`InitialRevisionReleased` audit event is written atomically. Every later revision must be proposed
through an ECO.

### ECO endpoints

| Endpoint | Behaviour |
|---|---|
| `GET /api/ecos?search=&status=&productId=&page=&pageSize=` | `PagedResponse<EcoSummaryDto>`. `search` matches number or title, case-insensitively. `status` accepts `Draft\|Submitted\|Approved\|Rejected\|Applied`; anything else is 400 `invalid-filter-value`. |
| `GET /api/ecos/{ecoId}` | `EcoDetailDto`, including lines. 404 if unknown. |
| `POST /api/ecos` | 201 `EcoDetailDto` with `Location: /api/ecos/{id}`. Server assigns the number and resolves the base revision. |
| `PUT /api/ecos/{ecoId}` | Edit a draft. Same fields as `POST` minus `productId`; `actor` identifies the editor. Any other status is 409 `invalid-transition`. |
| `POST /api/ecos/{ecoId}/submit` | `Draft` → `Submitted` after validating lines, targets, and replacements. |
| `POST /api/ecos/{ecoId}/approve` | Ten-step transaction: copies base lines, applies changes, creates an `Approved` revision, sets `ApprovedRevisionId`, writes audit, one commit. Never releases. |
| `POST /api/ecos/{ecoId}/reject` | `Submitted` → `Rejected` with a non-blank reason. Creates no BOM revision. |
| `POST /api/ecos/{ecoId}/release` | `Approved` → `Applied`: supersedes the old revision, releases the new one. |
| `GET /api/audit?entityType=&entityId=&page=&pageSize=` | Paged `AuditEventDto`, newest first. `entityType` is `Product\|Component\|BomRevision\|EngineeringChangeOrder`. |

#### Create and edit bodies

```jsonc
// POST /api/ecos
{
  "productId": "guid",
  "title": "string (required, max 200)",
  "reasonForChange": "string (required, max 4000)",
  "impactAssessment": "string (optional, max 4000)",
  "requestedEffectiveAtUtc": "datetime | null",
  "actor": "string (required, max 200)",
  "lines": [
    {
      "changeType": "ReplaceComponent | ChangeQuantity",
      "targetComponentId": "guid",
      "replacementComponentId": "guid | null",   // required for ReplaceComponent
      "newQuantity": "int | null",               // required for ChangeQuantity, must be > 0
      "reason": "string | null (max 4000)"
    }
  ]
}
```

- **The ECO number is server-generated** (`ECO-{yyyy}-{NNNN}`) and never sent by the client.
- **`baseReleasedRevisionId` is deliberately absent.** The server resolves the product's current
  released revision at creation, so a stale client cannot propose against an outdated revision.
  Integration test IT-01 is true by construction rather than by validation.
- `lines` must be non-empty, and **no two lines may target the same component**
  (`duplicate-target-component`): approval resolves each base line with `FirstOrDefault`, so a second
  change to the same target would be silently discarded.
- A successful draft edit writes `EcoDraftUpdated` with complete `before` and `after` snapshots,
  including change lines, in the same save as the replacement fields.

#### Action bodies

```jsonc
// submit
{ "actor": "string (required)" }

// approve
{ "actor": "string (required)", "comment": "string | null" }

// reject
{ "actor": "string (required)", "rejectionReason": "string (required, non-blank)" }

// release
{ "actor": "string (required)", "confirmImmediateEffectivity": "boolean (optional, defaults false)" }
```

**Release ordering is load-bearing.** The partial unique index
`ux_bom_revisions_one_released_per_product` is checked per statement and cannot be deferred. Sequence
it as **supersede → save → release → save inside one explicit transaction**. A single `SaveChanges`
happened not to throw in five measured runs on 2026-10-21, but that is an accident of EF batching,
not a guarantee. `IT11_*` tests pin both halves.

The service records `ReleasedAtUtc` as the release event time and `EffectiveAtUtc` as the time the
revision applies; both are current UTC in this prototype. If the ECO has a future
`RequestedEffectiveAtUtc`, the client must send `confirmImmediateEffectivity: true`; otherwise the
API returns 409 `future-effectivity-requires-confirmation`. No scheduled effectivity is supported.
`GET /api/products/{productId}/bom-revisions/current` only returns an effective released revision,
so a future-dated revision is never presented as the production configuration.

### DTOs

```jsonc
EcoSummaryDto: { id, ecoNumber, title, productId, product: { id, partNumber, name }, status,
                 createdBy, createdAtUtc, lineCount }

EcoDetailDto:  EcoSummaryDto plus { baseReleasedRevisionId, baseRevisionCode, reasonForChange,
                 impactAssessment, requestedEffectiveAtUtc?, submittedAtUtc?, decidedAtUtc?,
                 decidedBy?, reviewerComment?, approvedRevisionId?, lines[] }

EcoLineDto:    { id, changeType, targetComponentId, target: { id, partNumber, name },
                 replacementComponentId?, replacement: { id, partNumber, name }?, newQuantity?,
                 reason? }

AuditEventDto: { id, occurredAtUtc, actor, action, entityType, entityId, details }
```

`details` is a JSON object, not a string, so the timeline renders it without a second parse.
Component summaries are **denormalised into** `target`, `replacement`, and `product`; without them
the ECO detail screen would need a request per line to render part numbers.

Audit actions: `ProductCreated`, `ComponentCreated`, `InitialRevisionReleased`, `EcoCreated`,
`EcoDraftUpdated`, `EcoSubmitted`, `EcoApproved`, `EcoRejected`, `RevisionReleased`.

### Error contract

Status is decided by the **exception type**, never by matching message text. One mapper owns this
(`ErrorResponseMapper`); endpoints let exceptions propagate to it.

| Failure | Status | `code` / `conflictType` |
|---|---|---|
| `DomainRuleViolationException` | 400 | `code` |
| `DomainConflictException` | 409 | `conflictType = Code` |
| `DbUpdateConcurrencyException` | 409 | `concurrency-conflict` |
| `23505` unique violation | 409 | derived from the violated **index name** |
| anything else | 500, logged | — |

**400 — the request itself is invalid and no future state fixes it:** `blank-title`, `blank-actor`,
`eco-lines-required`, `duplicate-target-component`, `target-component-required`,
`replacement-component-required`, `target-component-not-in-base-revision`,
`replacement-must-differ-from-target`, `non-positive-quantity`, `rejection-reason-required`,
`component-not-active`, `reason-for-change-required`, `text-too-long`, `invalid-filter-value`.

**409 — the request was valid but current state forbids it:** `invalid-transition` (disable the
button instead), `stale-base-revision` (another ECO released first; offer a new ECO),
`product-retired`, `revision-limit-reached`, `duplicate-eco-number` (server retry handles it; should
not surface), `concurrency-conflict` (reload and retry), `conflicting-released-revision` (two
releases raced), `no-released-revision` (creating an ECO for a product with no released BOM),
`future-effectivity-requires-confirmation`.

The frontend keys its messages and recovery actions off these strings, so adding a code is an API
contract change.

### The UI should prevent, not report, `invalid-transition`

A `409 invalid-transition` reaching the UI is a client bug, not a user error.

| Status | Submit | Approve | Reject | Release |
|---|---|---|---|---|
| `Draft` | ✅ | ❌ | ❌ | ❌ |
| `Submitted` | ❌ | ✅ | ✅ | ❌ |
| `Approved` | ❌ | ❌ | ❌ | ✅ |
| `Rejected` | ❌ | ❌ | ❌ | ❌ |
| `Applied` | ❌ | ❌ | ❌ | ❌ |

Approve, reject, and release require a confirmation dialog showing the **current actor**.

---

## Implementation decisions

### Part-number uniqueness

Part numbers are trimmed and uppercased at the domain boundary. PostgreSQL has a plain unique index
on the stored value, so case-insensitive uniqueness depends on all writes passing through the
domain. A direct SQL write could bypass that rule. Tests cover normalization and the database
constraint separately.

### ECO numbering

The API assigns `ECO-{year}-{sequence}`. A unique database constraint protects the number; creation
retries a collision (bounded to five attempts, detaching the failed attempt) rather than asking the
client to choose.

### Error mapping

One exception handler maps domain rule violations to 400 and domain conflicts to 409. PostgreSQL
unique violations and EF concurrency failures also map to 409 with a stable conflict code. Endpoint
handlers do not duplicate this mapping.

### Migrations and seed

The API applies migrations and the idempotent demo seed on startup when `Database:ApplyOnStartup` is
enabled; Compose enables it for the demo. A non-demo deployment should apply migrations as a
separate release step. Fixed GUIDs make re-running the seed a no-op rather than a duplicate check.

### Concurrency

Product and ECO `xmin` tokens plus a partial unique index enforce release and state-transition
concurrency. PostgreSQL-specific by design; see [ADR-004](#adr-004-concurrency-via-xmin-token-plus-a-partial-unique-index).
Integration tests run against PostgreSQL rather than an in-memory provider.

### Short references in source comments

These labels remain in implementation comments. Their durable requirements are stated in the
specification or above.

| Label | Reference |
|---|---|
| `A-04` | Server-generated yearly ECO numbers |
| `A-07` | Product/component fields are immutable in v1; spec §16 |
| `A-11` | Migrations and idempotent seed run on startup when configured |
| `A-12` | Fixed GUIDs make demo seed application idempotent |
| `A-15` | UTC storage and local display; spec §3 |
| `A-16` | API serves the built SPA in demo mode; Vite proxy in development |
| `A-18` | `BomRevisionStatus.Draft` is unused in v1; spec §16 |
| `D-02c` | Protected demo path; spec §1 |
| `D-04a` | Approval and release are separate; spec §15, decision 7 |
| `D-04b` | Prototype releases take effect immediately; release and effective timestamps stay separate; future dates need confirmation |
| `D-05` | PostgreSQL is the chosen database; spec §4 |

---

## ADR-004: Concurrency via `xmin` token plus a partial unique index

- **Status:** Accepted, 2026-10-21
- **Related:** spec §8, §11 (IT-06, IT-07, IT-10)

### Context

Spec §8 requires that two competing ECOs based on the same released revision cannot both release as
the next current revision, and warns against claiming concurrency safety without tests. PostgreSQL
has a native version token that needs no schema column and no application bookkeeping: the `xmin`
system column.

### Decision

Two independent defences, because they fail differently.

**1. Optimistic concurrency — `xmin` on `Product` and `EngineeringChangeOrder`.**

Both roots carry shadow `xmin` properties configured as concurrency tokens. Approve and release load
and write the product row inside their transaction; every ECO edit or transition writes the ECO row.
If another transaction committed a conflicting write after the row was read, `xmin` has changed, EF
throws `DbUpdateConcurrencyException`, and the endpoint maps it to 409 `concurrency-conflict`.
`stale-base-revision` remains the separate deterministic check that the ECO's base is still current.
This prevents stale draft edits, duplicate submissions, and competing decisions from silently
overwriting one another.

Npgsql EF Core 10 **removed** `UseXminAsConcurrencyToken()`, so the three equivalent calls are
written explicitly in `Configurations.cs`:

```csharp
builder.Property<uint>("xmin")
    .IsConcurrencyToken()          // original xmin goes in the UPDATE's WHERE clause
    .ValueGeneratedOnAddOrUpdate() // xmin stays out of the INSERT column list
    .HasDefaultValueSql("xmin");   // the system column supplies the value
```

The generated migrations confirm equivalence: `type: "xid", rowVersion: true, defaultValueSql:
"xmin"`. Because it is a shadow property, `xmin` never appears in the domain model.

**2. Database enforcement — partial unique index.**

```sql
CREATE UNIQUE INDEX ux_bom_revisions_one_released_per_product
    ON bom_revisions (product_id)
    WHERE status = 'Released';
```

Even if application logic were wrong, the database refuses a second current released revision. This
invariant lives in the schema, not in a code path that could be bypassed.

Related schema constraints:

- `bom_revisions (product_id, revision_code)` unique — no duplicate `C`.
- `eco_number` unique — no duplicate `ECO-2026-0042`.
- `(part_number)` unique per table, on the *stored* column. EF Core cannot express `upper(part_number)`,
  and hand-editing a migration would leave the model snapshot disagreeing with the database.
  Case-insensitivity comes from domain normalization instead; see
  [part-number uniqueness](#part-number-uniqueness). Pinned by IT-09's two tests.

### Release ordering (measured 2026-10-21)

A code review predicted that superseding the old revision and releasing the new one in a single
`SaveChanges` would violate the partial index, because EF Core does not guarantee the order of two
updates to the same table. That was measured rather than debated.

**The prediction did not reproduce.** A released `B` and an approved `C` for one product, both
persisted, then both status changes in one `SaveChanges`: no exception, stably across five runs. EF
happened to issue the supersede first.

**That is still not a licence to batch it.** Three facts keep the ordered design:

1. EF Core guarantees ordering only for *dependent* commands. Two updates to one table have no such
   guarantee, so today's behaviour is an accident of batching that a patch release could change.
2. The constraint **cannot be deferred.** PostgreSQL allows `DEFERRABLE` only on table constraints,
   and a partial unique index is not one.
3. The failure, when it comes, would be a `23505` surfacing from a code path nobody thought could
   produce one — a bad shape for the demo's central invariant to fail in.

So the release use case sequences **supersede → save → release → save inside one explicit
transaction**: two statements in a fixed order, kept atomic so a failure on the second rolls the
first back and the product is never left with no released revision.

Both halves are pinned: `IT11_ReleasingTheNewRevisionBeforeSupersedingTheOldOneViolatesTheIndex`
proves the constraint is live for the release-time shape, and
`IT11_OrderedTwoStepReleasePersistsAtomicallyInOneTransaction` proves the design that satisfies it.
The failed prediction is recorded deliberately so the next reader sees what was measured.

### Consequences

**Good**

- No hand-rolled version column, no "remember to increment" discipline, no trigger.
- The defences are independent: the index does not depend on the token being read, and the token
  does not depend on anyone remembering the index.
- IT-06 (competing ECOs) and IT-10 (index refuses a second released revision) are real relational
  tests against the same engine the demo uses. No in-memory provider can support these claims.

**Costs**

- `xmin` is PostgreSQL-specific. Acceptable: spec §4 rules out a second provider.
- `DbUpdateConcurrencyException` must be caught in exactly one place and mapped to a consistent 409.
  Swallowing it or retrying silently would break the "return conflicts for stale data" principle.
- Do not claim either defence alone is sufficient; both are tested for different properties.

**Rejected alternatives**

- *Application-only check ("read current revision, compare, then write"):* correct under a race only
  if the transaction holds a lock, which is exactly what the index gives for free.
- *Advisory locks per product:* real mutual exclusion, but serialises legitimately concurrent work
  and is harder to explain.
- *Serializable isolation for everything:* correct but disproportionate, and serialization failures
  would need retry logic to build and test.
- *`rowversion`-style column:* a SQL Server idiom; `xmin` achieves the same on PostgreSQL without a
  column.
