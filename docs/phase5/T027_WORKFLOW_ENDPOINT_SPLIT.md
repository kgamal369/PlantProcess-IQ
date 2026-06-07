# T-027 Workflow Endpoint Split

Marker: PPIQ_REALIZATION_T027_WORKFLOW_ENDPOINT_SPLIT

## Result

Phase1WorkflowTruthEndpoints and WorkflowEndpoints runtime mega-files were decomposed into partial route, handler, helper and contract files.

## Validation

Run:

    node tools/phase5/validate-t027-workflow-endpoint-split.cjs
    dotnet build Backend

## Backups

- Phase1WorkflowTruthEndpoints: .phase5_backup/t027_workflow_split_20260607_124324/Phase1WorkflowTruthEndpoints
- WorkflowEndpoints: .phase5_backup/t027_workflow_split_20260607_124324/WorkflowEndpoints
