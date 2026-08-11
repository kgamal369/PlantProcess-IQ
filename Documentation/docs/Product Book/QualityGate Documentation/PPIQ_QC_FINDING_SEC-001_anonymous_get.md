# QC FINDING SEC-001
## SECURITY ARCHITECTURE FAIL - QUALITY STOP - RELEASE BLOCKER
### The anonymous GET catch-all. Proven against the current tree, 10 August 2026.

**Verdict:** `SECURITY REMEDIATION REQUIRED` + `RELEASE BLOCKER`
**Quality Stop:** QS-03, ACTIVE (see section 7)
**Implementation owner:** Worker 2. **Worker 1 does not touch this** - the authorization model is on the structural override list.
**Worksheet for the owner:** `PPIQ_anonymous_get_routes.csv`, 139 rows, severity-classified.

---

## 1. TREE IDENTITY

The export contains no git metadata, so the measurement is bound to file identity rather than a commit SHA. That is stated rather than papered over.

| | |
|---|---|
| Export generated | 2026-08-09 15:38:22 |
| File | `Backend/PlantProcess.Api/Security/PlantAccessControl.cs` |
| File last written | 2026-08-09 10:02:02 |
| Lines | 365 |
| SHA-256 | `06055D3DD6AD423FB475C93F2A6B2BAC73704054BC62A6131482D6339098A6C4` |
| Extraction | verified byte-for-byte against the declared hash |

**Before acting, confirm this hash still matches the working tree.** If it has moved, re-run the proof script before treating the numbers below as current.

---

## 2. STEP 1 - GET ROUTES ENUMERATED

Every `Map*` call in `Backend/PlantProcess.Api` was collected from comment-stripped source, joined to its enclosing `MapGroup` prefix, and filtered to endpoint groups that `Program.cs` actually wires.

| | |
|---:|---|
| **540** | `Map*` calls found across the API project |
| **267** | GET routes in endpoint groups that are wired in `Program.cs` |
| **8** | files declaring endpoint groups that `Program.cs` never calls - dead route surface, recorded separately as SEC-002 |

The eight unwired files: `TwoStageImportEndpoints`, `RiskCalibrationEndpoints`, `RiskEvidenceEndpoints`, `SuggestionEndpoints`, `ValueEndpoints`, `ProvenanceGuardedAdvancedResultsEndpoints`, `SupervisorEndpoints`, `AlertEndpoints`.

---

## 3. STEP 2 - EXPLICIT AUTHORIZATION DECLARATIONS

40 matrix entries parsed from comment-stripped source. **10 grant anonymous access:**

| Prefix | Methods | Assessment |
|---|---|---|
| `/auth/login`, `/auth/refresh`, `/auth/logout`, `/auth/mfa/step-up` | POST | Correct and necessary |
| `/auth/provisioning` | GET, POST | Requires an owner decision |
| `/swagger` | GET | Should not be anonymous in a customer build |
| `/health`, `/db-health`, `/health/ready` | GET | Correct |
| **`/`** | **GET** | **THE CATCH-ALL** |

```
Backend/PlantProcess.Api/Security/PlantAccessControl.cs:189
    ("/", new[] { "GET" }, "anonymous", true),
```

**The codebase already knows.** Four separate comments in this same file describe the behaviour as a known, tolerated condition:

```
:205  // readiness/outcomes calls slip through anonymously via ("/", GET).
:216  // while GETs slip through anonymously via the ("/", GET, anonymous) entry.
:239  // currently fall through the ("/", GET, anonymous) entry and are served
:248  // GET /pages was quietly served through the ("/", GET, anonymous)
```

This is what makes it an architecture failure rather than a bug. The pattern was observed at least four times, closed one prefix at a time, and the invariant was never fixed. **The T-042 fix for `/pages` was correct and it was the fourth patch, not the cure.**

---

## 4. STEP 3 - ROUTES CURRENTLY FALLING THROUGH

Resolution mirrors `AccessControlMiddleware`: longest matching prefix wins, the catch-all excluded.

> ## **141 of 267 wired GET routes - 53 percent - are served without a token.**

Severity classification of 139 distinct routes:

| Severity | Count | What is exposed |
|---|---:|---|
| **CRITICAL** | **18** | Identity, sessions, role mappings, audit records, and the access model itself |
| **HIGH** | **45** | Customer plant data and derived analytical output, with no tenant context |
| **MEDIUM** | 52 | Operational and derived state |
| **LOW** | 24 | Health, version and static contract routes - probably intended, but undeclared |

### The eighteen critical routes, named

**The access control model, readable by anyone:**
`/api/p09/access/matrix` | `/api/p09/access/effective` | `/api/p09/access/check/{capability}` | `/api/p09/access/enforce/{capability}` | `/api/p09/executive/dashboard`

**Identity and sessions:**
`/auth/session` | `/api/v5/enterprise-identity/sessions` | `/api/v5/enterprise-identity/policy` | `/api/v5/enterprise-identity/health` | `/api/v5/sso/Users` | `/api/v5/sso/role-mappings` | `/api/v5/sso/ServiceProviderConfig` | `/api/v5/sso/health` | `/api/v5/identity-runtime/health` | `/api/v5/identity-runtime/scim/schema-proof`

**Audit:**
`/api/v5/compliance/audit/events` | `/api/v5/assistant-gateway/audit`

**Development surface:**
`/dev/database-summary` | `/dev/material-sample`

### The plant-data exposure is a second violation

Forty-five HIGH routes return customer plant data or analytical output. Because there is no token, **there is no tenant context**, so the tenant predicate has nothing to scope by. Among them: `/api/analytics/advanced/results`, `/api/analytics/advanced/runs`, all seven `/api/analytics/read-models/*`, `/phase2/genealogy/{materialCode}`, all seven `/plant-layout/*`, and eight `/reports/*` routes including two PDF exports.

**This breaches two rules at once:** the isolation rule that no analytical surface may serve a row without the declared path, and tenant isolation, which is a hard-override category on its own.

---

## 5. STEP 4 - EXPECTED BEHAVIOUR

| Route class | Required behaviour |
|---|---|
| Health, readiness, version | Anonymous, **declared explicitly** in the matrix. Never by fall-through. |
| Auth entry points | Anonymous, declared. Already correct. |
| Everything else | **Deny by default.** An explicit permission and tenant scope, identical to the write path. |
| A newly added route with no declaration | **Fails the build.** Not served, not warned about - refused. |

---

## 6. REQUIRED OUTCOME

The instruction is the invariant, not the list. **Do not close 139 routes one at a time and leave the catch-all in place** - that is the fifth patch.

1. **Remove anonymous fallback semantics.** Reads become deny-by-default exactly as writes are.
2. **Declare every intentionally anonymous endpoint explicitly**, one matrix entry each, with a written reason.
3. **Generate a route-by-role/auth matrix** from the route table rather than maintaining it by hand.
4. **A newly introduced unmapped route fails the gate.** This is the part that makes it permanent; the reflection-based proofs written for `/pages` in T-042 are the correct pattern, applied to the whole route table rather than one family.
5. **Decide the eight unwired endpoint files** - wire them or delete them. Dead route surface is a future instance of this same defect.

---

## 7. QUALITY STOP QS-03

| | |
|---|---|
| **Affected scope** | New GET endpoints across the API surface, and any work that would add route surface before the invariant is fixed. |
| **Evidence** | This document. 141 of 267 wired GET routes anonymous; catch-all at `PlantAccessControl.cs:189`; four in-file comments documenting prior encounters. |
| **Owner** | Worker 2 for implementation. Karim for the anonymous allowlist decisions. |
| **Exit criteria** | Catch-all removed; every intentionally anonymous route explicitly declared; the generated matrix exists; an added unmapped route fails the build, **falsified once** by adding one and observing the failure. |
| **Allowed unaffected work** | Everything that does not add or alter API route surface. Frontend, analytics, database, deployment, documentation and the M1 presentation slice all continue. **This is not a development freeze.** |
| **Waiver authority** | Karim, by ruling, recorded with its reason. |

---

## 8. WHY THIS IS NOT A MICRO-FIX

The time rule would permit a one-line deletion. The **structural override forbids it**, because the authorization model is on the list, and because deleting line 189 without first declaring the intentional anonymous routes would take `/health` and `/swagger` down with it and break the deploy health gate.

**I have not touched this file and will not.** My part is this proof, the acceptance criteria in section 7, and the regression gate in item 4 once Worker 2 has delivered.

---

## 9. WHAT I RECOMMEND, WITHOUT DECIDING IT

Sequence that avoids an outage: add explicit entries for the 24 LOW routes first, confirm the deploy health gate still passes, then remove line 189, then work the CRITICAL and HIGH lists. Roughly a day of Worker 2's time, plus your decisions on the allowlist.

Whether that sequence is right, whether `/swagger` and `/auth/provisioning` stay anonymous, and whether this precedes or follows the M1 presentation are **your rulings, not mine.**

---

*Proven 10 August 2026 by `prove_anon_get.py` against the 09 August export. Steps 1 to 4 as specified. Re-run before acting if the file hash has moved.*
