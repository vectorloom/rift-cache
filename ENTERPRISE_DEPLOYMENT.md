# Enterprise Deployment Guide

This guide is for platform/infrastructure teams running RiftCache as **shared, centrally-owned infrastructure** for multiple internal teams — not for a single team self-hosting their own instance. If that's your situation, see the main [README](README.md) Quick Start instead; you don't need most of what's here.

Running RiftCache this way means other teams will build on it and depend on it being up, correct, and fast. That changes the requirements from "a container I run" to "infrastructure I operate." This guide walks through what's needed.

---

## 1. Choose an Isolation Model

This is the first decision, and it shapes everything else.

| Model | Description | Trade-off |
|---|---|---|
| **Shared instance, logical multi-tenancy** | One (or a few) RiftCache deployment(s), tenants scoped via `/api/v1/cache/{tenantId}/...` URLs and per-tenant API keys | Cheaper, simpler to operate. A noisy-neighbor team or bug can affect everyone sharing that instance. |
| **Per-team instance, shared platform template** | Each team gets their own RiftCache deployment, provisioned from a common IaC template you maintain centrally | Stronger isolation, contained blast radius. More instances to monitor; higher aggregate cost. |
| **Hybrid** | Shared instance for small/low-stakes teams; dedicated instances available on request for teams with stricter isolation, compliance, or performance needs | Most common landing point for orgs of meaningful size. Requires a policy for which teams get which tier. |

Decide this explicitly before building anything — retrofitting isolation later is painful.

---

## 2. Multi-Tenant Auth & Isolation

RiftCache's multi-tenant mode is opt-in (off by default for the simple self-host case). Turn it on and configure:

- **Per-tenant API keys**, issued and rotated centrally — don't let teams generate or manage their own.
- **`ISecretProvider` backed by a real secret store** (Azure Key Vault, AWS Secrets Manager, GCP Secret Manager — not the default environment-variable provider) so keys aren't hand-managed in config files.
- **Tenant-scoped rate limiting and quotas**, so one team's traffic spike can't degrade service for others.
- **Tenant-scoped storage isolation** at the persistence layer (separate containers/prefixes per tenant), so a misconfigured query can't cross tenant boundaries.

---

## 3. Capacity Planning for Aggregate Load

Sizing for one team (2-3 replicas, 0.5-1 vCPU) does not hold once you're serving every team in the org.

- Estimate **aggregate** throughput and memory needs across all teams you expect to onboard — not per-team averages, but realistic combined peak load.
- Tune auto-scaling rules for the combined traffic shape, not any single app's pattern.
- If running on a Dedicated Container Apps workload profile (or equivalent reserved compute), model headroom for the full org's load, and set utilization alerts (e.g., >75% aggregate) well before you'd need to scale out to a new node — reserved-capacity scaling happens in large, discrete steps, not gradually.
- Revisit capacity planning at each new team onboarding, not just at initial rollout.

---

## 4. Persistence Is Non-Negotiable

For a single team's optional cache, memory-only mode may be acceptable. For shared infrastructure other teams build products on, persistence must be:

- **Configured** — a real `IPersistenceProvider` (Azure Blob, S3, GCS), not the default no-op provider.
- **Tested** — verify data actually survives a replica restart and a full redeploy before any team goes live on it.
- **Documented** — teams will assume durability unless you explicitly tell them otherwise (e.g., "this is best-effort, TTL-bounded cache, not a system of record").

---

## 5. Reliability & Ownership

This is the most commonly underestimated part of running shared platform infrastructure. Once other teams depend on it, it needs the same operational rigor as any other critical internal service:

- **A defined SLA**, even an informal one (e.g., "99.9% availability, best-effort support during business hours"). Ambiguity here causes friction the first time something breaks.
- **Clear on-call ownership** — a specific team's pager goes off when it's down, not "whoever notices."
- **Runbooks** for known failure modes: replica crash, persistence backend outage, key rotation, tenant quota breach.
- **Rollout discipline for upgrades** — canary new versions against one low-stakes tenant before rolling out broadly, with a tested rollback path. A shared cache going down, or silently corrupting data, affects every dependent team simultaneously — treat upgrades with the caution that implies.

---

## 6. Per-Tenant Observability

The core OpenTelemetry integration gives you service-wide metrics out of the box. For a multi-tenant deployment, extend this so:

- Each team can see **their own** hit rate, latency, and error rate — not just aggregate numbers.
- You can spot a single noisy or misbehaving tenant before it affects others.
- Dashboards support per-tenant filtering, not just org-wide totals.

---

## 7. Onboarding Process

Decide how a new team actually starts using the shared service:

- **Self-service**: a form or internal portal that provisions a tenant and API key automatically. Scales well past a handful of teams, but requires upfront tooling investment.
- **Central team provisions manually**: simpler to start, fine for early adoption, but doesn't scale past a small number of teams without becoming a bottleneck.

Most orgs start manual and move to self-service once demand outpaces what the platform team can hand-provision.

---

## 8. Cost Allocation

If a central platform team owns the Azure/AWS/GCP bill, other teams will eventually ask for their share of the cost — especially relevant if you're running on a Dedicated/reserved-capacity billing model, where cost isn't naturally attributable per app the way Consumption billing is.

- Tag resources and/or track usage by tenant from day one.
- Decide early whether cost is charged back to teams, absorbed centrally, or something in between — this is an organizational decision as much as a technical one, but the tagging needs to exist regardless of which way you decide.

---

## 9. Security & Compliance Review

Centralized infrastructure touching multiple teams' data typically draws InfoSec attention. Be ready with:

- Encryption at rest and in transit (should already hold true via HTTPS + encrypted persistence backend — confirm, don't assume).
- Audit logging of access per tenant (who accessed what, when).
- A documented incident-response plan for a cross-tenant data exposure scenario — even if you consider it unlikely, "here's what happens if it occurs" is what a security review will ask for, not "we don't expect it to happen."

---

## Rollout Approach

If you're introducing RiftCache as new shared infrastructure (rather than migrating from an existing cache), a phased approach reduces risk:

1. **Pilot** with 1-2 low-stakes teams to validate the deployment, persistence, and monitoring setup under real usage.
2. **Expand gradually** to additional teams in waves, watching aggregate capacity and per-tenant metrics at each step.
3. **Formalize the SLA and on-call rotation** once a meaningful number of teams depend on it — informal ownership doesn't scale past the pilot phase.

Keep every team's option to run their own dedicated instance instead (per the isolation model in section 1) if the shared service doesn't meet their needs — this keeps the shared platform optional infrastructure, not a hard dependency imposed on every team.

---

## Related Docs

- [ARCHITECTURE_NOTES.md](ARCHITECTURE_NOTES.md) — provider abstraction interfaces referenced throughout this guide
- [ROADMAP.md](ROADMAP.md) — where multi-tenancy hardening and enterprise features sit in the project timeline
- [CONTRIBUTING.md](CONTRIBUTING.md) — how to contribute a new cloud provider if your org needs one not yet supported
