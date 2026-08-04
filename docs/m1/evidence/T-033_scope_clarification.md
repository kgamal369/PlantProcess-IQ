# T-033 SCOPE CLARIFICATION - carried in from T-032

**Recorded** 04-Aug-2026, on the ruling that these are a scope clarification and NOT a new task.
**Owning task** T-033, "Shared Authoring Shell, part 2: relational block grammar on the board", M1-P2, Critical, 12 h.

T-033's contract is unchanged: implement source, join, filter, derived column and select or rename as board nodes with typed ports, keep drag-time refusal with a named reason per Chapter 4 section 5.2.7, keep the interface operator lists byte-identical to the server whitelist, and declare the rest of the design's blocks as unavailable rather than omitting them.

The three items below were surfaced by the T-032 browser walk. None is new scope; each is board behaviour that T-033 already owns or is the first task in a position to deliver.

## 1. ARRANGE - ruled into T-033

Chapter 4 sections 5.2.3 and 5.2.6 name automatic layout on the canvas toolbar. Zoom, fit and the minimap already exist through the board's own controls. **Arrange does not exist anywhere and no task text in backlog v2.9.1 covers it.**

Ruled 04-Aug: assign Arrange to T-033 as board behaviour. No non-functional Arrange control was added in T-032.

## 2. NODE AND EDGE REMOVAL

There is no visible control to remove a node or an edge. The board accepts only the Delete key, which requires canvas focus the author does not have after double-clicking a tree row. During the T-032 walk a table dropped by mistake could not be removed at all, and the page had to be reloaded.

Chapter 4 section 5.2.15 item 8 requires that delete edge and duplicate node both work or both be absent. Neither holds today, and a hidden keyboard shortcut a plant engineer will never discover is not "works".

This is carried behaviour, not a T-032 regression: `CanvasShell` and its props are identical to what `VisualJoinCanvasPage` used.

## 3. VALIDITY WHEN THE BOARD HAS A SOURCE AND NO OUTPUT

The validity indicator reads `Valid flow` with a single source table and no output block. That is defensible today, because one table compiles to a legitimate single-table SELECT and no output block exists.

It stops being defensible the moment `Output to canonical entity` and `Output to named dataset` become real board blocks in T-033. A graph with a source and no output is then incomplete, and section 5.2.7's rule - a required input unconnected at Run is refused before execution, naming the input - should catch it.

## 4. THE OPERATOR WHITELIST, MEASURED

T-033's validation requires the interface operator lists to stay byte-identical to what `BuildSafeSelect` enforces. Read from `Backend/PlantProcess.Api/Endpoints/Prep/VisualMapperEndpoints.cs`:

```
FilterOps = { "=", "<>", ">", ">=", "<", "<=", "LIKE", "NOT LIKE", "IS NULL", "IS NOT NULL" }
MathOps   = { "+", "-", "*", "/" }
```

Ten and four. The retired side panel carried exactly these, so the vitest T-033 asks for has a real server-side source of truth to compare against rather than a copy of a copy.

## 5. NOT IN T-033

Schema-tree drag, multi-select and search belong to T-034. Retiring `WidgetAuthoringPanel` belongs to T-038. Converging `AnalysisToolboxPage` as the S3 face belongs to T-065. The donor `src_*` schema names visible in the tree belong to T-030 and T-031, and the T-032 browser acceptance must be re-run against the regenerated staging representation once T-030 lands.