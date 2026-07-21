import React, { useEffect } from "react";
import "../styles/new-landing.css";
import RequestDemoForm from "../components/proof/RequestDemoForm";

export function NewHomePage() {
  useEffect(() => {
    const io = new IntersectionObserver(
      (entries) => {
        entries.forEach((e) => {
          if (e.isIntersecting) {
            e.target.classList.add("in");
            io.unobserve(e.target);
          }
        });
      },
      { threshold: 0.16, rootMargin: "0px 0px -40px 0px" }
    );
    document.querySelectorAll(".rv").forEach((el) => io.observe(el));
    return () => io.disconnect();
  }, []);

  return (
    <div className="new-landing-wrapper">
      <section className="hero" id="top">
        <div className="wrap">
          <div>
            <p className="eyebrow rv">PLANT INTELLIGENCE PLATFORM</p>
            <h1 className="rv" style={{ transitionDelay: ".06s" }}>
              One brain in the middle of your plant.<br />
              <span className="g">Fewer defects. Less downtime. Higher output.</span>
            </h1>
            <p className="lead rv" style={{ transitionDelay: ".12s" }}>
              PlantProcess IQ connects to the systems you already run &mdash; Level&nbsp;2, SAP, sensors, quality, lab, inspection, even Excel &mdash; reads their data, and turns it into answers: what caused this defect, where the downtime really comes from, what to adjust next shift.
            </p>
            <div className="hero-cta rv" style={{ transitionDelay: ".18s" }}>
              <a className="btn primary" href="#request-demo">Request a demo</a>
              <a className="btn ghost" href="#platform">See how it works</a>
            </div>
            <div className="hero-note rv" style={{ transitionDelay: ".24s" }}>
              <span><b>Read-only</b> toward your systems</span>
              <span><b>No-code</b> configuration</span>
              <span><b>Any industry</b>, any plant</span>
            </div>
          </div>

          <div className="hub rv" style={{ transitionDelay: ".2s" }} role="img" aria-label="PlantProcess IQ at the center of the plant">
            <svg viewBox="0 0 560 520" width="100%" aria-hidden="true">
              <defs>
                <radialGradient id="core" cx=".5" cy=".5" r=".55">
                  <stop offset="0" stopColor="#0a84ff"/><stop offset=".65" stopColor="#0b3d70"/><stop offset="1" stopColor="#0b1730"/>
                </radialGradient>
              </defs>
              <circle className="ring2" cx="280" cy="250" r="196"/>
              <circle className="ring" cx="280" cy="250" r="150"/>
              
              <line className="spoke" x1="82" y1="86" x2="238" y2="212"/><line className="spoke-flow" x1="82" y1="86" x2="238" y2="212"/>
              <line className="spoke" x1="280" y1="46" x2="280" y2="188"/><line className="spoke-flow s2" x1="280" y1="46" x2="280" y2="188"/>
              <line className="spoke" x1="478" y1="86" x2="322" y2="212"/><line className="spoke-flow s3" x1="478" y1="86" x2="322" y2="212"/>
              <line className="spoke" x1="52" y1="250" x2="218" y2="250"/><line className="spoke-flow s4" x1="52" y1="250" x2="218" y2="250"/>
              <line className="spoke" x1="508" y1="250" x2="342" y2="250"/><line className="spoke-flow s5" x1="508" y1="250" x2="342" y2="250"/>
              <line className="spoke" x1="96" y1="408" x2="240" y2="288"/><line className="spoke-flow s6" x1="96" y1="408" x2="240" y2="288"/>
              <line className="spoke" x1="464" y1="408" x2="320" y2="288"/><line className="spoke-flow s7" x1="464" y1="408" x2="320" y2="288"/>
              
              <g><rect className="srcbox" x="22" y="56" width="120" height="42" rx="7"/><text className="srct" x="82" y="74" textAnchor="middle">Level 2 &middot; L2 DB</text><text className="srcs" x="82" y="89" textAnchor="middle">process automation</text></g>
              <g><rect className="srcbox" x="220" y="14" width="120" height="42" rx="7"/><text className="srct" x="280" y="32" textAnchor="middle">SAP / ERP</text><text className="srcs" x="280" y="47" textAnchor="middle">orders &middot; materials</text></g>
              <g><rect className="srcbox" x="418" y="56" width="120" height="42" rx="7"/><text className="srct" x="478" y="74" textAnchor="middle">L1 Sensors</text><text className="srcs" x="478" y="89" textAnchor="middle">historian &middot; telemetry</text></g>
              <g><rect className="srcbox" x="6" y="228" width="96" height="42" rx="7"/><text className="srct" x="54" y="246" textAnchor="middle">Quality</text><text className="srcs" x="54" y="261" textAnchor="middle">QMS module</text></g>
              <g><rect className="srcbox" x="458" y="228" width="96" height="42" rx="7"/><text className="srct" x="506" y="246" textAnchor="middle">Lab / LIMS</text><text className="srcs" x="506" y="261" textAnchor="middle">chemistry</text></g>
              <g><rect className="srcbox" x="36" y="398" width="120" height="42" rx="7"/><text className="srct" x="96" y="416" textAnchor="middle">Inspection</text><text className="srcs" x="96" y="431" textAnchor="middle">device databases</text></g>
              <g><rect className="srcbox" x="404" y="398" width="120" height="42" rx="7"/><text className="srct" x="464" y="416" textAnchor="middle">Files</text><text className="srcs" x="464" y="431" textAnchor="middle">Excel &middot; CSV exports</text></g>
              
              <g className="hub-core">
                <circle cx="280" cy="250" r="62" fill="url(#core)" stroke="#00d4ff" strokeWidth="1.6"/>
                <text x="280" y="243" textAnchor="middle" style={{ fontFamily:"'Chakra Petch', sans-serif", fontSize:"15px", fontWeight:700 }} fill="#fff">PlantProcess</text>
                <text x="280" y="263" textAnchor="middle" style={{ fontFamily:"'Chakra Petch', sans-serif", fontSize:"17px", fontWeight:700 }} fill="#00d4ff">IQ</text>
              </g>
              
              <line className="spoke" x1="280" y1="312" x2="280" y2="472"/><line className="outflow" x1="280" y1="312" x2="280" y2="472"/>
              <line className="spoke" x1="238" y1="296" x2="150" y2="472"/><line className="outflow o2" x1="238" y1="296" x2="150" y2="472"/>
              <line className="spoke" x1="322" y1="296" x2="410" y2="472"/><line className="outflow o3" x1="322" y1="296" x2="410" y2="472"/>
              <text className="outt" x="150" y="496" textAnchor="middle">Dashboards</text>
              <text className="outt" x="280" y="496" textAnchor="middle">Predictions &middot; AI+ML</text>
              <text className="outt" x="410" y="496" textAnchor="middle">Recommendations</text>
            </svg>
          </div>
        </div>
      </section>

      <div className="band rv">
        <div className="wrap">
          <div className="cell"><div className="big">Quality <span>&uarr;</span></div><div className="s">Trace every recurring defect to its process cause across the whole production journey &mdash; and remove it at the source.</div></div>
          <div className="cell"><div className="big">Downtime <span>&darr;</span></div><div className="s">Separate equipment stops from the production losses they create, and see which ones actually cost you output.</div></div>
          <div className="cell"><div className="big">Productivity <span>&uarr;</span></div><div className="s">Yield, energy and throughput explained in one place &mdash; every 1% of prime yield recovered goes straight to margin.</div></div>
        </div>
      </div>

      <section className="section" id="platform">
        <div className="wrap">
          <p className="eyebrow rv">HOW IT WORKS</p>
          <h2 className="rv" style={{ transitionDelay: ".05s" }}>Read everything. Understand everything. Recommend the next move.</h2>
          <p className="lead rv" style={{ transitionDelay: ".1s" }}>No rip-and-replace. PlantProcess IQ sits beside your existing systems, reads their data through a secure read-only connection, rebuilds the full material genealogy, and applies statistics and AI &mdash; so your teams get answers, not another database.</p>
          <div className="flow rv" style={{ transitionDelay: ".15s" }}>
            <div className="fl-row">
              <div className="fl-node">
                <div className="ic"><svg width="26" height="26" viewBox="0 0 24 24" fill="none" stroke="#00d4ff" strokeWidth="1.8"><ellipse cx="12" cy="6" rx="8" ry="3"/><path d="M4 6v12c0 1.7 3.6 3 8 3s8-1.3 8-3V6"/><path d="M4 12c0 1.7 3.6 3 8 3s8-1.3 8-3"/></svg></div>
                <div className="h">Connect</div><div className="s">every plant source, read-only</div>
              </div>
              <div className="fl-arrow"></div>
              <div className="fl-node">
                <div className="ic"><svg width="26" height="26" viewBox="0 0 24 24" fill="none" stroke="#00d4ff" strokeWidth="1.8"><path d="M4 7h16M4 12h16M4 17h10"/><circle cx="19" cy="17" r="2.6"/></svg></div>
                <div className="h">Unify</div><div className="s">one plant model, full genealogy</div>
              </div>
              <div className="fl-arrow"></div>
              <div className="fl-node">
                <div className="ic"><svg width="26" height="26" viewBox="0 0 24 24" fill="none" stroke="#0a84ff" strokeWidth="1.8"><circle cx="12" cy="12" r="3"/><path d="M12 2v4M12 18v4M2 12h4M18 12h4M5 5l2.8 2.8M16.2 16.2L19 19M19 5l-2.8 2.8M7.8 16.2L5 19"/></svg></div>
                <div className="h">Analyze</div><div className="s">statistics &middot; correlations &middot; AI+ML</div>
              </div>
              <div className="fl-arrow"></div>
              <div className="fl-node">
                <div className="ic"><svg width="26" height="26" viewBox="0 0 24 24" fill="none" stroke="#2ce6a2" strokeWidth="1.8"><path d="M12 3l2.5 5.3 5.5.8-4 4 1 5.9-5-2.8-5 2.8 1-5.9-4-4 5.5-.8z"/></svg></div>
                <div className="h">Recommend</div><div className="s">suggestions &middot; predictions &middot; assistant</div>
              </div>
              <div className="fl-arrow"></div>
              <div className="fl-node">
                <div className="ic"><svg width="26" height="26" viewBox="0 0 24 24" fill="none" stroke="#2ce6a2" strokeWidth="1.8"><path d="M3 17l6-6 4 4 8-8"/><path d="M14 7h7v7"/></svg></div>
                <div className="h">Improve</div><div className="s">quality &middot; uptime &middot; KPIs, shift by shift</div>
              </div>
            </div>
          </div>
          <div className="grid g3">
            <div className="card rv"><span className="pill">Chatbot assistant</span><h3>Ask the plant a question</h3><p>&ldquo;Which line produced the most defects this week &mdash; and what changed in the process?&rdquo; Grounded answers with the evidence behind them, in plain language.</p></div>
            <div className="card rv" style={{ transitionDelay: ".08s" }}><span className="pill">Predictions</span><h3>See problems before the shift does</h3><p>AI models trained on your own history flag at-risk material and drifting parameters early &mdash; while there is still time to act.</p></div>
            <div className="card rv" style={{ transitionDelay: ".16s" }}><span className="pill">Recommendations</span><h3>The next best action, ranked</h3><p>Suggestion cards tell your team what to look at first &mdash; with the data trail that justifies each recommendation.</p></div>
          </div>
        </div>
      </section>

      <section className="section" id="packs">
        <div className="wrap">
          <p className="eyebrow rv">ONE PLATFORM &middot; FOUR CAPABILITY PACKS</p>
          <h2 className="rv" style={{ transitionDelay: ".05s" }}>Start where it hurts most. Expand on the same core.</h2>
          <div className="grid g4">
            <div className="card rv"><div className="icx"><svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="#00d4ff" strokeWidth="1.8"><path d="M12 2l8 4v6c0 5-3.4 8.4-8 10-4.6-1.6-8-5-8-10V6z"/><path d="M8.5 12l2.5 2.5 4.5-5"/></svg></div>
              <h3>Quality / Surface</h3><p>Trace recurring defects back through the production journey.</p></div>
            <div className="card rv" style={{ transitionDelay: ".07s" }}><div className="icx"><svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="#00d4ff" strokeWidth="1.8"><circle cx="12" cy="12" r="9"/><path d="M12 7v5l3.5 2"/></svg></div>
              <h3>Reliability / Downtime</h3><p>Separate equipment stops from the production losses they create.</p></div>
            <div className="card rv" style={{ transitionDelay: ".14s" }}><div className="icx"><svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="#00d4ff" strokeWidth="1.8"><path d="M13 2L4 14h6l-1 8 9-12h-6z"/></svg></div>
              <h3>Energy Intelligence</h3><p>Explain consumption in the context of product, process and operating conditions.</p></div>
            <div className="card rv" style={{ transitionDelay: ".21s" }}><div className="icx"><svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="#00d4ff" strokeWidth="1.8"><rect x="3" y="8" width="18" height="12" rx="2"/><path d="M7 8V5h10v3M8 13h3M8 16h6"/></svg></div>
              <h3>Yard / Logistics</h3><p>Expose location, age, movement and buffer constraints across the material journey.</p></div>
          </div>
        </div>
      </section>

      <section className="section" id="nocode">
        <div className="wrap">
          <p className="eyebrow rv">NO-CODE &middot; NO CONSULTING ARMY</p>
          <h2 className="rv" style={{ transitionDelay: ".05s" }}>Configured by your engineers &mdash; not by experts you have to hire</h2>
          <div className="split">
            <div className="rv">
              <p className="lead">Connecting a new data source, mapping its tables, building a dashboard or defining an analysis &mdash; all of it happens in a visual, no-code interface your own process engineers operate after a short onboarding.</p>
              <ul className="tick">
                <li>Generic, dynamic ETL: point at any table, map it visually, load it on a schedule</li>
                <li>Dashboards and analyses assembled by drag &amp; drop &mdash; no SQL required, SQL available when wanted</li>
                <li>Works with any vendor's systems and any industry's data model</li>
                <li>New source systems added in days, not consulting quarters</li>
              </ul>
            </div>
            <div className="nocode-mock rv" style={{ transitionDelay: ".1s" }} role="img" aria-label="Stylized no-code mapping canvas">
              <svg viewBox="0 0 520 300" width="100%" aria-hidden="true">
                <g className="ncgrid">
                  <line x1="0" y1="60" x2="520" y2="60"/><line x1="0" y1="120" x2="520" y2="120"/>
                  <line x1="0" y1="180" x2="520" y2="180"/><line x1="0" y1="240" x2="520" y2="240"/>
                  <line x1="86" y1="0" x2="86" y2="300"/><line x1="172" y1="0" x2="172" y2="300"/>
                  <line x1="258" y1="0" x2="258" y2="300"/><line x1="344" y1="0" x2="344" y2="300"/><line x1="430" y1="0" x2="430" y2="300"/>
                </g>
                <path className="ncwire" d="M150 96 C 200 96 200 150 250 150"/>
                <path className="ncwire" d="M150 216 C 200 216 200 162 250 156"/>
                <path className="ncwire" d="M360 150 C 400 150 400 150 430 150"/>
                <g><rect className="ncnode" x="30" y="66" width="120" height="58" rx="8"/><text className="nct" x="90" y="90" textAnchor="middle">SOURCE TABLE</text><text className="ncs" x="90" y="106" textAnchor="middle">l2_caster_heats</text></g>
                <g><rect className="ncnode" x="30" y="188" width="120" height="58" rx="8"/><text className="nct" x="90" y="212" textAnchor="middle">SOURCE TABLE</text><text className="ncs" x="90" y="228" textAnchor="middle">inspection_events</text></g>
                <g><rect className="ncnode" x="250" y="120" width="110" height="62" rx="8"/><text className="nct" x="305" y="146" textAnchor="middle">MAP &amp; JOIN</text><text className="ncs" x="305" y="162" textAnchor="middle">visual &middot; validated</text></g>
                <g><rect className="ncnode" x="430" y="118" width="80" height="66" rx="8" style={{ stroke: '#2ce6a2' }}/><text className="nct" x="470" y="146" textAnchor="middle" fill="#2ce6a2">PLANT</text><text className="nct" x="470" y="162" textAnchor="middle" fill="#2ce6a2">MODEL</text></g>
              </svg>
            </div>
          </div>
          <div className="inds rv">
            <span className="ind">Steel &amp; Metals</span><span className="ind">Aluminium</span><span className="ind">Chemicals</span>
            <span className="ind">Cement</span><span className="ind">Paper &amp; Pulp</span><span className="ind">Glass</span>
            <span className="ind">Automotive</span><span className="ind">Food &amp; Beverage</span><span className="ind">Your industry</span>
          </div>
        </div>
      </section>

      <section className="section roles" id="roles">
        <div className="wrap">
          <p className="eyebrow rv">WHO IT SERVES</p>
          <h2 className="rv" style={{ transitionDelay: ".05s" }}>One platform. A clear answer for every chair at the table.</h2>
          <div className="grid g3">
            <div className="card rv"><div className="who">Operations</div><h3>Find the losses that deserve attention before the next shift compounds them.</h3></div>
            <div className="card rv" style={{ transitionDelay: ".06s" }}><div className="who">Quality</div><h3>Move from a failed coil to the upstream evidence in one governed thread.</h3></div>
            <div className="card rv" style={{ transitionDelay: ".12s" }}><div className="who">Process Engineering</div><h3>Replace weeks of spreadsheet stitching with a repeatable investigation.</h3></div>
            <div className="card rv" style={{ transitionDelay: ".18s" }}><div className="who">IT &amp; OT</div><h3>Approve intelligence without introducing a control-system command path.</h3></div>
            <div className="card rv" style={{ transitionDelay: ".24s" }}><div className="who">CFO &amp; Procurement</div><h3>Fund a measurable plant outcome &mdash; not another open-ended software programme.</h3></div>
            <div className="card rv" style={{ transitionDelay: ".3s" }}><span className="pill g">Time to value</span><h3 style={{ marginTop: "12px" }}>Weeks to first insight</h3><p>Connect one line, load its history, and review the first findings with your team &mdash; typically within the first weeks of a pilot.</p></div>
          </div>
        </div>
      </section>

      <section className="section" id="deploy">
        <div className="wrap">
          <p className="eyebrow rv">DEPLOYMENT &amp; TRUST</p>
          <h2 className="rv" style={{ transitionDelay: ".05s" }}>Enterprise-grade, plant-friendly</h2>
          <div className="grid g3">
            <div className="card rv"><span className="pill">On-premise</span><h3>Your data stays in your plant</h3><p>Containerized deployment on your servers or private cloud &mdash; nothing leaves your network.</p></div>
            <div className="card rv" style={{ transitionDelay: ".08s" }}><span className="pill">Read-only</span><h3>Zero risk to production systems</h3><p>Connections toward your automation and business systems are read-only by design. PlantProcess IQ observes; it never commands.</p></div>
            <div className="card rv" style={{ transitionDelay: ".16s" }}><span className="pill">Evidence-grade</span><h3>Every number can show its source</h3><p>Findings, KPIs and recommendations trace back to the source records they came from &mdash; the level of rigor your quality and audit teams expect.</p></div>
          </div>
        </div>
      </section>

      <RequestDemoForm />
    </div>
  );
}