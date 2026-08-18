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
        connector layer for the ones we haven&rsquo;t met yet. Connector availability is confirmed per source class during the technical review; an unproven connector is presented as planned rather than as supported.
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