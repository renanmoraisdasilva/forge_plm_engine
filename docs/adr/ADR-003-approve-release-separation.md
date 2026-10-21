# ADR-003: Approval and release are separate; effective date is a recorded label

- **Status:** Accepted, 2026-10-21
- **Deciders:** Product owner (D-04a, D-04b)
- **Related:** spec §1, §8, §15.5, §15.7

## Context

Two sources of truth disagreed.

**The specification** (§1, §15.7) says approval and release are deliberately separate concepts: approval creates a revision in `Approved` status; a distinct, explicit release action makes it the current released configuration and assigns the effective date. It calls this separation intentional and says it must remain consistent across API and UI.

**The ECO-details mockup** says the opposite in its own copy: *"Approval authorizes the configuration change immediately; physical release deployment triggers on Effective Date (Nov 1, 2026)"*, and it renders the pipeline as Draft → Submitted → Review & CCB → "Release / Applied — **auto** Nov 1, 2026".

The specification also permits future effective dates but never defines what one *means* — a real gap, because a future effective date normally implies either a scheduler or a time-dependent notion of "current revision".

## Decision

1. **The specification wins.** Approval creates an `Approved` revision and changes nothing else. Release is a separate confirmed action, visible only when the ECO is `Approved`. The mockup's copy is rewritten, not ported (D-04a).
2. **Release always takes effect immediately.** In one transaction: the new revision becomes `Released` with `EffectiveAtUtc` set, the previous released revision becomes `Superseded`, and the ECO becomes `Applied`. A future timestamp is accepted, stored, and displayed as "Scheduled" — it is intent, not a trigger. There is no background job (D-04b).

## Consequences

**Good**

- Two distinct, demonstrable PLM concepts instead of one collapsed action — which is the point of the demo (D-02: "demonstrating PLM concepts").
- "At most one `Released` revision per product" stays trivially true, because a revision is either released now or it is not. No partial-state reasoning.
- The concurrency test (IT-06) stays honest and simple: two competing ECOs, one release wins.
- No scheduler, no timer, no "current revision depends on wall-clock time" query semantics.

**Costs and honesty requirements**

- A revision released with a future effective date is *already current*, while its stored intent says otherwise. That is a real modelling simplification, not a feature. The UI must label it "Scheduled" so the demo never implies delayed effectivity that does not exist.
- `EffectiveAtUtc` is metadata. It must not be used anywhere as a filter for "which revision is current" — doing so would silently reintroduce the scheduler this decision removed.

**If genuine scheduled effectivity is ever required**, three things must change together, and this ADR is the record of what to revisit: the "at most one released revision" rule, the partial unique index that enforces it, and every query that resolves the current revision. The model has no way to express "released but not yet effective".

**Rejected alternatives**

- *Mockup behaviour — approval releases immediately:* simpler flow, but the `Approved` state, the separate Release button and the whole approval/effectivity distinction disappear. The spec calls the separation intentional.
- *Genuine scheduled release job:* correct PLM semantics, but requires a scheduler, makes "current" wall-clock dependent, and is disproportionate for a prototype whose protected demo path (D-02c) completes in one sitting.
- *Rejecting future dates entirely:* simplest rule, but discards the effectivity concept the specification explicitly asks for, and makes the release form's date field decorative.
