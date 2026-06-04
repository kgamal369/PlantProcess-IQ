# PlantProcess IQ — S2B Batch 3A Orphan Source Analysis

Generated at: 2026-06-04 11:55:53

Archive root:

`	ext
C:\Workspace\PlantProcess-IQ_Archive\S2B_Batch3_OrphanSource_20260604_115539

## Findings

| Area | Path | Status | Evidence |
|---|---|---|---|
| Backend/src | $(@{Area=Backend/src; Path=Backend\src; Status=Exists; Evidence=Files=6; Csproj=0}.Path) | Exists | Files=6; Csproj=0 |
| Backend/src | $(@{Area=Backend/src; Path=External references; Status=Clean; Evidence=No external text references found.}.Path) | Clean | No external text references found. |
| Frontend/src | $(@{Area=Frontend/src; Path=Frontend\src; Status=Exists; Evidence=Files=4}.Path) | Exists | Files=4 |
| Frontend/src | $(@{Area=Frontend/src; Path=External references; Status=BLOCKER; Evidence=Found 63 possible references to Frontend\src.}.Path) | BLOCKER | Found 63 possible references to Frontend\src. |

## Targets

| Path | Reason |
|---|---|
| $(@{RelativePath=Backend\src; FullPath=C:\Workspace\PlantProcess-IQ\Backend\src; Reason=Shadow/orphan backend skeleton outside real PlantProcess.* solution.}.RelativePath) | Shadow/orphan backend skeleton outside real PlantProcess.* solution. |
| $(@{RelativePath=Frontend\src; FullPath=C:\Workspace\PlantProcess-IQ\Frontend\src; Reason=Shadow/orphan frontend skeleton outside real Frontend\PlantProcess.Web app.}.RelativePath) | Shadow/orphan frontend skeleton outside real Frontend\PlantProcess.Web app. |
