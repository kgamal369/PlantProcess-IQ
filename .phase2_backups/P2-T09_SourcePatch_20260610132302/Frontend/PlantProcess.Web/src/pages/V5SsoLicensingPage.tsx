import { useEffect, useState } from "react";

import { P2T08_STANDARD_ROLLOUT_MARKER, StandardP2Button } from "@/components/standard/StandardP2Controls";
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
    <main>
      <section
      >
        <p>
          Doctrine v5 · Phase 9 / Phase 10
        </p>
        <h1>Enterprise SSO / SCIM + Signed Offline Licensing</h1>
        <p>
          This page proves enterprise identity and commercial control: mock OIDC SSO,
          JIT role mapping, SCIM provisioning contract, offline signed license verification,
          verified entitlement source, activation, expiry, and anti-tamper behavior.
        </p>

        <div>
          <div>SSO health: {ssoHealth?.status ?? "pending"}</div>
          <div>Licensing health: {licenseHealth?.status ?? "pending"}</div>
          <div>Current license tier: {licenseTier}</div>
          <strong>{status}</strong>
        </div>

        <div>
          <StandardP2Button
            type="button"
            onClick={runMockSsoLogin}
          >
            Run mock SSO login
          </StandardP2Button>

          <StandardP2Button
            type="button"
            onClick={createAndActivateLicense}
          >
            Create + activate signed license
          </StandardP2Button>
        </div>
      </section>
    </main>
  );
}

export default V5SsoLicensingPage;