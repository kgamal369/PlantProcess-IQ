import { routeDecision, visibleNavigation, type CommercialTier, type FormalPlantRole } from "@/security/roleAccess";
import "./phase9-personas.css";

const demoRoles: FormalPlantRole[] = ["ChiefExecutiveOfficer", "MaintenanceEngineer", "ProcessEngineer", "PlantAdmin", "Viewer"];
const tier: CommercialTier = "Enterprise";

export function PersonaAccessMatrixPage() {
  return (
    <main className="phase9-page" data-testid="phase9-persona-access-matrix">
      <section className="phase9-hero">
        <p className="phase9-eyebrow">Role and permission matrix by persona.</p>
        <h1>Persona Access Matrix</h1>
        <p className="phase9-muted">
          Routes are scoped by formal role and tier. Ungranted features are shown disabled with a reason instead of a dead click or unexplained 404.
        </p>
        <p className="phase9-muted">
          This matrix is descriptive. Access is enforced by the server on every request; a
          route absent from this table is denied, not permitted.
        </p>
      </section>

      {demoRoles.map((role) => (
        <section className="phase9-card" key={role}>
          <h2>{role}</h2>
          <div className="phase9-grid">
            {visibleNavigation(role, tier).slice(0, 10).map((item) => (
              <article className="phase9-card" key={role + item.path}>
                <strong>{item.path}</strong>
                <p className={item.allowed ? "phase9-muted" : "phase9-disabled"}>
                  {item.allowed ? "Allowed" : "Disabled"} — {item.reason}
                </p>
              </article>
            ))}
          </div>
        </section>
      ))}

      <section className="phase9-card">
        <h2>Example route guard</h2>
        <p className="phase9-muted">
          Maintenance engineer opening assistant configuration:
        </p>
        <pre>{JSON.stringify(routeDecision("/assistant/configuration", "MaintenanceEngineer", "Enterprise"), null, 2)}</pre>
      </section>
    </main>
  );
}

export default PersonaAccessMatrixPage;