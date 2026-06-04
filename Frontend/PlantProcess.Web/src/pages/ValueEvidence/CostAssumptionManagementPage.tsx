import { useEffect, useMemo, useState } from "react";
import useOptimisticSave from "@/hooks/useOptimisticSave";
import { V5I18nProvider, useV5I18n, v5Locales, type V5LocaleCode } from "@/i18n/v5I18n";

type Band = { low: number; mid: number; high: number };

type CostAssumptionDto = {
  currency: string;
  downgradeDeltaPerTon: Band;
  scrapCostPerTon: Band;
  downtimeCostPerMin: Band;
  gradePremiumPerTon: Band;
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

const defaultForm: CostAssumptionDto = {
  currency: "EUR",
  downgradeDeltaPerTon: { low: 80, mid: 100, high: 120 },
  scrapCostPerTon: { low: 180, mid: 220, high: 260 },
  downtimeCostPerMin: { low: 40, mid: 60, high: 80 },
  gradePremiumPerTon: { low: 150, mid: 200, high: 250 },
};

function validateBand(name: string, band: Band): string | null {
  if (band.low < 0 || band.mid < 0 || band.high < 0) return `${name}: values must be non-negative`;
  if (!(band.low <= band.mid && band.mid <= band.high)) return `${name}: low <= mid <= high`;
  return null;
}

function BandEditor({
  label,
  value,
  onChange,
}: {
  label: string;
  value: Band;
  onChange: (next: Band) => void;
}) {
  return (
    <fieldset
      style={{
        border: "1px solid rgba(0,212,255,0.22)",
        borderRadius: 16,
        padding: 14,
        display: "grid",
        gap: 10,
      }}
    >
      <legend style={{ paddingInline: 8, color: "#00d4ff", fontWeight: 800 }}>{label}</legend>
      {(["low", "mid", "high"] as const).map((key) => (
        <label key={key} style={{ display: "grid", gap: 4 }}>
          <span>{key.toUpperCase()}</span>
          <input
            type="number"
            value={value[key]}
            min={0}
            aria-label={`${label} ${key}`}
            onChange={(event) => onChange({ ...value, [key]: Number(event.target.value) })}
            style={{
              minHeight: 42,
              borderRadius: 12,
              border: "1px solid rgba(255,255,255,0.18)",
              background: "#071426",
              color: "#eaf7ff",
              paddingInline: 12,
            }}
          />
        </label>
      ))}
    </fieldset>
  );
}

function CostAssumptionInner() {
  const { locale, setLocaleCode, t } = useV5I18n();
  const [form, setForm] = useState<CostAssumptionDto>(defaultForm);
  const [status, setStatus] = useState("Ready.");
  const [version, setVersion] = useState<number | null>(null);

  const errors = useMemo(
    () =>
      [
        validateBand("downgradeDeltaPerTon", form.downgradeDeltaPerTon),
        validateBand("scrapCostPerTon", form.scrapCostPerTon),
        validateBand("downtimeCostPerMin", form.downtimeCostPerMin),
        validateBand("gradePremiumPerTon", form.gradePremiumPerTon),
      ].filter(Boolean) as string[],
    [form],
  );

  useEffect(() => {
    let alive = true;

    api<CostAssumptionDto & { version?: number }>("/api/value/cost-assumptions")
      .then((data) => {
        if (!alive) return;
        setForm({
          currency: data.currency ?? "EUR",
          downgradeDeltaPerTon: data.downgradeDeltaPerTon ?? defaultForm.downgradeDeltaPerTon,
          scrapCostPerTon: data.scrapCostPerTon ?? defaultForm.scrapCostPerTon,
          downtimeCostPerMin: data.downtimeCostPerMin ?? defaultForm.downtimeCostPerMin,
          gradePremiumPerTon: data.gradePremiumPerTon ?? defaultForm.gradePremiumPerTon,
        });
        setVersion(data.version ?? null);
        setStatus("Loaded tenant cost assumptions.");
      })
      .catch(() => setStatus("No active tenant cost assumptions yet. Demo defaults are shown."));

    return () => {
      alive = false;
    };
  }, []);

  const save = useOptimisticSave<{ version: number }>({
    toastId: "p3-cost-assumptions",
    successMessage: "Cost assumptions saved.",
    onSave: async () => {
      if (errors.length > 0) {
        throw new Error(errors.join("; "));
      }

      return api<{ version: number }>("/api/value/cost-assumptions", {
        method: "PUT",
        body: JSON.stringify(form),
      });
    },
    onSuccess: (result) => {
      setVersion(result.version);
      setStatus(`Saved version ${result.version}.`);
    },
  });

  return (
    <main
      dir={locale.direction}
      style={{
        minHeight: "100vh",
        padding: 24,
        color: "#eaf7ff",
        background: "linear-gradient(135deg,#06101f,#091a33 52%,#071426)",
      }}
    >
      <section
        aria-labelledby="p3-cost-title"
        style={{
          maxWidth: 1100,
          margin: "0 auto",
          border: "1px solid rgba(0,212,255,0.22)",
          borderRadius: 24,
          padding: 24,
          background: "rgba(7,20,38,0.86)",
          boxShadow: "0 24px 80px rgba(0,0,0,0.35)",
          display: "grid",
          gap: 20,
        }}
      >
        <div style={{ display: "flex", justifyContent: "space-between", gap: 16, flexWrap: "wrap" }}>
          <div>
            <p style={{ color: "#00d4ff", textTransform: "uppercase", letterSpacing: "0.08em", fontSize: 12 }}>
              P3 · Value & Evidence in HMI
            </p>
            <h1 id="p3-cost-title" style={{ margin: 0 }}>
              {t("v5.p3.cost.title")}
            </h1>
            <p>{t("v5.p3.cost.description")}</p>
            <p>Active version: {version ?? "not saved yet"}</p>
          </div>

          <label style={{ display: "grid", gap: 6 }}>
            <span>{t("v5.p11p12.language")}</span>
            <select
              value={locale.code}
              onChange={(event) => setLocaleCode(event.target.value as V5LocaleCode)}
              style={{ minHeight: 42, borderRadius: 12, paddingInline: 12 }}
            >
              {v5Locales.map((item) => (
                <option key={item.code} value={item.code}>
                  {item.label}
                </option>
              ))}
            </select>
          </label>
        </div>

        <label style={{ display: "grid", gap: 6, maxWidth: 220 }}>
          <span>Currency</span>
          <input
            value={form.currency}
            onChange={(event) => setForm({ ...form, currency: event.target.value })}
            style={{
              minHeight: 42,
              borderRadius: 12,
              paddingInline: 12,
              background: "#071426",
              color: "#eaf7ff",
              border: "1px solid rgba(255,255,255,0.18)",
            }}
          />
        </label>

        <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit,minmax(230px,1fr))", gap: 14 }}>
          <BandEditor
            label="Downgrade delta / ton"
            value={form.downgradeDeltaPerTon}
            onChange={(downgradeDeltaPerTon) => setForm({ ...form, downgradeDeltaPerTon })}
          />
          <BandEditor
            label="Scrap cost / ton"
            value={form.scrapCostPerTon}
            onChange={(scrapCostPerTon) => setForm({ ...form, scrapCostPerTon })}
          />
          <BandEditor
            label="Production impact downtime / min"
            value={form.downtimeCostPerMin}
            onChange={(downtimeCostPerMin) => setForm({ ...form, downtimeCostPerMin })}
          />
          <BandEditor
            label="Grade premium / ton"
            value={form.gradePremiumPerTon}
            onChange={(gradePremiumPerTon) => setForm({ ...form, gradePremiumPerTon })}
          />
        </div>

        {errors.length > 0 && (
          <div role="alert" style={{ border: "1px solid #ff9f43", borderRadius: 14, padding: 12 }}>
            {errors.map((error) => (
              <div key={error}>{error}</div>
            ))}
          </div>
        )}

        <button
          type="button"
          disabled={save.isSaving || errors.length > 0}
          onClick={save.save}
          style={{
            minHeight: 44,
            borderRadius: 14,
            border: "1px solid rgba(0,212,255,0.35)",
            color: "#eaf7ff",
            background: errors.length > 0 ? "rgba(255,255,255,0.08)" : "rgba(0,132,255,0.28)",
            cursor: errors.length > 0 ? "not-allowed" : "pointer",
            fontWeight: 800,
          }}
        >
          {save.isSaving ? "Saving..." : t("v5.p3.cost.save")}
        </button>

        <strong>{status}</strong>
      </section>
    </main>
  );
}

export function CostAssumptionManagementPage() {
  return (
    <V5I18nProvider>
      <CostAssumptionInner />
    </V5I18nProvider>
  );
}

export default CostAssumptionManagementPage;