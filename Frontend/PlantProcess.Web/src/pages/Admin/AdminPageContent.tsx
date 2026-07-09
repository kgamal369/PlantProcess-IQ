// ============================================================
// FILE: src/pages/Admin/AdminPageContent.tsx
// M1-06: Administrator is administration.
//
// The four data-integration tabs (DB Configuration, Schema Configuration,
// Importing Data, Jobs Monitor) and Connector Truth moved to /data-integration.
// Connecting a database was never an administration task, and the customer said so.
//
// STILL MISSING, deliberately not invented here: Users/Roles and System health.
// Neither exists as UI today. Building them is a feature, not an IA restructure.
// ============================================================
import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { StandardCard } from "@/components/standard";
import { apiClient } from "../../api/http";

export function AdminPageContent() {
  const [siteName, setSiteName] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    apiClient
      .get<{ siteName?: string | null }>("/admin/site-identity")
      .then((identity) => {
        if (!cancelled) setSiteName(identity?.siteName?.trim() ?? null);
      })
      .catch(() => undefined);
    return () => {
      cancelled = true;
    };
  }, []);

  return (
    <main data-admin-workspace="administrator" data-testid="administrator-page">
      <h1>Administrator</h1>
      <p>Plant identity, licensing and access. Data sources live under Data Integration.</p>

      <StandardCard
        title="Site identity"
        subtitle="The plant name shown throughout the application."
      >
        <p>{siteName ? siteName : "Site identity has not been configured."}</p>
      </StandardCard>

      <StandardCard
        title="License tier"
        subtitle="The active tier is controlled by a signed license token. It is activated at install or by support and cannot be edited from this screen."
      >
        <p>
          To change the plant license tier, activate a new signed license key. The effective
          tier and its feature gates apply immediately across the application.
        </p>
      </StandardCard>

      <StandardCard
        title="Looking for database connections?"
        subtitle="They moved."
      >
        <p>
          Connections, table registry, importing and job monitoring now live under{" "}
          <Link to="/data-integration/connections">Data Integration</Link>.
        </p>
      </StandardCard>
    </main>
  );
}