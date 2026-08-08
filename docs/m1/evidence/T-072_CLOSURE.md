# T-072 CLOSURE - Page and widget context envelope

**Status:** API certification complete. Closed.
**Commits:** `7a4dc615` (backend), `6a3937c1` (frontend), `b30f97a9` (prefixed hints)
**Evidence:** `docs/m1/evidence/T-071_T-072_T-073_certification_20260808_202350.txt`

## What was built

A typed `AssistantContextEnvelope` carrying route, page code, widget code,
selections, filters, last-result summary and evidence handles, sent by the dock
and accepted by `POST /api/assistant/ask`.

The central finding that shaped it: the transport already existed and the
narrowing never happened. `AskRequest` accepted `ContextChips`, the service
received them, and `AskAsync` then built its retrieval query from the question
alone and dropped them.

## The contract, and how each half is held

| Rule | How it is held |
|---|---|
| Context NARROWS retrieval | Identifiers become prefixed hints embedded with the question |
| Context is NOT evidence | The composer receives `request with { Context = null }` - structural, not a string check |
| Context never widens permission | Tenant, role and licence stay claim-derived; the envelope carries none of them |
| A client cannot stuff the embedding | Terms trimmed, capped at 120 chars each and 24 total |

Hints carry their kind and their value: `page:X`, `widget:Y`, `selection:f=v`,
`filter:k=v`. Before that correction a selection and a filter on the same field
produced the identical token, so ranking could not tell two kinds of hint apart.

The last-result summary and the evidence handles are carried but deliberately
NOT embedded. A number supplied by the client must never influence what the
retrieval layer treats as evidence.

## Certified

- Same question on two pages retrieved different evidence, with the cited
  identifiers recorded in the evidence file.
- A fabricated context marker placed in the route, page code, widget code, a
  selection and a filter never appeared in the answer. String absence alone
  could not prove this, since a legitimate chunk sentence contains the page
  code; a marker no chunk can contain is the only version of the test that can
  fail.
- 7 backend facts and 9 frontend tests, all green.

## Recorded, not fixed

- `pageCode` is the first path segment. On a detail route the last segment is a
  row identifier, not a page. The full route travels beside it, and one line
  changes when Worker 2's page definitions expose a definitive code.
- Snapshots execute unfiltered, so `filter_context_json` is `{}` in this pass.
