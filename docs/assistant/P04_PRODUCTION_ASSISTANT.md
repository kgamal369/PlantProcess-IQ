# PlantProcess IQ — P4 Production Assistant

## Closed by P4A

- Replaced runtime `HashingEmbedder` with `LocalSemanticEmbedder`.
- Added a pluggable `IEmbedder` production boundary.
- Added idempotent retrieval index build service.
- Added SQL tables for provider/model config, retrieval index jobs, audit log, and eval cases.
- Added grounded gateway certification helpers.
- Added assistant eval harness with model-version pinning.
- Added UI proof page for grounded answers, citations, blocked claims, and abstention.
- Added hard tests covering local embedding relevance, index filtering, grounding, citations, and eval failure behavior.

## Air-gap rule

When `air_gapped = true`, the provider config must not contain an endpoint URL. The assistant remains local-only.

## Honesty rule

The assistant can only answer from retrieved handles and approved tools. Unsupported numbers, root-cause phrasing, and fabricated savings are blocked before reaching the UI.