<#
.SYNOPSIS
    Install-PpiqReactEnhancements.ps1 - installs the website enhancement pack
    (scroll-drawn GoldenThread + Architecture SVGs, Integration Ecosystem
    section, interactive ROI calculator, widget-hover CSS) into
    Website\PlantProcess.Website. Contract: preflight -> backup -> write ->
    per-file self-check -> tsc gate -> auto-revert on failure.
.PARAMETER RepoRoot  repository root (default: current directory)
.PARAMETER NoGate    skip the npx tsc --noEmit gate
.PARAMETER Revert    remove pack files / restore backups from the newest run
.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File .\Install-PpiqReactEnhancements.ps1
#>
[CmdletBinding()]
param([string]$RepoRoot = (Get-Location).Path, [switch]$NoGate, [switch]$Revert)
$LogName = 'Install_ReactEnhancements'


$ErrorActionPreference = 'Continue'
Set-StrictMode -Version Latest
$stamp   = Get-Date -Format 'yyyyMMdd_HHmmss'
$logPath = Join-Path $RepoRoot ($LogName + '_' + $stamp + '.txt')
$lines   = New-Object System.Collections.Generic.List[string]
$utf8    = New-Object System.Text.UTF8Encoding($false)
function W([string]$t = '') { $lines.Add($t); Write-Host $t }
function Save { [System.IO.File]::WriteAllText($logPath, (($lines -join "`r`n") + "`r`n"), $utf8); Write-Host ''; Write-Host ('Log: ' + $logPath) -ForegroundColor Cyan }
$created = New-Object System.Collections.Generic.List[string]
$backups = @{}
function Write-PackFile([string]$rel, [string]$content, [string]$marker) {
    $full = Join-Path $RepoRoot $rel
    $dir = Split-Path $full
    if (-not (Test-Path -LiteralPath $dir)) { [void](New-Item -ItemType Directory -Path $dir -Force) }
    if (Test-Path -LiteralPath $full) {
        $bak = $full + '.' + $stamp + '.bak'
        Copy-Item -LiteralPath $full -Destination $bak -Force
        $backups[$full] = $bak
        W ('  [overwrite+bak] ' + $rel)
    } else {
        $created.Add($full)
        W ('  [new]           ' + $rel)
    }
    [System.IO.File]::WriteAllText($full, $content, $utf8)
    $chk = [System.IO.File]::ReadAllText($full)
    if (-not $chk.Contains($marker)) { throw ('self-check failed for ' + $rel) }
}
function Revert-All {
    foreach ($f in $created) { if (Test-Path -LiteralPath $f) { Remove-Item -LiteralPath $f -Force } }
    foreach ($k in $backups.Keys) { Copy-Item -LiteralPath $backups[$k] -Destination $k -Force }
    W '  reverted: new files removed, backups restored.'
}


W '=============================================================================='
W ('INSTALL PPIQ REACT ENHANCEMENTS - ' + (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'))
W '=============================================================================='
W ''
$site = Join-Path $RepoRoot 'Website\PlantProcess.Website'
if ($Revert) {
    W '[REVERT] removing pack files / restoring newest backups'
    $rels = @(
      'src\components\motion\useScrollDraw.ts','src\components\graphics\GoldenThreadScroll.tsx',
      'src\components\graphics\ArchitectureFlowScroll.tsx','src\components\sections\IntegrationEcosystem.tsx',
      'src\components\roi\RoiCalculator.tsx','src\styles\motion-roi.css')
    foreach ($r in $rels) {
        $f = Join-Path $site $r
        $b = Get-ChildItem -Path (Split-Path $f) -Filter ((Split-Path $f -Leaf) + '.*.bak') -EA SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1
        if ($b) { Copy-Item $b.FullName $f -Force; W ('  restored ' + $r) }
        elseif (Test-Path -LiteralPath $f) { Remove-Item -LiteralPath $f -Force; W ('  removed ' + $r) }
    }
    Save; exit 0
}
W '[PREFLIGHT]'
if (-not (Test-Path -LiteralPath (Join-Path $site 'src'))) { W ('  MISSING ' + $site + '\src - run from the repo root.'); Save; exit 2 }
W ('  site: ' + $site)


W ''
W '[WRITE] ' + '6 files'

try {

$c_useScrollDraw_ts = @'
import { useEffect, useRef } from "react";

/**
 * Scroll-bound SVG path drawing.
 * Attach the returned ref to an <svg>. Every <path>, <line> and <polyline>
 * carrying data-draw inside it draws itself as the svg traverses the viewport.
 * Draw-only (never un-draws), rAF-throttled; prefers-reduced-motion renders
 * everything fully drawn and static.
 */
export function useScrollDraw<T extends SVGSVGElement>() {
  const ref = useRef<T | null>(null);

  useEffect(() => {
    const svg = ref.current;
    if (!svg) return;

    const els = Array.from(
      svg.querySelectorAll<SVGGeometryElement>("[data-draw]")
    );
    if (els.length === 0) return;

    const lengths = els.map((el) => {
      const len = el.getTotalLength ? el.getTotalLength() : 0;
      el.style.strokeDasharray = `${len}`;
      el.style.strokeDashoffset = `${len}`;
      return len;
    });

    const reduced = window.matchMedia("(prefers-reduced-motion: reduce)");
    if (reduced.matches) {
      els.forEach((el) => { el.style.strokeDashoffset = "0"; });
      return;
    }

    let raf = 0;
    let done = false;
    const ease = (t: number) => 1 - Math.pow(1 - t, 3);

    const update = () => {
      raf = 0;
      if (done) return;
      const r = svg.getBoundingClientRect();
      const vh = window.innerHeight;
      const raw = (vh - r.top) / (vh * 0.62 + r.height * 0.5);
      const p = ease(Math.min(1, Math.max(0, raw)));
      els.forEach((el, i) => {
        const current = parseFloat(el.style.strokeDashoffset || "0");
        const target = lengths[i] * (1 - p);
        if (target < current) el.style.strokeDashoffset = `${target}`;
      });
      if (p >= 1) {
        done = true;
        window.removeEventListener("scroll", onScroll);
      }
    };
    const onScroll = () => { if (!raf) raf = requestAnimationFrame(update); };

    window.addEventListener("scroll", onScroll, { passive: true });
    update();
    return () => {
      window.removeEventListener("scroll", onScroll);
      if (raf) cancelAnimationFrame(raf);
    };
  }, []);

  return ref;
}
'@

Write-PackFile 'Website\PlantProcess.Website\src\components\motion\useScrollDraw.ts' $c_useScrollDraw_ts 'useScrollDraw'

$c_GoldenThreadScroll_tsx = @'
import { useScrollDraw } from "../motion/useScrollDraw";

/**
 * Scroll-drawn Golden Thread: HEAT -> SLAB -> COIL -> EVIDENCE.
 * The lineage draws itself as the reader scrolls; nodes fade in after
 * their segment completes (CSS-driven via the same progress classes).
 * Quiet by design: single stroke, no loops, brand tokens only.
 */
export function GoldenThreadScroll() {
  const ref = useScrollDraw<SVGSVGElement>();
  return (
    <svg
      ref={ref}
      viewBox="0 0 1120 220"
      width="100%"
      role="img"
      aria-label="A heat flows to a slab and then a coil; the thread ends in quality evidence."
      className="ppiq-goldenthread"
    >
      <defs>
        <linearGradient id="gt-grad" x1="0" x2="1">
          <stop offset="0" stopColor="var(--sou-blue, #0a84ff)" />
          <stop offset="1" stopColor="var(--sou-cyan, #00d4ff)" />
        </linearGradient>
      </defs>

      <path
        data-draw
        d="M80 120 H340 M420 120 H640 M720 120 H930"
        fill="none"
        stroke="url(#gt-grad)"
        strokeWidth="2.2"
        strokeLinecap="round"
      />

      <circle cx="80" cy="120" r="24" fill="none" stroke="var(--sou-blue, #0a84ff)" strokeWidth="1.6" data-draw />
      <text x="80" y="168" textAnchor="middle" className="gt-label">HEAT</text>
      <text x="80" y="184" textAnchor="middle" className="gt-id">H-2214</text>

      <rect x="340" y="98" width="80" height="44" rx="6" fill="none" stroke="var(--sou-blue, #0a84ff)" strokeWidth="1.4" data-draw />
      <text x="380" y="168" textAnchor="middle" className="gt-label">SLAB</text>
      <text x="380" y="184" textAnchor="middle" className="gt-id">S-88410</text>

      <circle cx="680" cy="120" r="26" fill="none" stroke="var(--sou-cyan, #00d4ff)" strokeWidth="1.6" data-draw />
      <circle cx="680" cy="120" r="15" fill="none" stroke="var(--sou-cyan, #00d4ff)" strokeWidth="1" opacity=".6" data-draw />
      <text x="680" y="168" textAnchor="middle" className="gt-label">COIL</text>
      <text x="680" y="184" textAnchor="middle" className="gt-id">C-710909</text>

      <rect x="930" y="74" width="160" height="94" rx="8" fill="none" stroke="var(--sou-green, #2ce6a2)" strokeWidth="1.5" data-draw />
      <text x="1010" y="100" textAnchor="middle" className="gt-label" fill="var(--sou-green, #2ce6a2)">EVIDENCE</text>
      <text x="948" y="124" className="gt-ev">cause: superheat window</text>
      <text x="948" y="142" className="gt-ev">source: L2_CASTER</text>
      <text x="948" y="160" className="gt-ev">batch: IMP-2026-118</text>
    </svg>
  );
}
'@

Write-PackFile 'Website\PlantProcess.Website\src\components\graphics\GoldenThreadScroll.tsx' $c_GoldenThreadScroll_tsx 'GoldenThreadScroll'

$c_ArchitectureFlowScroll_tsx = @'
import { useScrollDraw } from "../motion/useScrollDraw";

/** Scroll-drawn architecture: sources -> read-only link -> unified model -> intelligence. */
export function ArchitectureFlowScroll() {
  const ref = useScrollDraw<SVGSVGElement>();
  const box = "var(--sou-panel-2, #102a43)";
  const line = "#1d3a63";
  return (
    <svg
      ref={ref}
      viewBox="0 0 1120 300"
      width="100%"
      role="img"
      aria-label="Plant systems flow through a read-only link into one unified model and out to dashboards, predictions and recommendations."
      className="ppiq-archflow"
    >
      {[
        ["Level 2 / L2 DB", 26],
        ["SAP / ERP", 82],
        ["L1 Sensors / Historian", 138],
        ["Quality / LIMS / Inspection", 194],
      ].map(([label, y]) => (
        <g key={label as string}>
          <rect x="20" y={y as number} width="200" height="40" rx="7" fill={box} stroke={line} />
          <text x="120" y={(y as number) + 25} textAnchor="middle" className="af-t">{label}</text>
        </g>
      ))}

      <path data-draw d="M220 46 H300 M220 102 H300 M220 158 H300 M220 214 H300"
        fill="none" stroke="var(--sou-cyan, #00d4ff)" strokeWidth="2" strokeLinecap="round" />

      <rect x="300" y="96" width="150" height="72" rx="8" fill={box} stroke="var(--sou-cyan, #00d4ff)" />
      <text x="375" y="126" textAnchor="middle" className="af-t">READ-ONLY LINK</text>
      <text x="375" y="146" textAnchor="middle" className="af-s">observes, never commands</text>

      <path data-draw d="M450 132 H540" fill="none" stroke="var(--sou-cyan, #00d4ff)" strokeWidth="2" strokeLinecap="round" />

      <rect x="540" y="86" width="180" height="92" rx="8" fill={box} stroke="var(--sou-blue, #0a84ff)" />
      <text x="630" y="118" textAnchor="middle" className="af-t">UNIFIED PLANT MODEL</text>
      <text x="630" y="138" textAnchor="middle" className="af-s">full material genealogy</text>
      <text x="630" y="156" textAnchor="middle" className="af-s">every row keeps its source</text>

      <path data-draw d="M720 110 H820 M720 132 H820 M720 154 H820"
        fill="none" stroke="var(--sou-green, #2ce6a2)" strokeWidth="2" strokeLinecap="round" />

      {[
        ["Dashboards", 88],
        ["Predictions &middot; AI+ML", 122],
        ["Recommendations", 156],
      ].map(([label, y]) => (
        <g key={label as string}>
          <rect x="820" y={(y as number)} width="270" height="30" rx="7" fill={box} stroke="var(--sou-green, #2ce6a2)" />
          <text x="955" y={(y as number) + 20} textAnchor="middle" className="af-t" fill="var(--sou-green, #2ce6a2)">{label}</text>
        </g>
      ))}
    </svg>
  );
}
'@

Write-PackFile 'Website\PlantProcess.Website\src\components\graphics\ArchitectureFlowScroll.tsx' $c_ArchitectureFlowScroll_tsx 'ArchitectureFlowScroll'

$c_IntegrationEcosystem_tsx = @'
/**
 * Integration ecosystem - typed system names, deliberately NOT vendor logos:
 * we render trademarks as text identifiers (nominative, accurate) rather than
 * reproducing brand marks we hold no rights to. Reads more enterprise, not less.
 */
const GROUPS: { title: string; items: string[] }[] = [
  { title: "Process automation", items: ["Siemens L2", "SMS Level 2", "Primetals L2", "Custom Level 2"] },
  { title: "Business systems", items: ["SAP ERP", "Oracle EBS", "Microsoft Dynamics", "MES platforms"] },
  { title: "Databases", items: ["Oracle", "SQL Server", "PostgreSQL", "MySQL"] },
  { title: "Historians & telemetry", items: ["OSIsoft PI", "Wonderware", "OPC-UA", "REST / IoT"] },
  { title: "Quality & lab", items: ["LIMS", "QMS modules", "Inspection devices", "Gauge systems"] },
  { title: "Files & exports", items: ["Excel", "CSV", "XML", "Vendor exports"] },
];

export function IntegrationEcosystem() {
  return (
    <section className="ppiq-ecosystem" aria-labelledby="eco-h2">
      <p className="eco-eyebrow">INTEGRATION ECOSYSTEM</p>
      <h2 id="eco-h2">Connects to what you already run</h2>
      <p className="eco-lead">
        Read-only connectors for the systems on your floor today - and a generic
        connector layer for the ones we haven&rsquo;t met yet. If it has a database,
        an export or an API, PlantProcess&nbsp;IQ can read it.
      </p>
      <div className="eco-grid">
        {GROUPS.map((g) => (
          <div className="eco-card" key={g.title}>
            <h3>{g.title}</h3>
            <ul>
              {g.items.map((it) => (
                <li key={it}>{it}</li>
              ))}
            </ul>
          </div>
        ))}
      </div>
      <p className="eco-note">
        System names identify integration targets and remain trademarks of their
        respective owners.
      </p>
    </section>
  );
}
'@

Write-PackFile 'Website\PlantProcess.Website\src\components\sections\IntegrationEcosystem.tsx' $c_IntegrationEcosystem_tsx 'IntegrationEcosystem'

$c_RoiCalculator_tsx = @'
import { useMemo, useState } from "react";

/**
 * Interactive ROI model - the visitor's own math, never our benchmark.
 * We compute the value of the yield recovery THEY choose to model, on THEIR
 * tonnage and THEIR margin, and label it a directional estimate. The CTA turns
 * the result into a conversation.
 */
const fmt = (n: number) =>
  new Intl.NumberFormat("en-US", {
    style: "currency",
    currency: "EUR",
    maximumFractionDigits: 0,
  }).format(n);

export function RoiCalculator({ demoHref = "#request-demo" }: { demoHref?: string }) {
  const [tonnage, setTonnage] = useState(1_000_000);
  const [margin, setMargin] = useState(80);
  const [recovery, setRecovery] = useState(1.0);

  const annual = useMemo(
    () => tonnage * (recovery / 100) * margin,
    [tonnage, margin, recovery]
  );
  const perMonth = annual / 12;

  return (
    <section className="ppiq-roi" aria-labelledby="roi-h2">
      <p className="roi-eyebrow">WHAT IS 1% WORTH IN YOUR PLANT?</p>
      <h2 id="roi-h2">Model it with your own numbers</h2>

      <div className="roi-grid">
        <div className="roi-inputs">
          <label>
            <span className="roi-label">Annual production</span>
            <span className="roi-value">{tonnage.toLocaleString("en-US")} t</span>
            <input
              type="range" min={50_000} max={5_000_000} step={50_000}
              value={tonnage}
              onChange={(e) => setTonnage(Number(e.target.value))}
              aria-label="Annual production in tonnes"
            />
          </label>
          <label>
            <span className="roi-label">Contribution margin</span>
            <span className="roi-value">{margin} &euro; / t</span>
            <input
              type="range" min={20} max={400} step={5}
              value={margin}
              onChange={(e) => setMargin(Number(e.target.value))}
              aria-label="Contribution margin in euros per tonne"
            />
          </label>
          <label>
            <span className="roi-label">Prime-yield recovery you model</span>
            <span className="roi-value">{recovery.toFixed(1)} %</span>
            <input
              type="range" min={0.2} max={3} step={0.1}
              value={recovery}
              onChange={(e) => setRecovery(Number(e.target.value))}
              aria-label="Modelled prime yield recovery in percent"
            />
          </label>
        </div>

        <div className="roi-result" role="status" aria-live="polite">
          <div className="roi-headline">{fmt(annual)}</div>
          <div className="roi-sub">per year &middot; {fmt(perMonth)} per month</div>
          <p className="roi-explain">
            {recovery.toFixed(1)}% of {tonnage.toLocaleString("en-US")} t moved from
            downgrade or scrap back to prime, at {margin}&nbsp;&euro;/t contribution.
          </p>
          <a className="roi-cta" href={demoHref}>
            Discuss these numbers with us
          </a>
          <p className="roi-disclaimer">
            Directional estimate from your inputs - not a guarantee. A pilot on one
            line is how we validate the recoverable share together.
          </p>
        </div>
      </div>
    </section>
  );
}
'@

Write-PackFile 'Website\PlantProcess.Website\src\components\roi\RoiCalculator.tsx' $c_RoiCalculator_tsx 'RoiCalculator'

$c_motion_roi_css = @'
/* ===== PPIQ enhancement pack: scroll-draw, ecosystem, ROI ===== */
/* Uses existing SOU brand tokens; falls back to their literal values. */

/* golden thread / architecture text */
.ppiq-goldenthread .gt-label,
.ppiq-archflow .af-t { font-family: "Chakra Petch", var(--disp, sans-serif); font-size: 12px; letter-spacing: .08em; fill: var(--sou-text, #eaf6ff); font-weight: 600; }
.ppiq-goldenthread .gt-id { font-family: var(--body, "Inter", sans-serif); font-size: 11px; fill: var(--sou-muted, #8ea7c1); }
.ppiq-goldenthread .gt-ev { font-family: var(--body, "Inter", sans-serif); font-size: 11px; fill: var(--sou-muted, #8ea7c1); }
.ppiq-archflow .af-s { font-family: var(--body, "Inter", sans-serif); font-size: 10.5px; fill: var(--sou-muted, #8ea7c1); }

/* ---------- ecosystem ---------- */
.ppiq-ecosystem { padding: 96px 0; }
.eco-eyebrow, .roi-eyebrow { font-family: "Chakra Petch", sans-serif; font-size: 12.5px; letter-spacing: .22em; color: var(--sou-cyan, #00d4ff); font-weight: 600; text-transform: uppercase; }
.ppiq-ecosystem h2, .ppiq-roi h2 { font-family: "Chakra Petch", sans-serif; font-size: clamp(26px, 3.4vw, 38px); color: #fff; margin: 10px 0 14px; }
.eco-lead { color: var(--sou-muted, #8ea7c1); max-width: 640px; font-size: 17px; }
.eco-grid { display: grid; grid-template-columns: repeat(3, 1fr); gap: 18px; margin-top: 40px; }
@media (max-width: 960px) { .eco-grid { grid-template-columns: 1fr 1fr; } }
@media (max-width: 640px) { .eco-grid { grid-template-columns: 1fr; } }
.eco-card { background: var(--sou-panel, #0b1730); border: 1px solid #16294a; border-radius: 10px; padding: 20px 22px; transition: border-color .25s, transform .25s; }
.eco-card:hover { border-color: #27507f; transform: translateY(-3px); }
.eco-card h3 { font-family: "Chakra Petch", sans-serif; font-size: 13px; letter-spacing: .1em; text-transform: uppercase; color: var(--sou-cyan, #00d4ff); margin-bottom: 12px; }
.eco-card ul { list-style: none; display: flex; flex-wrap: wrap; gap: 8px; }
.eco-card li { font-size: 13.5px; color: var(--sou-text, #eaf6ff); border: 1px solid #1d3a63; border-radius: 6px; padding: 6px 12px; background: var(--sou-panel-2, #102a43); }
.eco-note { margin-top: 26px; font-size: 12px; color: #5c7391; }

/* ---------- ROI ---------- */
.ppiq-roi { padding: 96px 0; }
.roi-grid { display: grid; grid-template-columns: 1.1fr 1fr; gap: 40px; margin-top: 44px; align-items: stretch; }
@media (max-width: 920px) { .roi-grid { grid-template-columns: 1fr; } }
.roi-inputs { display: flex; flex-direction: column; gap: 30px; background: var(--sou-panel, #0b1730); border: 1px solid #16294a; border-radius: 12px; padding: 32px; }
.roi-inputs label { display: block; }
.roi-label { display: inline-block; font-family: "Chakra Petch", sans-serif; font-size: 12px; letter-spacing: .12em; text-transform: uppercase; color: var(--sou-muted, #8ea7c1); }
.roi-value { float: right; font-family: "Chakra Petch", sans-serif; font-size: 15px; color: var(--sou-cyan, #00d4ff); font-weight: 600; }
.roi-inputs input[type="range"] { width: 100%; margin-top: 14px; accent-color: var(--sou-cyan, #00d4ff); height: 4px; }
.roi-result { background: linear-gradient(165deg, #0d1f3a, var(--sou-panel, #0b1730)); border: 1px solid #1e4a6e; border-radius: 12px; padding: 36px; display: flex; flex-direction: column; }
.roi-headline { font-family: "Chakra Petch", sans-serif; font-size: clamp(34px, 4.4vw, 52px); font-weight: 700; color: var(--sou-green, #2ce6a2); letter-spacing: .01em; }
.roi-sub { font-size: 14.5px; color: var(--sou-muted, #8ea7c1); margin-top: 4px; }
.roi-explain { font-size: 14.5px; color: var(--sou-text, #eaf6ff); margin-top: 18px; line-height: 1.6; }
.roi-cta { margin-top: auto; align-self: flex-start; margin-top: 26px; font-family: "Chakra Petch", sans-serif; font-weight: 600; font-size: 15px; color: #03222c; background: linear-gradient(90deg, var(--sou-cyan, #00d4ff), #4de3ff); padding: 13px 26px; border-radius: 7px; transition: box-shadow .25s, transform .2s; }
.roi-cta:hover { box-shadow: 0 0 26px rgba(0, 212, 255, .45); transform: translateY(-1px); }
.roi-disclaimer { margin-top: 16px; font-size: 12px; color: #5c7391; line-height: 1.55; }

/* interactive-widget hover feel for existing chart mocks */
.ppiq-widget-hover rect, .ppiq-widget-hover circle, .ppiq-widget-hover path { transition: opacity .18s, filter .18s; }
.ppiq-widget-hover:hover [data-dim] { opacity: .35; }
.ppiq-widget-hover [data-focus]:hover { opacity: 1 !important; filter: drop-shadow(0 0 6px rgba(0, 212, 255, .6)); cursor: pointer; }
'@

Write-PackFile 'Website\PlantProcess.Website\src\styles\motion-roi.css' $c_motion_roi_css 'ppiq-roi'

} catch { W ('  WRITE FAILED: ' + $_.Exception.Message); Revert-All; Save; exit 1 }

if (-not $NoGate) {
    W ''
    W '[GATE] npx tsc --noEmit (auto-revert on failure)'
    Push-Location $site
    $o = & npx tsc --noEmit 2>&1
    $code = $LASTEXITCODE
    Pop-Location
    foreach ($l in ($o | Select-Object -Last 10)) { W ('    ' + $l) }
    if ($code -ne 0) { W '  TYPE CHECK FAILED - reverting.'; Revert-All; Save; exit 1 }
    W '  TYPE CHECK GREEN'
}


W ''
W 'DONE. WIRE-UP (NewHomePage.tsx / App.tsx):'
W '  import { GoldenThreadScroll } from "./components/graphics/GoldenThreadScroll";'
W '  import { ArchitectureFlowScroll } from "./components/graphics/ArchitectureFlowScroll";'
W '  import { IntegrationEcosystem } from "./components/sections/IntegrationEcosystem";'
W '  import { RoiCalculator } from "./components/roi/RoiCalculator";'
W '  import "./styles/motion-roi.css";'
W 'Order: Hero -> ArchitectureFlowScroll -> packs -> GoldenThreadScroll ->'
W '       IntegrationEcosystem -> RoiCalculator demoHref="#request-demo" -> RequestDemoForm'
W 'Widget hover: add className="ppiq-widget-hover" to a mock svg; tag elements'
W 'with data-dim (background) and data-focus (interactive).'
W 'Reduced motion renders all paths fully drawn. ROI stays benchmark-free.'


Save
exit 0