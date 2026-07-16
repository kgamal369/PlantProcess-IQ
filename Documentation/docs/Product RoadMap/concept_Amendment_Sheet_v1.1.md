# concept.md - Amendment Sheet v1.0 -> v1.1
**15-Jul-2026, 15:10** | Apply these five amendments verbatim to concept.md, bump the header to v1.1, and append the changelog block at the end. This sheet deliberately does NOT regenerate the constitution - a governing document is amended surgically, never rewritten by tooling.

---

## Amendment 1 - Rule 1 Enforcement clause (MATERIAL - the document currently claims proof that does not exist)

**FIND (in Rule 1 - Generic Only):**
> **Enforcement:** the generic-only grep gate over the projection path; the migration-path gate (build fails if scripts/ or seed/ create demo-named objects); both gates falsified once (seen red) before trusted.

**REPLACE WITH:**
> **Enforcement (implemented today):** the architecture gate family in `src/test/architecture/` - route/phase-token gates (T09/T10), design-system gate (T11), `noMojibake`, and the `uiConformanceRatchet` (baseline may only decrease); the CI truth-gate test suite over the live Jenkinsfile; the website honesty-lint.
> **Enforcement (committed, not yet built - M2):** the migration-path gate (build fails if `scripts/` or `seed/` create demo-named objects; currently scripts 110/111/140/142/665 and the `seed/` demo files violate Rule 1 on every fresh install) and the generic-only grep gate over the projection path. Every new gate is falsified once - seen red - before it is trusted; a gate claimed in this document without a recorded red run is a defect in this document.

*Rationale: the v1.0 clause asserted two nonexistent gates in the present indicative - the exact "guard satisfiable by its own prose" failure this constitution forbids. The replacement states what is enforced today, names the installer violation honestly, and keeps the falsify-once covenant.*

---

## Amendment 2 - The causal pattern numbers (money slide)

**FIND (wherever the demo pattern effect size is stated, likely in the Journey/step-10 or demo-outcome section):**
> 9.5x (superheat -> CRACK_LONG)  *(or equivalent wording pairing 9.5x with superheat)*

**REPLACE WITH:**
> 9.3x (R1: CRACK_LONG ~ peritectic C-band x superheat x cast speed). 9.5x belongs to R3 (WAVY_EDGE ~ rolling force per gauge x roll wear). SCRATCH is the planted 1.0x null control. Source of truth: `ppiq-fleet-3months/FLEET_RELATIONS.md` (seed=42), 16 further planted relations R2-R17 available as supporting findings.

---

## Amendment 3 - Journey step 14 status

**FIND (step 14 / supervisor description, if it states the supervisor does not exist or is absent from the codebase):**
> *(any wording asserting step 14 exists nowhere / is entirely unbuilt)*

**REPLACE WITH:**
> Step 14 v0 exists as of 14-Jul: SupervisorEndpoints + SupervisorReportPage (report from results_v2, honesty line, monitor row). NOT yet: schedule, tuning actions, provenance rows for adjustments (no storage table exists). The M2 keystone remains the full closed-loop supervisor; v0 is the honest framing artifact only. Grade [B].

---

## Amendment 4 - Source taxonomy premise (Route A revision)

**FIND (wherever the document asserts that no definition/taxonomy tables exist in any source):**
> *(wording to the effect of: the sources contain only fact/event tables; no defect_definitions or parameter_definitions exist anywhere)*

**REPLACE WITH:**
> Verified 15-Jul against the live containers: parsytec (MySQL) SHIPS a real taxonomy table, `parsytec_defect_catalog` (20 codes) - defect taxonomy imports directly. No source carries parameter-definition tables; each source therefore exposes a read-only `v_parameter_definitions` view (created 15-Jul on meltshop-PG, caster-Oracle, hsm-Oracle), per the Emulation Doctrine (views live in the emulated sources, outside the product). The genealogy chain is present in-source with explicit keys: `cc_slabs(heat_id)` and `hsm_coils(slab_id)`.

---

## Amendment 5 - Version block

**FIND:** the version/date header line declaring v1.0.

**REPLACE WITH:** v1.1, 15-Jul-2026, and append at the document end:

> ## Changelog
> - **v1.1 (15-Jul-2026):** Amendment 1 corrected the Rule-1 Enforcement clause (removed the assertion of two unbuilt gates; recorded the implemented gate family and the M2 commitments honestly). Amendment 2 corrected the R1 effect size to 9.3x per FLEET_RELATIONS. Amendment 3 recorded supervisor v0 [B]. Amendment 4 revised the Route-A taxonomy premise against live source evidence. All derived documents (Roadmap v9+) re-validate against v1.1.
> - **v1.0 (12-Jul-2026):** initial constitution.

---

*Note on scope: nothing else in v1.0 was found false against the 15-Jul evidence base. The Three Rules, the 15-step journey, the demo-vs-product doctrine, and the enforcement philosophy stand unamended.*
