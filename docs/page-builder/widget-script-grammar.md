# Widget script grammar quick reference

The widget script layer lets a user bind a widget to a canonical view and define a safe aggregation without writing raw SQL.

## Example

```text
widget=chart;
chart=bar;
source=schema_view:defect_breakdown;
dimension=defectType;
measure=defectCount;
maxRows=20;
sort=desc;
```

## Safety rules

- No raw DDL/DML.
- All source views/columns must come from the canonical schema catalog.
- Save and execute must pass SafeSqlValidator.
- Unsupported columns, invalid aggregates, and type mismatches must return typed errors.
