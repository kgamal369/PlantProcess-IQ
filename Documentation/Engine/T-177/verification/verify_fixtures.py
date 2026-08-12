"""Independent second-source verification.
Recomputes every expected value from first principles WITHOUT scipy,
so a scipy bug cannot silently define the contract."""
import json, math
F=json.load(open("t177_known_answer_fixtures.json"))
fx={f["id"]:f for f in F["fixtures"]}
ok=True
def chk(tag,a,b,tol=1e-7):
    global ok
    good = abs(a-b) <= tol*max(1.0,abs(b))
    ok = ok and good
    print(f"  {'OK  ' if good else 'FAIL'} {tag:36} hand={a:.9f} fixture={b:.9f}")

def mean(v): return sum(v)/len(v)
def var(v):
    m=mean(v); return sum((x-m)**2 for x in v)/(len(v)-1)

print("F-01 one-way ANOVA, computed by hand")
g=fx["F-01"]["groups"]; gs=list(g.values())
allv=[x for s in gs for x in s]; N=len(allv); k=len(gs); gm=mean(allv)
ssb=sum(len(s)*(mean(s)-gm)**2 for s in gs)
ssw=sum((x-mean(s))**2 for s in gs for x in s)
sst=ssb+ssw
dfb,dfw=k-1,N-k
Fst=(ssb/dfb)/(ssw/dfw)
chk("F statistic",Fst,fx["F-01"]["expect"]["f_statistic"])
chk("eta squared = SSB/SST",ssb/sst,fx["F-01"]["expect"]["eta_squared"])
print(f"       SSB={ssb:.6f} SSW={ssw:.6f} SST={sst:.6f} df=({dfb},{dfw})")

print("\nF-02 Kruskal-Wallis H, computed by hand from average ranks")
g=fx["F-02"]["groups"]; gs=list(g.values())
flat=[(v,i) for i,s in enumerate(gs) for v in s]
srt=sorted(range(len(flat)), key=lambda i: flat[i][0])
ranks=[0.0]*len(flat); i=0
while i<len(srt):
    j=i
    while j+1<len(srt) and flat[srt[j+1]][0]==flat[srt[i]][0]: j+=1
    avg=(i+j)/2.0+1.0
    for t in range(i,j+1): ranks[srt[t]]=avg
    i=j+1
N=len(flat)
H=12.0/(N*(N+1))*sum(
    (sum(ranks[i] for i in range(N) if flat[i][1]==gi)**2)/len(gs[gi])
    for gi in range(len(gs))) - 3*(N+1)
import collections
allv=[v for _,s_ in [(0,x) for x in gs] for v in s_]
cnt=collections.Counter(allv)
tie=1 - sum(t**3-t for t in cnt.values() if t>1)/(N**3-N)
Hc=H/tie
print(f"       tie correction factor = {tie:.9f} (H_raw={H:.9f})")
chk("H statistic (tie-corrected)",Hc,fx["F-02"]["expect"]["h_statistic"])
H=Hc
kk=len(gs)
chk("epsilon squared",(H-kk+1)/(N-kk),fx["F-02"]["expect"]["epsilon_squared"])

print("\nF-08 Benjamini-Hochberg, computed by hand (step-up)")
p=fx["F-08"]["inputs"]["p_values"]; m=len(p)
order=sorted(range(m),key=lambda i:p[i]); q=[0.0]*m; run=1.0
for kx in range(m-1,-1,-1):
    idx=order[kx]; adj=p[idx]*m/(kx+1); run=min(run,adj); q[idx]=min(1.0,run)
for i in range(m): chk(f"q[{i}]",q[i],fx["F-08"]["expect"]["q_values"][i])

print("\nF-10 Spearman rho, computed by hand")
xs=fx["F-10"]["inputs"]["x"]; ys=fx["F-10"]["inputs"]["y"]
def rk(v):
    s=sorted(range(len(v)),key=lambda i:v[i]); r=[0.0]*len(v); i=0
    while i<len(v):
        j=i
        while j+1<len(v) and v[s[j+1]]==v[s[i]]: j+=1
        a=(i+j)/2.0+1.0
        for t in range(i,j+1): r[s[t]]=a
        i=j+1
    return r
rx,ry=rk(xs),rk(ys); mx,my=mean(rx),mean(ry)
num=sum((rx[i]-mx)*(ry[i]-my) for i in range(len(rx)))
den=math.sqrt(sum((v-mx)**2 for v in rx)*sum((v-my)**2 for v in ry))
chk("spearman rho",num/den,fx["F-10"]["expect"]["spearman_rho"])

print("\nTaxonomy separation check")
codes={}
for i in ["F-04","F-05","F-06","F-07"]:
    c=fx[i]["expect"]["exclusion_reason_code"]
    codes.setdefault(c,[]).append(i)
dupe=[(c,v) for c,v in codes.items() if len(v)>1]
print(f"  {'OK  ' if not dupe else 'FAIL'} four exclusion cases -> {len(codes)} distinct reason codes")
for c,v in codes.items(): print(f"       {c:32} <- {v}")
if dupe: ok=False
att={fx[i]["expect"].get("attribution") for i in ["F-04","F-05","F-06"]}
print(f"  {'OK  ' if 'method' in att and 'data' in att else 'FAIL'} attribution distinguishes method-side from data-side")

print("\n"+("="*58))
print("RESULT:", "ALL HAND-VERIFIED" if ok else "MISMATCH")
