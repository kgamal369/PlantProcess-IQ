# -*- coding: utf-8 -*-
"""Proof for the anonymous-GET catch-all, per Karim's required steps 1-4.

1. enumerate GET routes reachable in the running API
2. enumerate explicit authorization declarations
3. identify routes falling through the anonymous catch-all
4. record expected auth behaviour
"""
import os
import re

ROOT = "/home/claude/repo"
API = os.path.join(ROOT, "Backend/PlantProcess.Api")

# ---------------------------------------------------------------- 2. the matrix
mx_path = os.path.join(API, "Security/PlantAccessControl.cs")
mx_src = open(mx_path, encoding="utf-8", errors="replace").read()
# strip comments so a prefix mentioned in prose is never counted as a declaration
mx_code = re.sub(r"(?m)//.*$", "", mx_src)

ENTRY = re.compile(
    r'\(\s*"(?P<prefix>/[^"]*)"\s*,\s*(?P<methods>All\(\)|new\[\]\s*\{[^}]*\})\s*,'
    r'\s*"(?P<perm>[^"]*)"\s*,\s*(?P<anon>true|false)\s*\)'
)
matrix = []
for m in ENTRY.finditer(mx_code):
    meths = m.group("methods")
    if meths.strip() == "All()":
        methods = {"GET", "POST", "PUT", "PATCH", "DELETE"}
    else:
        methods = set(re.findall(r'"([A-Z]+)"', meths))
    matrix.append({
        "prefix": m.group("prefix"),
        "methods": methods,
        "perm": m.group("perm"),
        "anon": m.group("anon") == "true",
    })

print("=" * 78)
print("STEP 2 - EXPLICIT AUTHORIZATION DECLARATIONS")
print("=" * 78)
print("matrix entries parsed from comment-stripped source :", len(matrix))
anon_entries = [e for e in matrix if e["anon"]]
print("entries granting anonymous access                  :", len(anon_entries))
for e in anon_entries:
    star = "  <-- CATCH-ALL" if e["prefix"] == "/" else ""
    print("   %-28s %-34s%s" % (e["prefix"], ",".join(sorted(e["methods"])), star))

catchall = [e for e in matrix if e["prefix"] == "/" and e["anon"] and "GET" in e["methods"]]
print()
print("CATCH-ALL PRESENT IN CURRENT TREE:", bool(catchall))
for i, line in enumerate(mx_src.split("\n"), 1):
    if re.search(r'\(\s*"/"\s*,', line):
        print("   %s:%d  %s" % ("Backend/PlantProcess.Api/Security/PlantAccessControl.cs",
                                i, line.strip()))

# ---------------------------------------------------------------- 1. GET routes
GROUP = re.compile(r'MapGroup\(\s*"(?P<p>/[^"]*)"')
MAPGET = re.compile(r'\.?Map(?P<verb>Get|Post|Put|Patch|Delete)\(\s*"(?P<p>[^"]*)"')

# Which Map*Endpoints extension methods are actually wired in Program.cs?
prog = open(os.path.join(API, "Program.cs"), encoding="utf-8", errors="replace").read()
prog_code = re.sub(r"(?m)//.*$", "", prog)
wired = set(re.findall(r"app\.(Map\w+)\(", prog_code))

routes = []
unwired_files = []
for base, _, files in os.walk(API):
    for fn in files:
        if not fn.endswith(".cs"):
            continue
        p = os.path.join(base, fn)
        src = open(p, encoding="utf-8", errors="replace").read()
        code = re.sub(r"(?s)/\*.*?\*/", "", src)
        code = re.sub(r"(?m)//.*$", "", code)

        # extension methods declared in this file
        declared = set(re.findall(r"IEndpointRouteBuilder\s+Map(\w+)\s*\(", code))
        declared |= set(re.findall(r"static\s+\w+\s+(Map\w+)\s*\(\s*this\s+IEndpointRouteBuilder", code))
        declared = {d if d.startswith("Map") else "Map" + d for d in declared}
        is_wired = (not declared) or bool(declared & wired)
        if declared and not is_wired:
            unwired_files.append((os.path.relpath(p, ROOT), sorted(declared)))

        groups = [g.group("p") for g in GROUP.finditer(code)]
        prefix = groups[0] if groups else ""
        for m in MAPGET.finditer(code):
            verb = m.group("verb").upper()
            sub = m.group("p")
            full = (prefix + sub) if sub.startswith("/") else (prefix + "/" + sub)
            full = re.sub(r"/+", "/", full) or "/"
            routes.append({"verb": verb, "path": full,
                           "file": os.path.relpath(p, ROOT), "wired": is_wired})

gets = [r for r in routes if r["verb"] == "GET" and r["wired"]]
print()
print("=" * 78)
print("STEP 1 - GET ROUTES")
print("=" * 78)
print("Map* calls found across the API project :", len(routes))
print("GET routes in wired endpoint groups     :", len(gets))
print("files declaring endpoints never wired in Program.cs :", len(unwired_files))
for f, d in unwired_files[:8]:
    print("   %s  %s" % (f, d))

# ---------------------------------------------------------------- 3. fall-through
def resolve(path, verb):
    """Longest prefix wins, mirroring AccessControlMiddleware."""
    best = None
    for e in matrix:
        if e["prefix"] == "/":
            continue
        if path == e["prefix"] or path.startswith(e["prefix"].rstrip("/") + "/"):
            if verb in e["methods"]:
                if best is None or len(e["prefix"]) > len(best["prefix"]):
                    best = e
    return best

fell = []
for r in gets:
    if resolve(r["path"], "GET") is None:
        fell.append(r)

print()
print("=" * 78)
print("STEP 3 - GET ROUTES FALLING THROUGH TO THE ANONYMOUS CATCH-ALL")
print("=" * 78)
print("count:", len(fell), "of", len(gets), "wired GET routes")
print()
seen = set()
for r in sorted(fell, key=lambda x: x["path"]):
    key = (r["path"], r["file"])
    if key in seen:
        continue
    seen.add(key)
    print("   %-58s %s" % (r["path"], r["file"].replace("Backend/PlantProcess.Api/", "")))
