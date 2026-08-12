"""Line-for-line port of the C# special functions I am about to write.
If these do not hit the fixture p-values here, the C# will not either."""
import math, json
from scipy import stats

def LogGamma(x):
    c=[76.18009172947146,-86.50532032941677,24.01409824083091,
       -1.231739572450155,0.1208650973866179e-2,-0.5395239384953e-5]
    y=x; tmp=x+5.5; tmp-=(x+0.5)*math.log(tmp); ser=1.000000000190015
    for j in range(6):
        y+=1.0; ser+=c[j]/y
    return -tmp+math.log(2.5066282746310005*ser/x)

def BetaContinuedFraction(a,b,x):
    FPMIN=1e-300; EPS=3e-16; MAXIT=300
    qab=a+b; qap=a+1.0; qam=a-1.0
    c=1.0; d=1.0-qab*x/qap
    if abs(d)<FPMIN: d=FPMIN
    d=1.0/d; h=d
    for m in range(1,MAXIT+1):
        m2=2*m
        aa=m*(b-m)*x/((qam+m2)*(a+m2))
        d=1.0+aa*d
        if abs(d)<FPMIN: d=FPMIN
        c=1.0+aa/c
        if abs(c)<FPMIN: c=FPMIN
        d=1.0/d; h*=d*c
        aa=-(a+m)*(qab+m)*x/((a+m2)*(qap+m2))
        d=1.0+aa*d
        if abs(d)<FPMIN: d=FPMIN
        c=1.0+aa/c
        if abs(c)<FPMIN: c=FPMIN
        d=1.0/d; de=d*c; h*=de
        if abs(de-1.0)<EPS: break
    return h

def RegularizedIncompleteBeta(a,b,x):
    if x<=0.0: return 0.0
    if x>=1.0: return 1.0
    front=math.exp(LogGamma(a+b)-LogGamma(a)-LogGamma(b)+a*math.log(x)+b*math.log(1.0-x))
    if x < (a+1.0)/(a+b+2.0):
        return front*BetaContinuedFraction(a,b,x)/a
    return 1.0-math.exp(LogGamma(a+b)-LogGamma(a)-LogGamma(b)+b*math.log(1.0-x)+a*math.log(x))*BetaContinuedFraction(b,a,1.0-x)/b

def FDistributionSf(f,d1,d2):
    """Upper tail P(F > f). This IS the ANOVA p-value."""
    if f<=0.0: return 1.0
    x=d2/(d2+d1*f)
    return RegularizedIncompleteBeta(d2/2.0,d1/2.0,x)

def RegularizedGammaQ(a,x):
    """Upper regularized incomplete gamma Q(a,x)."""
    if x<0.0 or a<=0.0: return float('nan')
    if x==0.0: return 1.0
    if x < a+1.0:
        ap=a; s=1.0/a; delta=s
        for _ in range(1000):
            ap+=1.0; delta*=x/ap; s+=delta
            if abs(delta)<abs(s)*3e-16: break
        return 1.0-s*math.exp(-x+a*math.log(x)-LogGamma(a))
    FPMIN=1e-300
    b=x+1.0-a; c=1.0/FPMIN; d=1.0/b; h=d
    for i in range(1,1001):
        an=-i*(i-a); b+=2.0
        d=an*d+b
        if abs(d)<FPMIN: d=FPMIN
        c=b+an/c
        if abs(c)<FPMIN: c=FPMIN
        d=1.0/d; de=d*c; h*=de
        if abs(de-1.0)<3e-16: break
    return math.exp(-x+a*math.log(x)-LogGamma(a))*h

def ChiSquareSf(chi,df):
    """Upper tail P(X2 > chi). This IS the Kruskal-Wallis p-value."""
    if chi<=0.0: return 1.0
    return RegularizedGammaQ(df/2.0, chi/2.0)

F=json.load(open("t177_known_answer_fixtures.json"))
fx={f["id"]:f for f in F["fixtures"]}
ok=True
def chk(tag,mine,ref,tol=1e-9):
    global ok
    rel=abs(mine-ref)/max(1e-300,abs(ref))
    good = abs(mine-ref)<=tol or rel<=tol
    ok=ok and good
    print(f"  {'OK  ' if good else 'FAIL'} {tag:34} mine={mine:.12e} ref={ref:.12e} rel={rel:.2e}")

print("ANOVA p-value via my incomplete-beta F survival function")
e=fx["F-01"]["expect"]
chk("F-01 p_value", FDistributionSf(e["f_statistic"],2,21), e["p_value"])

print("\nKruskal-Wallis p-value via my incomplete-gamma chi2 survival function")
for fid in ("F-02","F-03"):
    e=fx[fid]["expect"]
    chk(f"{fid} p_value", ChiSquareSf(e["h_statistic"],e["df"]), e["p_value"])

print("\nStress: wide range against scipy")
for (f,d1,d2) in [(1.0,1,1),(2.5,3,10),(533.7777777,2,21),(0.001,5,5),(100.0,10,100)]:
    chk(f"F sf({f},{d1},{d2})", FDistributionSf(f,d1,d2), float(stats.f.sf(f,d1,d2)))
for (c,df) in [(0.5,1),(3.84,1),(15.393464052,2),(50.0,10),(0.001,3)]:
    chk(f"chi2 sf({c},{df})", ChiSquareSf(c,df), float(stats.chi2.sf(c,df)))

print("\n"+"="*60)
print("ALGORITHM PROOF:", "PASS - safe to write the C#" if ok else "FAIL - do not write C# yet")
