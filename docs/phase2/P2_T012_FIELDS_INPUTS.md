# P2-T012 — Fields / Inputs Inventory + StandardFields Gate

Marker: PPIQ_P2_T012_FIELDS_INPUTS_STANDARDIZATION

## Goal

P2-T012 locks the canonical field/input contract.

Scope:

- StandardInput
- StandardSelect
- StandardTextArea
- Native input/select/textarea audit
- Field inventory

Inventory output:

- `Frontend/PlantProcess.Web/docs/ui-standards/input-inventory.csv`

Captured fields:

- file
- page
- current implementation
- field type
- label text
- intent
- validation behavior
- error state
- helper text support
- label position
- required marker style
- focus ring
- standard/non-standard status

## Validation

Run:

    node tools/phase2/validate-p2-t012-fields-inputs.cjs
    cd Frontend/PlantProcess.Web
    npm run validate:fields
    npm run validate:tabs
    npm run validate:tables
    npm run validate:standard-imports
    npm run validate:action-buttons
    npm run build