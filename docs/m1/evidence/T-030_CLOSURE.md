# T-030 CLOSURE RECORD - Emit and populate the presentation staging representation, source-shaped

Milestone / Phase : M1 / M1-P2
Database          : ppiq_presentation
Recorded          : 2026-08-05
Evidence          : docs/m1/evidence/T-030_staging_verification_20260805_183901.txt

--------------------------------------------------------------------------------
1. THE SMALLEST PERMANENT CORRECT IMPLEMENTATION WAS A VERIFICATION
--------------------------------------------------------------------------------

110_phase1_demo_source_shapes.sql already creates five source-shaped schemas with
ten tables, and generate_fleet_v2_donor.py already populates them. So the question
was not "materialise it" but "is what exists the staging representation the task
describes". Building a second one would have been scope absorption.

BOUNDED CHECK, NOT A NEW AUDIT. One read-only pass, no write, no engine call.

--------------------------------------------------------------------------------
2. WHAT EXISTS
--------------------------------------------------------------------------------

  src_meltshop_pg.heats                                630
  src_meltshop_pg.lf_treatment                         630
  src_caster_oracle_shape.cast_sequence                630
  src_caster_oracle_shape.cast_pieces                5,670
  src_hsm_oracle_shape.hsm_coils                     5,670
  src_hsm_oracle_shape.hsm_pass_measurements        39,690
  src_pkl_mssql_shape.pickle_orders                  5,670
  src_pkl_mssql_shape.qa_lab_results                17,010
  src_inspection_mysql_shape.parsytec_surface_defects 1,987
  src_inspection_mysql_shape.downtime_events           210

Three of the eight declared sources - a separate Downtime MySQL, a Yard file and
a QA file - have no schema. Downtime and inspection share
src_inspection_mysql_shape. Recorded, not treated as a T-030 defect.

--------------------------------------------------------------------------------
3. CLAUSE RESULTS
--------------------------------------------------------------------------------

  POPULATED     PASS   zero source tables with zero rows
  UNPREPARED    PASS   4 checks, zero offending
  IDENTITY      PASS   zero staging rows without a canonical match
  ROW COUNTS    RECORDED, with one finding - see section 5
  SURFACES      NOT VERIFIABLE from a database check

GENUINELY UNPREPARED, OPERATIONALISED RATHER THAN ASSERTED. A view is a pre-join,
a generated column is a derived column, and a foreign key from a source schema
into public is a declared link - a pre-join by another name. All three measured
zero. Canonical vocabulary was tested BY COLUMN NAME - material_unit_id,
material_code, material_unit_type, product_family, grade_or_recipe,
defect_catalog_id, parameter_definition_id, site_id, is_deleted, created_at_utc,
is_synthetic, source_record_id - because those are the words a finished model uses
and a customer system does not. Zero leaked.

The vocabulary the source tables DO use is customer-shaped and was printed for
contrast: coil_id, input_piece_id, heat_no, mill_line, rolling_start_time,
target_fdt_c and actual_fdt_c as a pair, coil_weight_kg, last_update_ts; and
heat_no, plant_code, furnace_code, steel_grade, route_code, tap_start_utc,
heat_weight_ton, source_updated_at_utc. That is what a customer system exposes,
not what a model produces.

--------------------------------------------------------------------------------
4. A CORRECTION TO MY OWN CHECK
--------------------------------------------------------------------------------

The runner reported IDENTITY as FAIL with 12,600 offending. THAT VERDICT WAS
WRONG, and the error was mine.

The frozen clause reads: "Identities match the canonical layer exactly - a coil
visible here is the same coil there". That is directional, staging to canonical.
Measured in that direction:

  staging coils with no canonical match      0
  staging heats with no canonical match      0

Every one of the 5,670 staging coils and 630 staging heats resolves to the same
material_code in canonical. THE CLAUSE PASSES.

I also counted the reverse - canonical rows absent from staging - and gated on it.
That converted an expected row-count difference into a failure. The reverse count
is reported in section 5 as a finding, which is where it belongs.

--------------------------------------------------------------------------------
5. THE ROW-COUNT DIFFERENCE, AND ONE FINDING INSIDE IT
--------------------------------------------------------------------------------

The frozen task says row counts are NOT expected to be equal and that a test
asserting equality would be wrong. No such test exists in the runner.

  entity                                        staging     canonical
  coils                                           5,670        17,010
  heats                                             630         1,890
  pass measurements vs parameter observations    39,690       301,560
  source downtime vs canonical downtime             210           630
  surface defects vs quality events               1,987         7,844

TWO DIFFERENT KINDS OF DIFFERENCE ARE MIXED IN THAT TABLE, and they should not be
read the same way.

SHAPE DIFFERENCE, EXPECTED. 39,690 pass measurements against 301,560 parameter
observations. One source row carrying target and actual pairs becomes several
canonical observations. This is exactly what the task anticipates.

POPULATION DIFFERENCE, A FINDING. Coils 5,670 against 17,010 and heats 630
against 1,890 are both EXACTLY 3:1. That is not shape - it is the same entity at
one third the count. The source shapes carry a 1x emission while canonical was
materialised at 3x. Downtime 210 against 630 is also exactly 3:1.

WHY IT IS RECORDED RATHER THAN FIXED. Nothing in the frozen T-030 text requires
staging to cover the whole canonical population, and the schema tree, canvas, SQL
editor and preview read structure and sample rows rather than a complete census.
A one-third representative emission may be entirely adequate for them. But the
ratio is exact and systematic, so it is a deliberate property of the generator
rather than drift, and a later task that assumes staging covers the plant would
be wrong. RECORDED, NOT REMEDIATED - T-030 does not own the generator.

--------------------------------------------------------------------------------
6. THE SURFACES CLAUSE
--------------------------------------------------------------------------------

"The schema tree, canvas, SQL editor and preview all read it successfully" cannot
be verified from a database check. A row count proves nothing about a surface.

These are frontend surfaces on the track whose browser acceptance is already
DEFERRED pending presentation convergence - see T-024_REQUIREMENT_8_DEFERRED.md.
No claim is made here, and none is manufactured.

--------------------------------------------------------------------------------
7. CROSS-REFERENCE TO T-029, NOT A NEW FINDING
--------------------------------------------------------------------------------

T-029 recorded the density cross-check as NOT COMPUTABLE because canonical has no
WEIGHT_KG or LENGTH_MM parameter code. src_hsm_oracle_shape.hsm_coils.coil_weight_kg
is populated on all 5,670 rows, 12,508.1 to 28,498.4 kg. So the weight exists at
the source and is simply not projected into canonical.

That is a T-029 emission finding with an estimate already recorded against it.
T-030 does not change the emission - not its scope.

--------------------------------------------------------------------------------
8. STATUS
--------------------------------------------------------------------------------

  T-030 implementation / execution   COMPLETE
  T-030 acceptance                   BLOCKED on the surfaces clause only

  Every database-verifiable clause passes: the staging representation is
  populated from the generator, is genuinely unprepared, and its identities match
  canonical exactly in the direction the contract states. The only unmet clause is
  that the schema tree, canvas, SQL editor and preview read it successfully, which
  belongs to the deferred presentation surfaces.

THE CORE DISTINCTION IS PRESERVED: source-shaped unprepared staging is not the
canonical plant model. No canonical vocabulary, no derived field and no
pre-joined convenience view leaks into staging.

--------------------------------------------------------------------------------
9. EVIDENCE
--------------------------------------------------------------------------------

  docs/m1/evidence/T-030_staging_verification_20260805_183901.txt
  tools/run/Invoke-PpiqT030StagingVerification.ps1
  Backend/database/scripts/110_phase1_demo_source_shapes.sql