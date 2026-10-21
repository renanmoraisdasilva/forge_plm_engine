# ADR-005: Demo identity behind `IActorProvider` with a two-person switch

- **Status:** Accepted, 2026-10-21
- **Deciders:** Product owner (D-06)
- **Related:** spec §7, §9, §10, §15.4, §16

## Context

Authentication is explicitly out of scope (§2). But an approval record in which the reviewer and the author are the same person undermines the exact concept the demo exists to demonstrate: controlled approval. The specification's §15.4 default — "a fixed demo actor" — has that flaw baked in.

At the same time, a demo that sends an `actor` string in a request body is a security liability if anyone mistakes it for a security control.

## Decision

A two-person actor switch, read through a boundary:

- **UI:** a small persistent "Acting as" selector in the app header with two seeded identities — `Alex Morgan` (engineer/author) and `Jordan Lee` (reviewer).
- **Transport:** the selected name travels in the request body of every state-changing call, exactly as §7 already specifies.
- **Application:** a single interface, `IActorProvider`, resolves the current actor. Endpoints bind the request's actor value into it; the domain never reads `HttpContext`.
- **Boundary:** `IActorProvider` is the seam. Replacing it with authenticated claims later requires no domain change.

## Consequences

**Good**

- The demo's most likely credibility hole — self-approval — closes for about thirty minutes of work.
- The audit trail becomes a better story: `EcoCreated` by Alex Morgan, `EcoApproved` by Jordan Lee. That is what an interviewer expects to see.
- The boundary is one interface with one implementation. It is not an identity system.

**Honesty requirements**

- The actor is client-supplied. **It is not authentication, authorization or non-repudiation**, and the README and §16 must say so. Nobody should be able to describe this as "identity" in a demo without immediately adding "demo-only".
- The switch must not grow roles, permissions, or a notion of authority. §2 forbids configurable approval matrices and complex permissions, and a switch with role checks is that feature in disguise.
- `IActorProvider` must not accumulate identity concerns (claims, tenants, sessions). If it starts to, the boundary has been mistaken for a framework.

**Rejected alternatives**

- *Single fixed demo actor (§15.4 default):* simplest, but shows the same name creating and approving the ECO. Chosen against because it weakens the concept the demo exists to demonstrate.
- *Free-text actor field on each action form:* no seeding needed and fully flexible, but slow in a live demo and invites nonsense values ("test", "asdf") into the audit trail the demo is meant to showcase.
- *Real authentication (Identity, JWT, cookies):* explicitly out of scope (§2), and would consume the sessions the demo budget does not have (D-02).
