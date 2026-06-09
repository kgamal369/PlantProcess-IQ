import { useEffect, useState } from "react";

type Health = {
  status: string;
  component: string;
};

const apiBaseUrl = import.meta.env.VITE_API_BASE_URL ?? "http://localhost:5063";

async function api<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(`${apiBaseUrl}${path}`, {
    ...init,
    credentials: "include",
    headers: {
      "Content-Type": "application/json",
      ...(init?.headers ?? {}),
    },
  });

  if (!response.ok) {
    throw new Error(`${response.status} ${response.statusText}`);
  }

  return response.json() as Promise<T>;
}

export function V5SsoLicensingPage() {
  const [ssoHealth, setSsoHealth] = useState<Health | null>(null);
  const [licenseHealth, setLicenseHealth] = useState<Health | null>(null);
  const [status, setStatus] = useState("Loading P09/P10 proof...");
  const [licenseTier, setLicenseTier] = useState("pending");

  useEffect(() => {
    let active = true;

    Promise.all([
      api<Health>("/api/v5/sso/health"),
      api<Health>("/api/v5/licensing/health"),
      api<{ tier: string }>("/api/v5/licensing/current"),
    ])
      .then(([sso, licensing, current]) => {
        if (!active) return;
        setSsoHealth(sso);
        setLicenseHealth(licensing);
        setLicenseTier(current.tier);
        setStatus("P09/P10 endpoints are reachable.");
      })
      .catch((error) => {
        if (!active) return;
        setStatus(`P09/P10 proof unavailable: ${error.message}`);
      });

    return () => {
      active = false;
    };
  }, []);

  async function runMockSsoLogin() {
    setStatus("Creating mock IdP token...");

    const token = await api<{ idToken: string }>("/api/v5/sso/mock-idp/token", {
      method: "POST",
      body: JSON.stringify({
        subject: "mock-user-001",
        email: "quality.engineer@customer.example",
        displayName: "Quality Engineer",
        groups: ["QualityEngineers"],
      }),
    });

    const login = await api<{ accepted: boolean; appRole: string; plantRole: string }>("/api/v5/sso/login", {
      method: "POST",
      body: JSON.stringify({
        providerCode: "mock-idp",
        idToken: token.idToken,
      }),
    });

    setStatus(`Mock SSO login accepted=${login.accepted}; appRole=${login.appRole}; plantRole=${login.plantRole}.`);
  }

  async function createAndActivateLicense() {
    setStatus("Creating signed offline dev license...");

    const created = await api<{
      licenseKey: string;
      keyId: string;
      algorithm: string;
      payload: unknown;
      signatureBase64: string;
    }>("/api/v5/licensing/dev/create-license", {
      method: "POST",
      body: JSON.stringify({
        tier: "Enterprise",
        maxUsers: 80,
        maxSources: 25,
        maxPages: 100,
        assistantMode: "private_endpoint",
        deploymentMode: "on_prem",
        validDays: 365,
      }),
    });

    const activated = await api<{ activated: boolean; status: string }>("/api/v5/licensing/activate", {
      method: "POST",
      body: JSON.stringify(created),
    });

    const current = await api<{ tier: string }>("/api/v5/licensing/current");
    setLicenseTier(current.tier);
    setStatus(`License activation status=${activated.status}; active=${activated.activated}; tier=${current.tier}.`);
  }

  return (
    <main style={{ padding: 32, color: "#eaf7ff", background: "var(--ppiq-color-bg-deep)", minHeight: "100vh" }}>
      <section
        style={{
          border: "1px solid rgba(0, 212, 255, 0.22)",
          borderRadius: 24,
          padding: 24,
          background: "rgba(11, 23, 48, 0.82)",
        }}
      >
        <p style={{ color: "var(--ppiq-color-accent-cyan)", textTransform: "uppercase", letterSpacing: "0.08em", fontSize: 12 }}>
          Doctrine v5 · Phase 9 / Phase 10
        </p>
        <h1>Enterprise SSO / SCIM + Signed Offline Licensing</h1>
        <p>
          This page proves enterprise identity and commercial control: mock OIDC SSO,
          JIT role mapping, SCIM provisioning contract, offline signed license verification,
          verified entitlement source, activation, expiry, and anti-tamper behavior.
        </p>

        <div style={{ display: "grid", gap: 12, marginTop: 20 }}>
          <div>SSO health: {ssoHealth?.status ?? "pending"}</div>
          <div>Licensing health: {licenseHealth?.status ?? "pending"}</div>
          <div>Current license tier: {licenseTier}</div>
          <strong>{status}</strong>
        </div>

        <div style={{ display: "flex", gap: 12, flexWrap: "wrap", marginTop: 22 }}>
          <button
            type="button"
            onClick={runMockSsoLogin}
            style={{
              border: "1px solid rgba(0, 212, 255, 0.35)",
              borderRadius: 14,
              padding: "12px 18px",
              color: "#eaf7ff",
              background: "rgba(0, 132, 255, 0.24)",
              cursor: "pointer",
            }}
          >
            Run mock SSO login
          </button>

          <button
            type="button"
            onClick={createAndActivateLicense}
            style={{
              border: "1px solid rgba(44, 230, 162, 0.35)",
              borderRadius: 14,
              padding: "12px 18px",
              color: "#eaf7ff",
              background: "rgba(44, 230, 162, 0.18)",
              cursor: "pointer",
            }}
          >
            Create + activate signed license
          </button>
        </div>
      </section>
    </main>
  );
}

export default V5SsoLicensingPage;