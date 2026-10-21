# Forge PLM — ECO and Audit API Reference

This document records the ECO and audit routes, DTO shapes, and error codes used by the live
frontend. Keep it aligned with `src/ForgePlm.Api/Endpoints/` and the DTOs in
`src/ForgePlm.Api/Contracts/`. Product, component, and BOM routes are listed in specification §7.

---

## Conventions

- All bodies are JSON. Enums are **strings** (`"Draft"`, `"ReplaceComponent"`) — same rule as the
  read endpoints.
- Timestamps are ISO-8601 UTC. Stored UTC, displayed local, UTC on hover (spec §3).
- Every list endpoint takes `page` (1-based, default 1) and `pageSize` (default 50, max 200) and
  returns `{ items, total, page, pageSize }` (§7).
- Every error is RFC 7807 `ProblemDetails` with a `code` extension. Every `409` also carries
  `conflictType`; see [implementation decision notes](DECISIONS.md#errors).
- There are **no DELETE endpoints anywhere** (spec §16). A draft ECO cannot be cancelled.

---

## Endpoints

### `GET /api/ecos?search=&status=&productId=&page=&pageSize=`

`search` matches ECO number or title, case-insensitively. `status` matches
`Draft|Submitted|Approved|Rejected|Applied`; an unrecognised value is a **400**
`invalid-filter-value`, not an empty list. `productId` filters to one product's changes.

Returns `PagedResponse<EcoSummaryDto>`.

### `GET /api/ecos/{ecoId}` → 200 `EcoDetailDto`

404 if unknown. Includes `lines` so the detail screen needs one call.

### `POST /api/ecos` → 201 `EcoDetailDto`, `Location: /api/ecos/{id}`

```jsonc
// request
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
      "replacementComponentId": "guid | null",   // required when ReplaceComponent
      "newQuantity": "int | null",               // required when ChangeQuantity, must be > 0
      "reason": "string | null (max 4000)"
    }
  ]
}
```

**The ECO number is server-generated** (`ECO-{yyyy}-{NNNN}`) and is *not* in the request. See
[implementation decision notes](DECISIONS.md#eco-numbering).

**`baseReleasedRevisionId` is deliberately not in the request.** The server resolves the
product's current released revision at creation time. Passing it from the client would let a
stale client produce an ECO based on a revision that is no longer current — and spec §11's IT-01
("creating an ECO captures the current released revision") is then true by construction rather
than by validation.

`lines` must be non-empty, and **no two lines may target the same component**
(`duplicate-target-component`) — approval resolves each base line with `FirstOrDefault`, so a
second change to the same target would be silently discarded.

### `PUT /api/ecos/{ecoId}` → 200 `EcoDetailDto`

Draft only. Same body as `POST` minus `productId` and `actor` (the creator is immutable).
Editing a non-draft ECO is **409** `invalid-transition`.

### `POST /api/ecos/{ecoId}/submit` → 200 `EcoDetailDto`

```jsonc
{ "actor": "string (required)" }
```

Preconditions: status `Draft`, at least one line, every target exists in the base revision, every
replacement exists and is `Active`.

### `POST /api/ecos/{ecoId}/approve` → 200 `EcoDetailDto`

```jsonc
{ "actor": "string (required)", "comment": "string | null" }
```

Runs the ten-step transaction in §8: copy base lines → apply changes → new `Approved` revision →
ECO to `Approved` → audit event → **one commit**. Any failure leaves no partial revision.

The response's `approvedRevisionId` is populated. The new revision is **`Approved`, not
`Released`** (spec §15, decision 7) — release is a separate explicit action.

### `POST /api/ecos/{ecoId}/reject` → 200 `EcoDetailDto`

```jsonc
{ "actor": "string (required)", "rejectionReason": "string (required, non-blank)" }
```

A blank reason is **400** `rejection-reason-required`. Rejecting creates **no** BOM revision.

### `POST /api/ecos/{ecoId}/release` → 200 `EcoDetailDto`

```jsonc
{ "actor": "string (required)", "effectiveAtUtc": "datetime (required)" }
```

Supersedes the prior revision, releases the new one, sets the ECO to `Applied`.

> **Ordering is load-bearing — do not batch this.** The partial unique index
> `ux_bom_revisions_one_released_per_product` is checked per statement and cannot be deferred.
> Sequence it as **supersede → save → release → save inside one explicit transaction**.
> Measured on 2026-10-21: a single `SaveChanges` happened *not* to throw across five runs, but
> that is an accident of EF's batching, not a guarantee. `IT11_*` tests pin both halves.

The effective date is a **recorded label**, not a trigger (spec §15.5). Release takes effect
immediately; a future timestamp is stored and shown as "Scheduled". No scheduler exists.

### `GET /api/audit?entityType=&entityId=&page=&pageSize=`

Read-only, paged, newest first. `entityType` is `Product|Component|BomRevision|EngineeringChangeOrder`.
Needed by screen D's audit timeline. Ships with **Slice 4** — it is a read, and the audit rows are
written by the very use cases that slice adds.

---

## DTOs

```jsonc
EcoSummaryDto {
  "id": "guid",
  "ecoNumber": "ECO-2026-0042",
  "title": "string",
  "productId": "guid",
  "product": { "id": "guid", "partNumber": "SERVER-100", "name": "string" },
  "status": "Draft",
  "createdBy": "Alex Morgan",
  "createdAtUtc": "datetime",
  "lineCount": 1
}

EcoDetailDto {                     // EcoSummaryDto plus:
  "baseReleasedRevisionId": "guid",
  "baseRevisionCode": "B",
  "reasonForChange": "string",
  "impactAssessment": "string",
  "requestedEffectiveAtUtc": "datetime | null",
  "submittedAtUtc": "datetime | null",
  "decidedAtUtc": "datetime | null",
  "decidedBy": "string | null",
  "reviewerComment": "string | null",
  "approvedRevisionId": "guid | null",
  "lines": [ EcoLineDto ]
}

EcoLineDto {
  "id": "guid",
  "changeType": "ReplaceComponent | ChangeQuantity",
  "targetComponentId": "guid",
  "target": { "id": "guid", "partNumber": "MB-020", "name": "string" },
  "replacementComponentId": "guid | null",
  "replacement": { "id", "partNumber", "name" } | null,
  "newQuantity": "int | null",
  "reason": "string | null"
}

AuditEventDto {
  "id": "guid",
  "occurredAtUtc": "datetime",
  "actor": "Jordan Lee",
  "action": "EcoCreated | EcoSubmitted | EcoApproved | EcoRejected | RevisionReleased | ProductCreated | ComponentCreated",
  "entityType": "string",
  "entityId": "guid",
  "details": "object"
}
```

Component summaries are **denormalised into** `target` / `replacement` / `product`. Without them
the ECO detail screen needs one extra request per line to render part numbers.

---

## Error contract

Status is decided by the **exception type**, never by matching on message text. One mapper owns
this (`ErrorResponseMapper`), and endpoints let exceptions propagate to it.

| Failure | Status | `code` / `conflictType` |
|---|---|---|
| `DomainRuleViolationException` | 400 | `code` |
| `DomainConflictException` | 409 | `conflictType = Code` |
| `DbUpdateConcurrencyException` | 409 | `concurrency-conflict` |
| `23505` unique violation | 409 | derived from the violated **index name** |
| anything else | 500, logged | — |

### Codes the ECO screens must handle

Already implemented:

| Code | Status | Meaning / UI reaction |
|---|---|---|
| `blank-title` | 400 | field error |
| `blank-actor` | 400 | actor switch has no selection |
| `eco-lines-required` | 400 | add at least one change line |
| `duplicate-target-component` | 400 | two lines change the same part; split into two ECOs |
| `target-component-required` | 400 | field error |
| `replacement-component-required` | 400 | field error |
| `target-component-not-in-base-revision` | 400 | part is not in the base BOM |
| `replacement-must-differ-from-target` | 400 | field error |
| `non-positive-quantity` | 400 | field error |
| `rejection-reason-required` | 400 | required before rejecting |
| `invalid-transition` | 409 | **disable the button instead** — see below |
| `stale-base-revision` | 409 | another ECO released first; offer to start a new ECO |
| `component-not-active` | 400 | replacement part is retired |
| `product-retired` | 409 | product cannot receive a release |
| `revision-limit-reached` | 409 | product has 26 revisions; out of scope for the demo |
| `duplicate-eco-number` | 409 | server-side retry handles this; should not surface |
| `concurrency-conflict` | 409 | reload and retry |
| `conflicting-released-revision` | 409 | two releases raced |

**Additional implemented codes:**

| Code | Status | Raised by |
|---|---|---|
| `no-released-revision` | 409 | creating an ECO for a product with no released BOM |
| `effective-date-required` | 400 | release submitted without an effective date |
| `reason-for-change-required` | 400 | create/edit sent a blank reason (`required` above was previously only documented) |
| `text-too-long` | 400 | a text field exceeded its documented maximum before it reached the database |

### The UI should prevent, not report, `invalid-transition`

FE-02 requires invalid actions to be **disabled** per ECO status, not offered and then rejected.
A `409 invalid-transition` reaching the UI is a client bug, not a user error. The status matrix:

| Status | Submit | Approve | Reject | Release |
|---|---|---|---|---|
| `Draft` | ✅ | ❌ | ❌ | ❌ |
| `Submitted` | ❌ | ✅ | ✅ | ❌ |
| `Approved` | ❌ | ❌ | ❌ | ✅ |
| `Rejected` | ❌ | ❌ | ❌ | ❌ |
| `Applied` | ❌ | ❌ | ❌ | ❌ |

Approve, Reject and Release require a confirmation dialog showing the **current actor** (FE-03,
decision 4 in spec §15).

---

## Scope notes

- `POST /api/products` and `POST /api/components` are API-only endpoints and are not used by the
  protected demo path.
- Any DELETE endpoint — excluded by §16.
- Authentication. The `actor` field is a demo affordance (decision 4 in spec §15); production identity would come
  from authenticated claims, and the UI should say so.
