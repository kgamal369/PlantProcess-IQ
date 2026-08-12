#!/usr/bin/env python3
"""PPIQ cross-document convergence scan, V4. FAIL-CLOSED.

Scans every document in the converged set, not one file.
A missing required document is a FAILURE, never a pass.
Negative checks: withdrawn contracts must not appear in an active body.
Positive checks: the target architecture must be asserted where it is owned.
Exit 0 only if every check passes.
"""
import os, re, sys

# (path, is_required, active_body_cutoff_pattern_or_None)
DOCS = [
 ("PPIQ_Chapter2_Technical_Overview_RevisionNext.md",                        True,  None),
 ("PPIQ_Chapter3_General_Technical_Function_Description_RevisionNext.md",    True,  None),
 ("PPIQ_Chapter4_Specific_Technical_Function_Description_RevisionNext.md",   True,  None),
 ("PPIQ_Chapter6_Infrastructure_Website_Administration_RevisionNext.md",     True,  None),
 ("PPIQ_Layer_B_Architecture_Design_Pack.md",                                True,  r"^## 48\."),
 ("PPIQ_Layer_B_Rule_Revision7.md",                                          True,  None),
 ("PPIQ_Layer_B_Design_Pack_Batch_Mode_Order.md",                            True,  r"RETIREMENT NOTICE"),
 ("PPIQ_Engine_ML_Onboarding_Brief_AR.md",                                   True,  None),
 ("PPIQ_Final_Synchronisation_Ledger.md",                                    True,  r"^## 1\. RESOURCE"),
 ("PPIQ_Master_Design_Chapter_Amendment_Pack.md",                            True,  r"^## A2 -"),
 ("PPIQ_AI_ML_LLM_Target_Architecture_Optimisation.md",                      True,  r"^## 1\. DECISION MATRIX"),
]

# Documents whose purpose is to record what was withdrawn are exempt from
# naming-only negatives, but never from structural ones.
LEDGER_LIKE = {
 "PPIQ_Final_Synchronisation_Ledger.md",
 "PPIQ_Master_Design_Chapter_Amendment_Pack.md",
 "PPIQ_AI_ML_LLM_Target_Architecture_Optimisation.md",
 "PPIQ_Layer_B_Design_Pack_Batch_Mode_Order.md",
}

# A line that explicitly withdraws, negates or archives a term is not an assertion
# of it. Correcting terminology requires naming what is being corrected.
NEGATION = re.compile(
    r"(?i)\b(not|never|no longer|withdrawn|withdraws|superseded|historical|"
    r"non-implementable|must not|forbidden|do not|deprecated|retired|"
    r"the collective phrase|replaced by|instead of)\b")

NEG = [
 ("SemanticModelVersion",              r"SemanticModelVersion",                            False),
 ("semantic_model_version_id",         r"semantic_model_version_id",                       False),
 ("ModelBundle",                       r"ModelBundle",                                     False),
 ("bundle_version",                    r"bundle_version",                                  False),
 ("seven ML models",                   r"(?i)seven ML models",                             False),
 ("seven model families",              r"(?i)seven model families",                        False),
 ("Model Trainers MF-01..MF-07",       r"Model Trainers\*?\*? \(MF-01 to MF-07\)",         False),
 ("G-01..G-46 / 46 gates",             r"(?i)(G-01\s*(to|through|\.\.)\s*G-46|46 gates|46 gate\b)", False),
 ("ml parallelism = 1",                r"(?i)`?ml`?[^\n]{0,30}parallelism\s*(is|=)\s*1",   False),
 ("single-predicate admission",        r"sum\([^)]{0,40}\)\s*\+\s*[^<]{0,30}<=\s*parallelism", False),
 ("batch on ppiq-ml-online",           r"(?i)batch[^\n]{0,60}(may run on either|on `?ppiq-ml-online)", False),
 ("PG sequence array payload",         r"`?values`?\s*\|?\s*float32\[\]",                  False),
 ("training reads feature_store",      r"(?i)training (reads|queries)[^\n]{0,30}feature_store", False),
 ("absolute text prohibition",         r"(?i)no statistic, score or value is ever computed from text", False),
 ("stale chapter-sync claim",          r"(?i)chapter files[^\n]{0,60}read-only inputs",     True),
 ("feature_matrix",                    r"feature_matrix",                                  False),
 ("no GPU required",                   r"(?i)no GPU required",                             False),
]

# (name, pattern, list of docs that MUST assert it)
POS = [
 ("Semantic Contract Manifest",   r"(?i)Semantic Contract Manifest",
  ["PPIQ_Chapter2_Technical_Overview_RevisionNext.md","PPIQ_Layer_B_Rule_Revision7.md","PPIQ_Layer_B_Architecture_Design_Pack.md"]),
 ("manifest_id PK",               r"(?i)`manifest_id`[^\n]{0,80}(PRIMARY KEY|PK)",
  ["PPIQ_Chapter3_General_Technical_Function_Description_RevisionNext.md","PPIQ_Layer_B_Rule_Revision7.md"]),
 ("UNIQUE(tenant_id,manifest_hash)", r"UNIQUE `?\(tenant_id, manifest_hash\)`?",
  ["PPIQ_Chapter3_General_Technical_Function_Description_RevisionNext.md","PPIQ_Layer_B_Rule_Revision7.md"]),
 ("manifest coverage rule",       r"(?i)every new governed AI/ML execution",
  ["PPIQ_Chapter3_General_Technical_Function_Description_RevisionNext.md","PPIQ_Layer_B_Rule_Revision7.md"]),
 ("max_concurrency + capacity",   r"resource_capacity",
  ["PPIQ_Chapter4_Specific_Technical_Function_Description_RevisionNext.md","PPIQ_Layer_B_Rule_Revision7.md","PPIQ_Layer_B_Architecture_Design_Pack.md"]),
 ("three ml lanes",               r"ml\.online_scoring",
  ["PPIQ_Chapter4_Specific_Technical_Function_Description_RevisionNext.md","PPIQ_Chapter6_Infrastructure_Website_Administration_RevisionNext.md","PPIQ_Layer_B_Rule_Revision7.md","PPIQ_Layer_B_Architecture_Design_Pack.md","PPIQ_Engine_ML_Onboarding_Brief_AR.md"]),
 ("hard-reserved online scoring", r"(?i)hard-reserved",
  ["PPIQ_Chapter2_Technical_Overview_RevisionNext.md","PPIQ_Chapter4_Specific_Technical_Function_Description_RevisionNext.md","PPIQ_Chapter6_Infrastructure_Website_Administration_RevisionNext.md","PPIQ_Layer_B_Rule_Revision7.md"]),
 ("columnar training path",       r"(?i)typed columnar artifact",
  ["PPIQ_Chapter3_General_Technical_Function_Description_RevisionNext.md","PPIQ_Layer_B_Rule_Revision7.md","PPIQ_Layer_B_Architecture_Design_Pack.md"]),
 ("materialiser exemption",       r"(?i)materialiser is (the sole exception|exempt)",
  ["PPIQ_Chapter3_General_Technical_Function_Description_RevisionNext.md","PPIQ_Layer_B_Rule_Revision7.md","PPIQ_Layer_B_Architecture_Design_Pack.md"]),
 ("sequence_manifests",           r"sequence_manifests",
  ["PPIQ_Chapter3_General_Technical_Function_Description_RevisionNext.md","PPIQ_Layer_B_Rule_Revision7.md","PPIQ_Layer_B_Architecture_Design_Pack.md"]),
 ("object-storage sequence payload", r"(?i)(object storage holds the (numeric )?payload|no numeric sequence payload is stored in PostgreSQL)",
  ["PPIQ_Chapter3_General_Technical_Function_Description_RevisionNext.md","PPIQ_Layer_B_Rule_Revision7.md"]),
 ("three-dimensional promotion",  r"(?i)three-dimensional",
  ["PPIQ_Chapter4_Specific_Technical_Function_Description_RevisionNext.md","PPIQ_Layer_B_Rule_Revision7.md","PPIQ_Layer_B_Architecture_Design_Pack.md"]),
 ("exact Flat recall baseline",   r"(?i)exact Flat",
  ["PPIQ_Chapter3_General_Technical_Function_Description_RevisionNext.md","PPIQ_Layer_B_Rule_Revision7.md","PPIQ_Layer_B_Architecture_Design_Pack.md"]),
 ("deterministic tool planner",   r"(?i)deterministic tool planner",
  ["PPIQ_Chapter4_Specific_Technical_Function_Description_RevisionNext.md","PPIQ_Layer_B_Rule_Revision7.md","PPIQ_Layer_B_Architecture_Design_Pack.md"]),
 ("permission before ranking",    r"(?i)before ranking",
  ["PPIQ_Chapter4_Specific_Technical_Function_Description_RevisionNext.md","PPIQ_Layer_B_Rule_Revision7.md"]),
 ("governance-based boundary",    r"(?i)free-form or model-generated",
  ["PPIQ_Chapter4_Specific_Technical_Function_Description_RevisionNext.md","PPIQ_Layer_B_Rule_Revision7.md","PPIQ_Layer_B_Architecture_Design_Pack.md"]),
 ("G-01 through G-55",            r"G-01\s*(to|through|\.\.)\s*G-55",
  ["PPIQ_Layer_B_Rule_Revision7.md","PPIQ_Layer_B_Architecture_Design_Pack.md","PPIQ_Engine_ML_Onboarding_Brief_AR.md"]),
 ("B-01 through B-09",            r"B-09",
  ["PPIQ_Chapter4_Specific_Technical_Function_Description_RevisionNext.md","PPIQ_Chapter6_Infrastructure_Website_Administration_RevisionNext.md","PPIQ_Layer_B_Rule_Revision7.md","PPIQ_Engine_ML_Onboarding_Brief_AR.md"]),
 ("model_registry per serving id",r"(?i)serving identity",
  ["PPIQ_Chapter2_Technical_Overview_RevisionNext.md","PPIQ_Layer_B_Rule_Revision7.md","PPIQ_Layer_B_Architecture_Design_Pack.md"]),
 ("intelligence and engine families", r"(?i)intelligence and engine famil",
  ["PPIQ_Chapter2_Technical_Overview_RevisionNext.md","PPIQ_Chapter4_Specific_Technical_Function_Description_RevisionNext.md","PPIQ_Layer_B_Rule_Revision7.md","PPIQ_Layer_B_Architecture_Design_Pack.md"]),
]

fails = 0
bodies = {}

print("PPIQ CROSS-DOCUMENT CONVERGENCE SCAN V4 - FAIL-CLOSED")
print("=" * 72)
print("DOCUMENT PRESENCE")
print("-" * 72)
for path, required, cutoff in DOCS:
    if not os.path.exists(path):
        print(f"  MISSING (FAIL)  {path}")
        fails += 1
        continue
    text = open(path, encoding='utf-8', errors='replace').read()
    lines = text.split('\n')
    end = len(lines)
    if cutoff:
        for i, l in enumerate(lines):
            if re.search(cutoff, l):
                end = i
                break
    bodies[path] = lines[:end]
    print(f"  present         {path}  ({len(text):>7} bytes, active body {end} lines)")

print()
print("NEGATIVE CHECKS - withdrawn contracts must not appear in an active body")
print("-" * 72)
for name, pat, all_docs in NEG:
    hits = []
    for path, body in bodies.items():
        if not all_docs and path in LEDGER_LIKE:
            continue
        for i, l in enumerate(body):
            if re.search(pat, l) and not NEGATION.search(l):
                hits.append((path, i + 1, l))
    if hits: fails += 1
    print(f"  {name:<34}{len(hits):>4}  {'PASS' if not hits else 'FAIL'}")
    for path, ln, l in hits[:3]:
        print(f"        {os.path.basename(path)}:{ln}  {l.strip()[:78]}")

print()
print("POSITIVE CHECKS - the target must be asserted where it is owned")
print("-" * 72)
for name, pat, owners in POS:
    missing = []
    for owner in owners:
        body = bodies.get(owner)
        if body is None or not re.search(pat, '\n'.join(body)):
            missing.append(os.path.basename(owner))
    if missing: fails += 1
    print(f"  {name:<34}{len(owners) - len(missing):>2}/{len(owners)}  {'PASS' if not missing else 'FAIL'}")
    for m in missing:
        print(f"        missing in {m}")

print()
print("STRUCTURAL CHECKS")
print("-" * 72)
pack = bodies.get("PPIQ_Layer_B_Architecture_Design_Pack.md", [])
nums = [re.match(r'^## (\d+)\.', l).group(1) for l in pack if re.match(r'^## \d+\.', l)]
dups = sorted({n for n in nums if nums.count(n) > 1})
print(f"  {'duplicate pack section numbers':<34}{len(dups):>4}  {'PASS' if not dups else 'FAIL'}")
if dups: fails += 1

order_active = os.path.exists("PPIQ_Layer_B_Design_Pack_Batch_Mode_Order.md") and \
    "HISTORICAL" in open("PPIQ_Layer_B_Design_Pack_Batch_Mode_Order.md", encoding='utf-8').read()[:600]
print(f"  {'batch order marked historical':<34}{'':>4}  {'PASS' if order_active else 'FAIL'}")
if not order_active: fails += 1

rule_single = os.path.exists("PPIQ_Layer_B_Rule_Revision7.md") and \
    "Appendix B" not in '\n'.join(bodies.get("PPIQ_Layer_B_Rule_Revision7.md", []))
print(f"  {'rule has no override chain':<34}{'':>4}  {'PASS' if rule_single else 'FAIL'}")
if not rule_single: fails += 1

print("=" * 72)
print("RESULT:", "SCAN V4 PASSED - CONVERGED" if fails == 0 else f"SCAN V4 FAILED - {fails} check(s)")
sys.exit(1 if fails else 0)
