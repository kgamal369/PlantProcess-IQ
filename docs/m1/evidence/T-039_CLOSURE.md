# T-039 CLOSURE RECORD

**Task.** T-039 - Build IDefinitionService with a compatibility adapter over current
persistence. M1 / M1-P2, Backend / Definition store, Critical, 12 hours.

**Status: DONE**, closed on its frozen acceptance, 06-Aug-2026.

---

## What was accepted

| Acceptance item | Result |
|---|---|
| DefinitionKind contract | GREEN |
| 11 kinds represented | GREEN |
| Six IDefinitionService methods | GREEN |
| Contract tests | GREEN |
| Widget adapter | GREEN |
| Widget create produces version 1 | GREEN |
| Widget GetVersion(1) returns the immutable snapshot | GREEN |
| Widget update produces version 2 | GREEN |
| Versions 1 and 2 both immutable | GREEN |

Frozen integration validation, executed against a live database:

    Failed  0
    Passed  2
    Skipped 0

The validation names no table. That is the acceptance criterion the task states,
so it survives M2a replacing the storage underneath the service.

## What this does and does not mean

THE CONTRACT REPRESENTS ELEVEN DEFINITION KINDS. IT DOES NOT MEAN ELEVEN
PERSISTENCE ADAPTERS ARE IMPLEMENTED.

**Widget is the certified M1 implementation.** Every other kind returns an
explicit refusal. No kind returns a synthesised or fictitious version history,
and none may be made to: a fabricated history reads as a fact and is not one.

## What was built

- `Backend/PlantProcess.Application/Definitions/DefinitionKind.cs` - eleven
  members, with 0 reserved so an unset value cannot pass as a kind.
- `Backend/PlantProcess.Application/Definitions/Contracts/DefinitionSnapshot.cs`
- `Backend/PlantProcess.Application/Definitions/Interfaces/IDefinitionService.cs`
- `Backend/database/scripts/770_t039_definition_version_store.sql` - the M1
  compatibility snapshot store, in the authoritative numbered rebuild path.
  Idempotent, UNIQUE on (definition_kind, definition_id, version_number).
- `Backend/PlantProcess.Domain/Entities/Definitions/DefinitionVersion.cs`
- `Backend/PlantProcess.Infrastructure/Definitions/DefinitionService.cs`

`DashboardWidgetDefinition` remains the operational, current Widget definition.
The snapshot store holds history beside it and never replaces it.

Every write path opens one transaction, takes a row lock on the operational
widget row, allocates the version number under that lock, writes both the
current definition and the snapshot, and only then commits. A successful update
without its version record cannot exist. The unique index is the database
backstop.

---

## M2a CONVERGENCE ITEM, carried forward

**Establish one durable Transformation definition identity across Block/Board and
SQL authoring, then adapt or migrate both current history stores behind
IDefinitionService without changing its public contract.**

M2a must decide the authoritative identity BEFORE either existing version
sequence is migrated. Neither store is retired by T-039.

### The evidence this item rests on

Two live Transformation version stores exist, and they are not variants of each
other. They differ in identity, not only in shape.

| | ppiq_visual_mapper_versions | ppiq_mapping_versions |
|---|---|---|
| Identity | `session_id` | `mapping_code` (text) |
| Semantics | authoring workspace history | SQL-definition version history |
| Written by | board publish: `VisualMapperEndpoints`, `V5VisualMapperEndpoints` | `AuthoringSupportEndpoints.SaveSqlVersion` |
| Row identity | tenant + session + version_number | per-version uuid `id` |
| Allocation | next number computed inside a transaction | `COALESCE(MAX)+1` inside one INSERT ... SELECT |

`IDefinitionService` takes a `Guid definitionId`. NEITHER STORE HAS A
DEFINITION-LEVEL GUID: one has a session Guid, the other a per-version row Guid
plus a text code. The only durable bridge in the tree is the `MappingDefinition`
entity, which carries a Guid `Id` and a `MappingCode` - but nothing links
`ppiq_mapping_versions.mapping_code` to it by constraint. They share a naming
convention, not a key.

A visual-mapper session is not a durable definition identity. Redefining
`definitionId` to mean "session" for the Transformation kind alone would corrupt
the service contract. Adapting only `ppiq_mapping_versions` through
`mapping_code` would cover one side of the current Transformation authoring
model and silently omit the board-publish history.

### Explicitly NOT invented by T-039

- a Guid-to-code identity convention
- a new Transformation identity table
- a session-as-definition semantic
- a partial adapter presented as a complete Transformation adapter

Those are M2a definition-identity convergence decisions.

---

## One code-comment correction made under this task

`Backend/PlantProcess.Api/Endpoints/Prep/AuthoringSupportEndpoints.cs` carried a
comment stating that the graph path already used `ppiq_mapping_versions`. It does
not; the board publish path writes `ppiq_visual_mapper_versions`. The code was
correct and the sentence was stale. Corrected as documentation, not as a design
change.