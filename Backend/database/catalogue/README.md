# Physical database catalogue

The generated answer to one question: what physically exists in the database,
why does it exist, who owns it, and is every object governed.

## Authority files, in precedence order

1. `physical-object-classification.tsv` - explicit per-object adjudication.
   One line beats every rule. This is where a human decision lives.
2. `classification-rules.tsv` - pattern rules. Data, not code, so classifying a
   family of objects is a reviewed change rather than a source edit.
3. `public-platform-allowlist.tsv` - the only objects permitted to live in
   `public`. Anything else in `public` is a product object out of place.

An object that no explicit line and no rule classifies is UNCLASSIFIED, and
UNCLASSIFIED is red. That default is the point: a new table is ungoverned until
somebody governs it on purpose.

## Generated files

`generated/` holds the census. Nothing in it is hand-edited; the generator
rewrites it whole. The hashed artifacts carry no timestamp so that a rerun on an
unchanged database is byte-identical - determinism is the proof that the
catalogue reflects the database rather than the moment it was taken.
`catalogue-run-receipt.txt` is the one file that does carry timestamps, and it
is excluded from every determinism comparison.

## Regenerate

    powershell -ExecutionPolicy Bypass -File tools\db\New-PhysicalDatabaseCatalogue.ps1 -Database ppiq_acceptance_empty

## Scope

Read-only. The generator issues no DDL against the database it inspects. Object
deletion, renaming and runtime consumer convergence are not this authority's
business.