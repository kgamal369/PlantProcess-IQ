using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using PlantProcess.Application.Integration.Interfaces.SourceSystems;
using PlantProcess.Domain.Entities.Integration;
using PlantProcess.Domain.Enums.Integration;
using PlantProcess.Infrastructure.Persistence;

using PlantProcess.Api.ErrorHandling;

namespace PlantProcess.Api.Endpoints.Admin;

// PPIQ_REALIZATION_T027_PHASE1_WORKFLOW_TRUTH_HELPERS_SPLIT
public static partial class Phase1WorkflowTruthEndpoints
{
private static IReadOnlyList<CanonicalTargetRow> BuildCanonicalTargets()
    {
        return new List<CanonicalTargetRow>
        {
            new("MaterialUnit", "MaterialCode", "string", true, "Heat, slab, coil, batch, lot, roll, component, etc."),
            new("MaterialUnit", "MaterialUnitType", "string", true, "Generic material type."),
            new("MaterialUnit", "ProductFamily", "string", false, "Product family / business family."),
            new("MaterialUnit", "GradeOrRecipe", "string", false, "Grade, recipe, steel grade, pharma recipe, tire recipe, etc."),
            new("MaterialAlias", "ExternalId", "string", true, "Source-side ID or alternative piece/batch ID."),
            new("GenealogyEdge", "ParentMaterialCode", "string", true, "Parent material for genealogy."),
            new("GenealogyEdge", "ChildMaterialCode", "string", true, "Child material for genealogy."),
            new("ProcessStepExecution", "OperationCode", "string", true, "EAF, LF, Caster, HSM, PKL, Mix, Pack, Cure, etc."),
            new("ProcessStepExecution", "EquipmentCode", "string", true, "Generic equipment or line code."),
            new("ProcessStepExecution", "StartedAtUtc", "datetime", true, "Process step start time."),
            new("ProcessStepExecution", "EndedAtUtc", "datetime", false, "Process step end time."),
            new("ParameterObservation", "ParameterCode", "string", true, "Measured process parameter."),
            new("ParameterObservation", "NumericValue", "decimal", false, "Numeric measurement value."),
            new("ParameterObservation", "TextValue", "string", false, "Text measurement value."),
            new("ParameterObservation", "BooleanValue", "bool", false, "Boolean measurement value."),
            new("ParameterObservation", "ObservedAtUtc", "datetime", true, "Observation timestamp."),
            new("QualityEvent", "DefectCode", "string", false, "Source defect code mapped to DefectCatalog."),
            new("QualityEvent", "EventType", "string", true, "Defect, QA decision, inspection finding, lab issue, etc."),
            new("QualityEvent", "Decision", "string", false, "Accepted, downgraded, rejected, hold, rework."),
            new("QualityEvent", "EventAtUtc", "datetime", true, "Quality event timestamp."),
            new("DowntimeEvent", "ReasonCode", "string", true, "Downtime reason code."),
            new("DowntimeEvent", "StartedAtUtc", "datetime", true, "Downtime start."),
            new("DowntimeEvent", "EndedAtUtc", "datetime", false, "Downtime end.")
        };
    }
}
