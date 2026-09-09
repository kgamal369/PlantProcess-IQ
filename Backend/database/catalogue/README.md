# Physical database catalogue

The generated answer to one question: what physically exists in the database,
why does it exist, who owns it, and is every object governed.

## Authority, in precedence order

1. `physical-object-classification.tsv` - explicit per-object adjudication. One
   line beats everything else and may carry measured creator provenance when
   dynamic or retired SQL cannot be recovered by literal DDL scanning.
2. `classification-rules.tsv` - schema-scoped rules. The three governed schemas
   carry their meaning in their names, so membership of a schema is a governed
   fact rather than a guess about a table name. There are no name-pattern rules
   here: the earlier attempt had sixteen patterns that matched nothing, and a
   rule that protects nothing is a rule somebody believes is protecting
   something.
3. `public-platform-allowlist.tsv` - the only objects permitted in `public` as
   platform infrastructure.

Anything in `public` that is not allowlisted must resolve to a creator on the
canonical path or in repository history; it is then catalogued as bounded
compatibility with a retirement owner. Calling it platform to reach green is
the one thing this catalogue exists to prevent.

## Creator authority

The generator resolves the creating script for every object from
`canonical-migration-order.json` (createsTables) and from the DDL in the SQL
files themselves, canonical and offPath alike. Classification is derived from
that measured creator plus the governed schema, not from the object's name.

## Regenerate

    powershell -ExecutionPolicy Bypass -File tools\db\New-PhysicalDatabaseCatalogue.ps1 -Database ppiq_acceptance_empty

`generated/` is rewritten whole and carries no timestamps, so a rerun against an
unchanged database is byte-identical.

## Scope

Read-only. Object deletion, renaming and runtime consumer convergence belong to
T-256, not here. T-251 requires debt to be known and governed, not removed.