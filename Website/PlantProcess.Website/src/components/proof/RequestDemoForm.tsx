import { useMemo, useState, type FormEvent } from "react";
import { requestDemoMail } from "../../content/phase1WebsiteProof";

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
};

type StoredLead = FormState & {
  id: string;
  capturedAtUtc: string;
  fitScore: number;
  status: "captured" | "notification-draft-ready";
};

const storageKey = "ppiq.website.demoLeads.v1";

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
};

function encode(value: string) {
  return encodeURIComponent(value);
}

function readLeads(): StoredLead[] {
  if (typeof window === "undefined") return [];

  try {
    const raw = window.localStorage.getItem(storageKey);
    if (!raw) return [];
    const parsed = JSON.parse(raw);
    return Array.isArray(parsed) ? parsed : [];
  } catch {
    return [];
  }
}

function writeLeads(leads: StoredLead[]) {
  window.localStorage.setItem(storageKey, JSON.stringify(leads.slice(-50)));
}

function scoreFit(form: FormState) {
  let score = 0;

  if (form.company.trim()) score += 15;
  if (form.email.includes("@")) score += 15;
  if (form.role.trim()) score += 10;
  if (form.plantType.trim()) score += 15;
  if (form.sourceSystems.trim()) score += 20;
  if (form.pain.trim()) score += 20;
  if (form.timeline.trim()) score += 5;

  return Math.min(score, 100);
}

function validate(form: FormState) {
  const errors: Partial<Record<keyof FormState, string>> = {};

  if (!form.name.trim()) errors.name = "Name is required.";
  if (!form.company.trim()) errors.company = "Company is required.";
  if (!/^[^@\s]+@[^@\s]+\.[^@\s]+$/.test(form.email.trim())) {
    errors.email = "A valid work email is required.";
  }
  if (!form.plantType.trim()) errors.plantType = "Plant / industry type is required.";
  if (!form.sourceSystems.trim()) errors.sourceSystems = "At least one source system is required.";
  if (!form.pain.trim()) errors.pain = "Main quality or process pain is required.";

  return errors;
}

export function RequestDemoForm() {
  const [form, setForm] = useState<FormState>(initialState);
  const [errors, setErrors] = useState<Partial<Record<keyof FormState, string>>>({});
  const [submittedLead, setSubmittedLead] = useState<StoredLead | null>(null);
  const [leads, setLeads] = useState<StoredLead[]>(() => readLeads());

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
      "Captured by website lead form.",
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

  function onSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    const nextErrors = validate(form);
    setErrors(nextErrors);

    if (Object.keys(nextErrors).length > 0) {
      setSubmittedLead(null);
      return;
    }

    const lead: StoredLead = {
      ...form,
      id: `lead-${Date.now()}`,
      capturedAtUtc: new Date().toISOString(),
      fitScore: scoreFit(form),
      status: "notification-draft-ready",
    };

    const nextLeads = [...readLeads(), lead];
    writeLeads(nextLeads);
    setLeads(nextLeads);
    setSubmittedLead(lead);

    window.dispatchEvent(new CustomEvent("ppiq:demo-lead-captured", { detail: lead }));
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
                Fit score {submittedLead.fitScore}/100. The lead is stored in the local commercial queue
                and the notification email draft is ready.
              </span>
              <a className="website-button website-button--secondary" href={mailtoHref}>
                Open notification email
              </a>
            </div>
          ) : null}

          <details className="commercial-lead-queue" data-testid="commercial-admin-lead-queue">
            <summary>Commercial Admin lead queue ({leads.length})</summary>
            {leads.length === 0 ? (
              <p>No captured website leads yet.</p>
            ) : (
              <ul>
                {leads.slice().reverse().map((lead) => (
                  <li key={lead.id}>
                    <strong>{lead.company}</strong>
                    <span>{lead.email} · {lead.plantType} · score {lead.fitScore}/100</span>
                  </li>
                ))}
              </ul>
            )}
          </details>
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
            <input value={form.plantType} onChange={(event) => patch("plantType", event.target.value)} required placeholder="Steel, paper, pharma, food, aluminum..." />
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

          <button className="website-button website-button--primary" type="submit">
            Capture lead and prepare notification
          </button>
        </form>
      </div>
    </section>
  );
}

export default RequestDemoForm;