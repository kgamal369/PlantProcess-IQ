# Release Evidence Envelope v1

Release evidence is a claim that something was certified. This directory freezes
the shape of that claim so a reader can check it instead of trusting it.

## Three authorities, one contract

| Authority | File | What it proves |
| --- | --- | --- |
| Structural | `release-evidence-envelope.v1.schema.json` | The portable field set, types and vocabulary. |
| Semantic | `../../../../tools/validation/Test-ReleaseEvidenceEnvelope.ps1` | The arithmetic and the verdict prerequisites a schema cannot express. |
| Drift guard | `ReleaseEvidenceEnvelopeContractTests.cs` | That the first two still say the same thing. |

A contract enforced by only one of the three has already drifted.

## Validating an envelope

    powershell -NoProfile -ExecutionPolicy Bypass \
      -File tools/validation/Test-ReleaseEvidenceEnvelope.ps1 -Path <evidence.json>

Exit 0 valid, exit 1 invalid, exit 2 unreadable. Add `-RepositoryRoot <path>` to
additionally prove every `evidence_paths` entry exists on disk. Without it the
check is structural, so it needs no repository, no database and no product
runtime, and any producer can run it anywhere.

The validator never edits an envelope. A producer that reported wrong counts has
a defect worth seeing, and a validator that quietly corrects it hides exactly the
thing release evidence exists to expose.

## The rules, in words

- `contract_version` is `1.0`. A change is a deliberate revision, not an edit.
- Evidence names a producer: at least one of `gate_id` or `suite_id`.
- `total_count = executed_count + skipped_count`. A skipped test is not executed.
- `executed_count = passed_count + failed_count`.
- `skip_reasons` counts sum to `skipped_count`. There are no mystery skips.
- Unapproved reasons sum to `unapproved_skip_count`, which cannot exceed
  `skipped_count`.
- `commit_sha` is a full 40-character Git SHA. A branch name or an abbreviation
  cannot identify what was certified.
- `evidence_paths` are repository-relative, so the envelope stays portable.
- `GREEN` is impossible with a failure or with an unapproved skip.

`RED` is never rejected for being `RED`. A producer may fail for a domain reason
this envelope cannot see, so the conditions above are necessary for `GREEN` and
not sufficient for it.

Unknown top-level fields are refused rather than absorbed. A typo such as
`fail_count` reaching a producer as a silent no-op is the failure this rule
exists to catch.

## Fixtures

`examples/` holds three envelopes that must be accepted: a database-shaped run,
a frontend-shaped run carrying an approved skip, and an all-skipped run that
reports zero executed.

`examples/negative/` holds ten controls, each isolating one rule. It is not
enough that they are rejected. A control that failed for the wrong reason would
prove nothing about the rule it names, so each is asserted against its own
reason code.

## Naming

No file here carries a task identifier in its name. Task identity belongs in the
payload and in the commit subject. A contract test enforces this.
