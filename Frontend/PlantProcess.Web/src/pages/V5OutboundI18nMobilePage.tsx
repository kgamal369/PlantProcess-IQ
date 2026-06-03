import { useEffect, useState } from "react";
import { StandardButton } from "@/components/standard";
import { V5I18nProvider, useV5I18n, v5Locales, type V5LocaleCode } from "@/i18n/v5I18n";

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

function V5OutboundI18nMobileInner() {
  const { locale, setLocaleCode, t } = useV5I18n();
  const [outboundHealth, setOutboundHealth] = useState<Health | null>(null);
  const [status, setStatus] = useState("Loading P11/P12 proof...");
  const [leadCount, setLeadCount] = useState(0);

  useEffect(() => {
    let active = true;

    Promise.all([
      api<Health>("/api/v5/outbound/health"),
      api<unknown[]>("/api/v5/outbound/leads"),
    ])
      .then(([health, leads]) => {
        if (!active) return;
        setOutboundHealth(health);
        setLeadCount(leads.length);
        setStatus("P11/P12 endpoints are reachable.");
      })
      .catch((error) => {
        if (!active) return;
        setStatus(`P11/P12 proof could not load: ${error.message}`);
      });

    return () => {
      active = false;
    };
  }, []);

  async function captureDemoLead() {
    setStatus("Capturing backend lead...");

    const result = await api<{ leadId: string; notificationQueued: boolean }>("/api/v5/outbound/leads", {
      method: "POST",
      body: JSON.stringify({
        companyName: "Demo Steel Plant",
        contactName: "Quality Director",
        email: "quality.director@example.com",
        phone: "+49 211 0000",
        jobTitle: "Quality Director",
        country: "Germany",
        plantType: "Manufacturing / Steel",
        interestArea: "Quality intelligence, genealogy and AI assistant",
        painPoints: "Need quality risk visibility and closed-loop did acting help measurement.",
        preferredContact: "email",
        consentGiven: true,
        honeypot: "",
      }),
    });

    await api("/api/v5/outbound/notifications/process-mock", { method: "POST" });
    const leads = await api<unknown[]>("/api/v5/outbound/leads");
    setLeadCount(leads.length);
    setStatus(`Lead captured in backend: ${result.leadId}. Notification queued=${result.notificationQueued}.`);
  }

  async function sendHighSeverityNotification() {
    setStatus("Queueing high severity suggestion notification...");

    const result = await api<{ queued: number }>("/api/v5/outbound/notifications/trigger", {
      method: "POST",
      body: JSON.stringify({
        eventType: "high_severity_suggestion",
        severity: "high",
        subject: "High severity PlantProcess IQ suggestion",
        payload: {
          suggestionId: "SUG-DEMO-001",
          materialCode: "C-0044180",
          riskScore: 0.87,
          message: "Inspection-first escalation recommended. This does not claim causation.",
        },
      }),
    });

    await api("/api/v5/outbound/suggestions/outcomes", {
      method: "POST",
      body: JSON.stringify({
        suggestionId: "SUG-DEMO-001",
        actionTaken: "Operator reviewed coil and increased inspection priority.",
        actedBy: "demo.operator",
        beforeWindowStartUtc: new Date(Date.now() - 2 * 60 * 60 * 1000).toISOString(),
        beforeWindowEndUtc: new Date(Date.now() - 60 * 60 * 1000).toISOString(),
        afterWindowStartUtc: new Date(Date.now() - 60 * 60 * 1000).toISOString(),
        afterWindowEndUtc: new Date().toISOString(),
        targetKpi: "quality_risk",
        beforeValue: 0.87,
        afterValue: 0.64,
        evidence: {
          mode: "demo",
          honesty: "association only; no causation claim",
        },
      }),
    });

    setStatus(`High severity notification queued on ${result.queued} channel(s), and closed-loop outcome captured.`);
  }

  return (
    <main
      dir={locale.direction}
      style={{
        minHeight: "100vh",
        padding: "clamp(16px, 4vw, 32px)",
        color: "#eaf7ff",
        background: "#050b18",
      }}
    >
      <section
        style={{
          border: "1px solid rgba(0, 212, 255, 0.22)",
          borderRadius: 24,
          padding: "clamp(18px, 4vw, 28px)",
          background: "rgba(11, 23, 48, 0.82)",
          display: "grid",
          gap: 18,
          maxWidth: 1040,
          margin: "0 auto",
        }}
      >
        <p style={{ color: "#00d4ff", textTransform: "uppercase", letterSpacing: "0.08em", fontSize: 12 }}>
          Doctrine v5 · Phase 11 / Phase 12
        </p>

        <div style={{ display: "grid", gap: 8 }}>
          <label htmlFor="v5-locale-select" style={{ fontWeight: 700 }}>
            {t("v5.p11p12.language")}
          </label>
          <select
            id="v5-locale-select"
            value={locale.code}
            onChange={(event) => setLocaleCode(event.target.value as V5LocaleCode)}
            style={{
              width: "min(100%, 280px)",
              minHeight: 44,
              borderRadius: 12,
              padding: "0 12px",
              background: "#091a33",
              color: "#eaf7ff",
              border: "1px solid rgba(0, 212, 255, 0.28)",
            }}
          >
            {v5Locales.map((item) => (
              <option key={item.code} value={item.code}>
                {item.label}
              </option>
            ))}
          </select>
        </div>

        <h1 style={{ margin: 0 }}>{t("v5.p11p12.title")}</h1>
        <p style={{ margin: 0, lineHeight: 1.7 }}>{t("v5.p11p12.description")}</p>

        <div
          style={{
            display: "grid",
            gridTemplateColumns: "repeat(auto-fit, minmax(220px, 1fr))",
            gap: 12,
          }}
        >
          <article style={{ border: "1px solid rgba(255,255,255,0.12)", borderRadius: 18, padding: 16 }}>
            <strong>Outbound</strong>
            <div>{outboundHealth?.status ?? "pending"}</div>
          </article>

          <article style={{ border: "1px solid rgba(255,255,255,0.12)", borderRadius: 18, padding: 16 }}>
            <strong>Backend leads</strong>
            <div>{leadCount}</div>
          </article>

          <article style={{ border: "1px solid rgba(255,255,255,0.12)", borderRadius: 18, padding: 16 }}>
            <strong>{t("v5.p11p12.mobileProof")}</strong>
            <div>44px+ touch targets · responsive cards · RTL mirror</div>
          </article>
        </div>

        <div
          style={{
            display: "flex",
            gap: 12,
            flexWrap: "wrap",
            alignItems: "center",
          }}
        >
          <StandardButton type="button" onClick={captureDemoLead}>
            {t("v5.p11p12.leadCta")}
          </StandardButton>

          <StandardButton type="button" variant="secondary" onClick={sendHighSeverityNotification}>
            {t("v5.p11p12.notifyCta")}
          </StandardButton>
        </div>

        <strong>{status}</strong>
      </section>
    </main>
  );
}

export function V5OutboundI18nMobilePage() {
  return (
    <V5I18nProvider>
      <V5OutboundI18nMobileInner />
    </V5I18nProvider>
  );
}

export default V5OutboundI18nMobilePage;