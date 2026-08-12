"""Port of GroupComparisonKernel.Evaluate decision logic. Proves each fixture routes
to the branch its expectation demands, BEFORE the C# is compiled anywhere."""
import math, json
from statistics import median
def var(v):
    m=sum(v)/len(v); return sum((x-m)**2 for x in v)/(len(v)-1) if len(v)>1 else 0.0
def skew(v):
    n=len(v)
    if n<3: return 0.0
    m=sum(v)/n; sd=math.sqrt(var(v))
    if sd<=0: return 0.0
    return sum(((x-m)/sd)**3 for x in v)*n/((n-1)*(n-2))
def f_sf(f,d1,d2):
    import algo_proof as A; return A.FDistributionSf(f,d1,d2)
def levene(gs):
    z=[[abs(x-median(g)) for x in g] for g in gs]
    n=sum(len(s) for s in z); k=len(z)
    allz=[x for s in z for x in s]; grand=sum(allz)/n
    num=sum(len(s)*((sum(s)/len(s))-grand)**2 for s in z)/(k-1)
    den=sum(sum((x-(sum(s)/len(s)))**2 for x in s) for s in z)/(n-k)
    if den<=0: return math.inf,0.0
    w=num/den
    return w, f_sf(w,k-1,n-k)

VAR_CEIL=4.0; LEV_ALPHA=0.05; SKEW_CEIL=2.0
def decide(groups):
    gs=list(groups.values())
    if len(gs)<2: return "INSUFFICIENT_GROUPS"
    if min(len(g) for g in gs)<2: return "INSUFFICIENT_SAMPLE"
    allv=[x for g in gs for x in g]
    if var(allv)<=0: return "CONSTANT_ZERO_VARIANCE"
    vs=[var(g) for g in gs]
    ratio=math.inf if min(vs)<=0 else max(vs)/min(vs)
    _,lp=levene(gs)
    homo = lp>=LEV_ALPHA and ratio<=VAR_CEIL
    sym  = all(abs(skew(g))<=SKEW_CEIL for g in gs)
    return "ANOVA" if (homo and sym) else "KRUSKAL_WALLIS"

F=json.load(open("t177_known_answer_fixtures.json"))
ok=True
print(f"{'ID':6} {'ROUTED':22} {'REQUIRED':22} {'':4}")
print("-"*58)
for f in F["fixtures"]:
    if not f.get("groups"): continue
    got=decide(f["groups"]); e=f["expect"]
    if "exclusion_reason_code" in e:
        want = e["exclusion_reason_code"]
    elif "method" in e:
        want = "ANOVA" if e["method"]=="ANOVA" else "KRUSKAL_WALLIS"
    else:
        print(f"{f['id']:6} {got:22} {'(no method asserted)':22} SKIP")
        continue
    good = got==want
    ok = ok and good
    print(f"{f['id']:6} {got:22} {want:22} {'OK' if good else 'FAIL'}")
    if "must_not_be_method" in e:
        nm = e["must_not_be_method"].upper()
        assert got!=nm, f"{f['id']} routed to forbidden {nm}"
        print(f"       falsification held: did NOT route to {nm}")
print("-"*58)
print("KERNEL ROUTING PROOF:", "PASS" if ok else "FAIL")
