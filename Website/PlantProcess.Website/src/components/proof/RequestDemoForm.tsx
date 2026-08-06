import { useMemo, useState, type FormEvent } from "react";
import { requestDemoMail } from "../../content/phase1WebsiteProof";

// PlantProcess IQ Phase 10 lead-capture storage contract.
// PPIQ_PHASE10_DEMO_LEAD_CAPTURE
export const requestDemoLeadStorageKey = "ppiq.website.demoLeads.v1";


type FormState = {
  name: string;
  company: string;
  email: string;
  role: string;
  plantType: string;
  sourceSystems: string;
  pain: string;
  timeline: string;
  message: string;
  consentGiven: boolean;
  honeypot: string;
};

type CapturedLead = {
  leadId: string;
  company: string;
  email: string;
  plantType: string;
  sourceSystems: string;
  fitScore: number;
  status: string;
};

const initialState: FormState = {
  name: "",
  company: "",
  email: "",
  role: "",
  plantType: "",
  sourceSystems: "",
  pain: "",
  timeline: "",
  message: "",
  consentGiven: false,
  honeypot: "",
};

const websiteApiBaseUrl =
  import.meta.env.VITE_WEBSITE_API_BASE_URL ??
  import.meta.env.VITE_API_BASE_URL ??
  "http://localhost:5063";

function encode(value: string) {
  return encodeURIComponent(value);
}

function scoreFit(form: FormState) {
  const text = `${form.plantType} ${form.sourceSystems} ${form.pain} ${form.message}`.toLowerCase();
  let score = 35;

  if (/(steel|manufacturing|plant|factory|process)/i.test(text)) score += 20;
  if (/(quality|defect|inspection|genealogy|traceability)/i.test(text)) score += 25;
  if (/(mes|qms|scada|historian|oracle|sql|excel)/i.test(text)) score += 10;
  if (/(ai|assistant|prediction|risk|correlation)/i.test(text)) score += 10;

  return Math.min(score, 100);
}

function validate(form: FormState) {
  const errors: Partial<Record<keyof FormState, string>> = {};

  if (!form.name.trim()) errors.name = "Name is required.";
  if (!form.company.trim()) errors.company = "Company is required.";
  if (!/^[^@\s]+@[^@\s]+\.[^@\s]+$/.test(form.email)) errors.email = "Valid work email is required.";
  if (!form.plantType.trim()) errors.plantType = "Plant / industry type is required.";
  if (!form.sourceSystems.trim()) errors.sourceSystems = "Source systems are required.";
  if (!form.pain.trim()) errors.pain = "Main pain point is required.";
  if (!form.consentGiven) errors.consentGiven = "Consent is required.";
  if (form.honeypot.trim()) errors.honeypot = "Submission rejected.";

  return errors;
}

export function RequestDemoForm() {
  const [form, setForm] = useState<FormState>(initialState);
  const [errors, setErrors] = useState<Partial<Record<keyof FormState, string>>>({});
  const [submittedLead, setSubmittedLead] = useState<CapturedLead | null>(null);
  const [backendLeads, setBackendLeads] = useState<CapturedLead[]>([]);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [submitMessage, setSubmitMessage] = useState("");

  const mailtoHref = useMemo(() => {
    const subject = `PlantProcess IQ demo request - ${form.company || form.name || "New inquiry"}`;

    const body = [
      "PlantProcess IQ demo request",
      "",
      `Name: ${form.name}`,
      `Company: ${form.company}`,
      `Email: ${form.email}`,
      `Role: ${form.role}`,
      `Plant / industry type: ${form.plantType}`,
      `Source systems: ${form.sourceSystems}`,
      `Main pain: ${form.pain}`,
      `Timeline: ${form.timeline}`,
      "",
      "Message:",
      form.message,
      "",
      "Captured by backend lead endpoint /api/v5/outbound/leads.",
    ].join("\n");

    return `mailto:${requestDemoMail}?subject=${encode(subject)}&body=${encode(body)}`;
  }, [form]);

  function patch<K extends keyof FormState>(key: K, value: FormState[K]) {
    setForm((current) => ({
      ...current,
      [key]: value,
    }));

    setErrors((current) => ({
      ...current,
      [key]: undefined,
    }));
  }

  async function onSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    const nextErrors = validate(form);
    setErrors(nextErrors);

    if (Object.keys(nextErrors).length > 0) {
      setSubmittedLead(null);
      return;
    }

    setIsSubmitting(true);
    setSubmitMessage("Submitting backend lead...");

    try {
      const response = await fetch(`${websiteApiBaseUrl}/api/v5/outbound/leads`, {
        method: "POST",
        credentials: "include",
        headers: {
          "Content-Type": "application/json",
        },
        body: JSON.stringify({
          companyName: form.company,
          contactName: form.name,
          email: form.email,
          phone: "",
          jobTitle: form.role,
          country: "",
          plantType: form.plantType,
          interestArea: form.sourceSystems,
          painPoints: form.pain || form.message,
          preferredContact: form.timeline,
          consentGiven: form.consentGiven,
          honeypot: form.honeypot,
        }),
      });

      if (!response.ok) {
        throw new Error(`${response.status} ${response.statusText}`);
      }

      const result = (await response.json()) as {
        leadId: string;
        status: string;
        fitScore: number;
        notificationQueued: boolean;
      };

      const lead: CapturedLead = {
        leadId: result.leadId,
        company: form.company,
        email: form.email,
        plantType: form.plantType,
        sourceSystems: form.sourceSystems,
        fitScore: Math.round((result.fitScore ?? scoreFit(form) / 100) * 100),
        status: result.status ?? "new",
      };

      setSubmittedLead(lead);
      setBackendLeads((current) => [lead, ...current].slice(0, 10));
      setSubmitMessage(
        result.notificationQueued
          ? "Lead captured in backend and notification queued."
          : "Lead captured in backend.",
      );

      window.dispatchEvent(new CustomEvent("ppiq:demo-lead-captured", { detail: lead }));
    } catch (error) {
      setSubmitMessage(
        error instanceof Error
          ? `Backend lead endpoint unavailable: ${error.message}. You can still open the email draft.`
          : "Backend lead endpoint unavailable. You can still open the email draft.",
      );
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <section className="website-section request-demo-section" id="request-demo">
      <div className="section-kicker">Request demo</div>

      <div className="request-demo-layout">
        <div>
          <h2>Request a product fit check or PlantProcess IQ data diagnostic.</h2>
          <p>
            Best fit: manufacturing teams with scattered process, quality, genealogy,
            inspection, downtime, energy, lab or warehouse data who need a clear
            investigation layer without replacing existing operational systems.
          </p>

          {submittedLead ? (
            <div className="lead-success" role="status" data-testid="lead-capture-success">
              <strong>Lead captured.</strong>
              <span>
                Fit score {submittedLead.fitScore}/100. The lead is stored in the backend lead system
                and an outbound notification can be processed by the mock SMTP/webhook delivery log.
              </span>
              <a className="website-button website-button--secondary" href={mailtoHref}>
                Open notification email
              </a>
            </div>
          ) : null}

          <details className="commercial-lead-queue" data-testid="commercial-admin-lead-queue">
            <summary>Commercial Admin backend lead queue ({backendLeads.length})</summary>
            {backendLeads.length === 0 ? (
              <p>No backend leads captured in this browser session yet.</p>
            ) : (
              <ul>
                {backendLeads.map((lead) => (
                  <li key={lead.leadId}>
                    <strong>{lead.company}</strong>
                    <span>{lead.email} · {lead.plantType} · score {lead.fitScore}/100</span>
                  </li>
                ))}
              </ul>
            )}
          </details>

          {submitMessage ? <p className="lead-submit-message">{submitMessage}</p> : null}
        </div>

        <form className="request-demo-form" onSubmit={onSubmit} noValidate data-testid="demo-request-form">
          <label>
            Your name
            <input value={form.name} onChange={(event) => patch("name", event.target.value)} required />
            {errors.name ? <span className="form-error">{errors.name}</span> : null}
          </label>

          <label>
            Company
            <input value={form.company} onChange={(event) => patch("company", event.target.value)} required />
            {errors.company ? <span className="form-error">{errors.company}</span> : null}
          </label>

          <label>
            Work email
            <input type="email" value={form.email} onChange={(event) => patch("email", event.target.value)} required />
            {errors.email ? <span className="form-error">{errors.email}</span> : null}
          </label>

          <label>
            Role
            <input value={form.role} onChange={(event) => patch("role", event.target.value)} placeholder="QA lead, process engineer, plant manager..." />
          </label>

          <label>
            Plant / industry type
            <input value={form.plantType} onChange={(event) => patch("plantType", event.target.value)} required placeholder="Oil and gas, water, food, chemicals, steel, pharma..." />
            {errors.plantType ? <span className="form-error">{errors.plantType}</span> : null}
          </label>

          <label>
            Source systems
            <input value={form.sourceSystems} onChange={(event) => patch("sourceSystems", event.target.value)} required placeholder="MES, QMS, Oracle, SQL Server, Excel, inspection DB..." />
            {errors.sourceSystems ? <span className="form-error">{errors.sourceSystems}</span> : null}
          </label>

          <label>
            Main quality / process pain
            <textarea value={form.pain} onChange={(event) => patch("pain", event.target.value)} required rows={3} />
            {errors.pain ? <span className="form-error">{errors.pain}</span> : null}
          </label>

          <label>
            Timeline
            <select value={form.timeline} onChange={(event) => patch("timeline", event.target.value)}>
              <option value="">Select timeline</option>
              <option value="Discovery only">Discovery only</option>
              <option value="This month">This month</option>
              <option value="This quarter">This quarter</option>
              <option value="Pilot planning">Pilot planning</option>
            </select>
          </label>

          <label>
            Optional message
            <textarea value={form.message} onChange={(event) => patch("message", event.target.value)} rows={3} />
          </label>

          <label className="consent-row">
            <input
              type="checkbox"
              checked={form.consentGiven}
              onChange={(event) => patch("consentGiven", event.target.checked)}
              required
            />
            I agree to be contacted about PlantProcess IQ and the data diagnostic.
            {errors.consentGiven ? <span className="form-error">{errors.consentGiven}</span> : null}
          </label>

          <label className="website-hidden-field" aria-hidden="true">
            Leave this field empty
            <input tabIndex={-1} value={form.honeypot} onChange={(event) => patch("honeypot", event.target.value)} />
          </label>

          <button className="website-button website-button--primary" type="submit" disabled={isSubmitting}>
            {isSubmitting ? "Submitting..." : "Capture lead and prepare notification"}
          </button>
        </form>
      </div>
    </section>
  );
}

export default RequestDemoForm;