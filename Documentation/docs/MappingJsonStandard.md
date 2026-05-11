# PlantProcess IQ Mapping JSON Standard

PlantProcess IQ mapping definitions use a simple field-map object:

```json
{
  "CanonicalFieldName": "source_column_name"
}
```

The key is the canonical entity field expected by the mapping engine. The value is the raw JSON property name stored in `staging_records.raw_json`.

## Supported Phase C Targets

- `MaterialUnit`
- `MaterialAlias`
- `ProcessStepExecution`
- `ParameterObservation`
- `QualityEvent`
- `GenealogyEdge`

## Example: MaterialUnit

```json
{
  "MaterialCode": "material_id",
  "MaterialUnitType": "material_type",
  "SiteCode": "site_code",
  "ProductFamily": "product_family",
  "GradeOrRecipe": "grade",
  "ProductionStartUtc": "start_utc",
  "ProductionEndUtc": "end_utc"
}
```

## Example: ProcessStepExecution

```json
{
  "MaterialCode": "piece_id",
  "OperationType": "operation",
  "OperationCode": "operation_code",
  "EquipmentCode": "equipment_code",
  "StartedAtUtc": "start_utc",
  "EndedAtUtc": "end_utc",
  "CrewCode": "crew",
  "ExecutionStatus": "status"
}
```

## Example: ParameterObservation

```json
{
  "MaterialCode": "piece_id",
  "ParameterCode": "tag",
  "NumericValue": "value",
  "ObservedAtUtc": "sample_time",
  "QualityFlag": "quality",
  "EquipmentCode": "equipment_code",
  "UnitOfMeasure": "unit"
}
```

## Example: QualityEvent

```json
{
  "MaterialCode": "coil_id",
  "EventType": "quality_event",
  "EventAtUtc": "event_time",
  "Severity": "severity",
  "Decision": "decision",
  "DefectCode": "defect_code",
  "Description": "description"
}
```

## Execution Rules

1. The mapping engine only maps rows already stored in `staging_records`.
2. Preview mode validates and resolves references without saving canonical records.
3. Execute mode creates canonical records and marks staging rows as `Mapped`, `Failed`, or `Skipped`.
4. Failed rows keep `processing_error` for audit and replay.
5. Steel terms must stay in data/configuration, not in code.
