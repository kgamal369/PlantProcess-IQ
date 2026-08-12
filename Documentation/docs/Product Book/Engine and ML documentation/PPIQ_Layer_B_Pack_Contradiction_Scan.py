#!/usr/bin/env python3
"""Contradiction scan V3 for the PPIQ Layer B Architecture Pack.
Negative checks: stale contracts must not appear in the active body.
Positive checks: the canonical final state must be asserted in the active body.
Active body = lines 1 to the start of section 48, the historical ledger.
Exit 0 only if every check passes."""
import re, sys

PATH = 'PPIQ_Layer_B_Architecture_Design_Pack.md'
text = open(PATH, encoding='utf-8').read()
lines = text.split('\n')
ledger = next(i for i, l in enumerate(lines) if l.startswith('## 48.'))
active = lines[:ledger]
body = '\n'.join(active)

NEG = [
 ("ModelBundle",                     r"ModelBundle"),
 ("bundle_version",                  r"bundle_version"),
 ("any use of 'bundle'",             r"(?i)bundle"),
 ("Wide physical feature store",     r"(?i)wide physical feature store"),
 ("physically wide",                 r"(?i)physically wide"),
 ("feature_matrix",                  r"feature_matrix"),
 ("feature matrix",                  r"(?i)feature matrix"),
 ("DP-2a",                           r"DP-2a"),
 ("DP-2b",                           r"DP-2b"),
 ("f_<feature_code>",                r"f_<feature_code>"),
 ("Model Trainers (5 families)",     r"Model Trainers \(5 families\)"),
 ("five model families",             r"(?i)five model families"),
 ("G-01..G-20",                      r"G-01\.\.G-20"),
 ("twenty-two validation gates",     r"(?i)twenty-two validation gates"),
 ("supersedes (doc evolution)",      r"(?i)\bsupersedes\b"),
 ("Needs your ruling",               r"(?i)needs your ruling"),
 ("not until ruled",                 r"(?i)not until ruled"),
 ("OD-02 remains",                   r"OD-02 remains"),
 ("OD-13 open",                      r"OD-13.{0,80}(OPEN|needs a ruling|first blocker)"),
 ("Scope authority OPEN",            r"(?i)scope authority.{0,60}OPEN"),
 ("CT-07 needs",                     r"CT-07.{0,60}[Nn]eeds"),
 ("SM-15 as an object",              r"SM-15"),
 ("RelationshipModelVersion",        r"RelationshipModelVersion"),
 ("separate published version",      r"(?i)relationship.{0,40}separate published version"),
 ("single fact-row projection",      r"project into the same fact contract"),
 ("invented block taxonomy",         r"(INPUT / SCOPE|GOVERNANCE BLOCKS|OUTPUT BLOCKS|block_category|input_scope, intelligence)"),
 ("three-pool taxonomy",             r"(?i)pool.{0,40}(analysis, training, serving|serving\s*/\s*inference)"),
 ("serving pool",                    r"(?i)serving pool"),
 ("no point estimate rule",          r"No point estimate is ever emitted"),
 ("text encoder into MF-02/MF-03",   r"embedding consumed by MF-02"),
 ("chapters unavailable",            r"(?i)(chapters? (are|were) not available|only quotations|PENDING-SOURCE|PENDING SOURCE)"),
 ("NOT FROZEN status",               r"(?i)(not frozen|freeze checklist)"),
 ("supersession notices",            r"(?i)(PARTLY SUPERSEDED|a later section (governs|cancels))"),
 # --- target architecture (Revision 7) ---
 ("ml parallelism = 1",              r"(?i)`?ml`?\s*(pool\s*)?parallelism\s*(is|=)\s*1"),
 ("single-predicate admission",      r"sum\(.{0,40}\)\s*\+\s*.{0,30}<=\s*parallelism"),
 ("ml admits training and scoring",  r"(?i)\|\s*`?ml`?\s*\|[^|]*\|\s*training, scoring"),
 ("batch on online container",       r"(?i)batch_scoring may run on either"),
 ("absolute text boundary",          r"(?i)no statistic, score or value is ever computed from text"),
 ("manifest_hash as PK",             r"(?i)`manifest_hash`[^|]*\|[^|]*\|[^|]*PK"),
 ("seven ML models",                 r"(?i)seven ML models"),
 ("seven model families",            r"(?i)seven model families"),
 ("PostgreSQL as training path",     r"(?i)training (reads|queries).{0,30}feature_store"),
 ("sequence values as PG array",     r"`values`\s*\|\s*float32\[\]"),
 ("G-01..G-46 stale",                r"G-01\s*(to|through|\.\.)\s*G-46"),
 ("forty-seven gates",               r"(?i)forty-seven gates"),
]

POS = [
 ("seven families, correct term",    r"(?i)seven intelligence and engine famil"),
 ("MF-01 to MF-07 recognised",       r"MF-01\s*(to|through|\.\.)\s*MF-07"),
 ("OD-02 CLOSED",                    r"OD-02[^\n]{0,140}CLOSED"),
 ("OD-13 CLOSED",                    r"OD-13[^\n]{0,140}CLOSED"),
 ("CT-07 CLOSED",                    r"CT-07[^\n]{0,140}CLOSED"),
 ("seven output dataset families",   r"(?i)seven (governed )?(intelligence )?(output )?(dataset|analytical) famil"),
 ("canonical feature_store named",   r"ppiq_plant\.feature_store"),
 ("model_registry named",           r"ppiq_plant\.model_registry"),
 ("source_definition_version pin",   r"source_definition_version"),
 # --- target architecture (Revision 7) ---
 ("three ml lanes",                  r"ml\.online_scoring"),
 ("two-predicate admission",         r"resource_capacity"),
 ("max_concurrency separated",       r"max_concurrency"),
 ("online reservation hard",         r"(?i)hard-reserved"),
 ("Semantic Contract Manifest",      r"Semantic Contract Manifest"),
 ("manifest_id is PK",               r"(?i)`manifest_id`[^\n]{0,60}PK"),
 ("tenant-scoped manifest unique",   r"UNIQUE `\(tenant_id, manifest_hash\)`"),
 ("manifest coverage rule",          r"(?i)every new governed AI/ML execution"),
 ("columnar training artifact",      r"(?i)typed columnar artifact"),
 ("materialiser exemption",          r"(?i)snapshot materialiser is \*\*exempt|materialiser is exempt"),
 ("sequence split contract",         r"sequence_manifests"),
 ("three-dimensional promotion",     r"(?i)three-dimensional (promotion )?gate"),
 ("encoder promotion inequality",    r"promote_encoder"),
 ("exact Flat recall baseline",      r"(?i)exact Flat"),
 ("deterministic tool planner",      r"(?i)deterministic tool planner"),
 ("permission before ranking",       r"(?i)before ranking"),
 ("governance-based boundary",       r"(?i)free-form or model-generated"),
 ("intelligence and engine families",r"(?i)intelligence and engine famil"),
 ("gate inventory G-01..G-55",       r"G-01\s*(to|through|\.\.)\s*G-55"),
]

fails = 0
print("PPIQ LAYER B ARCHITECTURE PACK - CONTRADICTION SCAN V3")
print("file   :", PATH)
print("bytes  :", len(text.encode()))
print("lines  :", len(lines))
print("active : 1 -", ledger, "(section 48 onward is the historical ledger)")
print()
print("NEGATIVE CHECKS - stale contracts must be absent from the active body")
print(f"{'CHECK':<36}{'HITS':>6}  RESULT")
print("-" * 62)
for name, pat in NEG:
    hits = [(i + 1, l) for i, l in enumerate(active) if re.search(pat, l)]
    if hits: fails += 1
    print(f"{name:<36}{len(hits):>6}  {'PASS' if not hits else 'FAIL'}")
    for ln, l in hits[:3]:
        print(f"      line {ln}: {l.strip()[:96]}")

print()
print("POSITIVE CHECKS - the canonical final state must be asserted")
print(f"{'CHECK':<36}{'HITS':>6}  RESULT")
print("-" * 62)
for name, pat in POS:
    n = len(re.findall(pat, body))
    if n == 0: fails += 1
    print(f"{name:<36}{n:>6}  {'PASS' if n else 'FAIL'}")

print()
print("STRUCTURAL CHECKS")
print("-" * 62)
dup_line = [(i + 1, l) for i, l in enumerate(active)
            if len(re.findall(r'(?<![a-z_])model_version(?![a-z_])', l)) > 1
            and ('|' in l or '"' in l or ':' in l)]
print(f"{'duplicate model_version on a line':<36}{len(dup_line):>6}  {'PASS' if not dup_line else 'FAIL'}")
for ln, l in dup_line[:5]:
    print(f"      line {ln}: {l.strip()[:96]}")
if dup_line: fails += 1

nums = [re.match(r'^## (\d+)\.', l).group(1) for l in lines if re.match(r'^## \d+\.', l)]
dups = sorted({n for n in nums if nums.count(n) > 1})
print(f"{'duplicate section numbers':<36}{len(dups):>6}  {'PASS' if not dups else 'FAIL'}")
if dups: fails += 1

na = [i + 1 for i, l in enumerate(lines) if any(ord(c) > 126 for c in l)]
print(f"{'non-ascii characters':<36}{len(na):>6}  {'PASS' if not na else 'FAIL'}")
if na: fails += 1

print("-" * 62)
print("RESULT:", "SCAN V3 PASSED - FREEZE-SAFE" if fails == 0 else f"SCAN V3 FAILED - {fails} check(s)")
sys.exit(1 if fails else 0)
