import { useMemo, useState } from "react";
import { requestDemoMail } from "../../content/phase1WebsiteProof";

type FormState = {
  name: string;
  company: string;
  email: string;
  role: string;
  plantType: string;
  message: string;
};

const initialState: FormState = {
  name: "",
  company: "",
  email: "",
  role: "",
  plantType: "",
  message: "",
};

function encode(value: string) {
  return encodeURIComponent(value);
}

export function RequestDemoForm() {
  const [form, setForm] = useState<FormState>(initialState);
  const [submitted, setSubmitted] = useState(false);

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
      "",
      "Message:",
      form.message,
      "",
      "Requested from website Phase 1 demo form.",
    ].join("\n");

    return `mailto:${requestDemoMail}?subject=${encode(subject)}&body=${encode(body)}`;
  }, [form]);

  function patch<K extends keyof FormState>(key: K, value: FormState[K]) {
    setForm((current) => ({
      ...current,
      [key]: value,
    }));
  }

  function onSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setSubmitted(true);
    window.location.href = mailtoHref;
  }

  return (
    <section className="website-section request-demo-section" id="request-demo">
      <div className="section-kicker">Request demo</div>

      <div className="request-demo-layout">
        <div>
          <h2>Request a Phase 1 Golden Demo or data diagnostic.</h2>
          <p>
            Best fit: manufacturing teams with scattered process, quality,
            genealogy, inspection, downtime or lab data who need a clear
            investigation layer without replacing MES, SCADA or Level 2 systems.
          </p>

          <div className="request-demo-proof">
            <strong>Email delivery path</strong>
            <span>{requestDemoMail}</span>
            <p>
              The form opens a prepared email so the inquiry can be sent from the
              user’s mail client immediately. A backend CRM form can be added later.
            </p>
          </div>
        </div>

        <form className="request-demo-form" onSubmit={onSubmit}>
          <label>
            Name
            <input
              required
              value={form.name}
              onChange={(event) => patch("name", event.target.value)}
              placeholder="Your name"
            />
          </label>

          <label>
            Company
            <input
              required
              value={form.company}
              onChange={(event) => patch("company", event.target.value)}
              placeholder="Company / plant group"
            />
          </label>

          <label>
            Work email
            <input
              required
              type="email"
              value={form.email}
              onChange={(event) => patch("email", event.target.value)}
              placeholder="name@company.com"
            />
          </label>

          <label>
            Role
            <input
              value={form.role}
              onChange={(event) => patch("role", event.target.value)}
              placeholder="Quality, process, operations, automation, IT..."
            />
          </label>

          <label>
            Plant / industry type
            <input
              value={form.plantType}
              onChange={(event) => patch("plantType", event.target.value)}
              placeholder="Steel, paper, aluminum, food, pharma, tire..."
            />
          </label>

          <label>
            What do you want to investigate?
            <textarea
              value={form.message}
              onChange={(event) => patch("message", event.target.value)}
              placeholder="Example: surface defects, downtime impact, quality claims, genealogy gaps, process parameter correlation..."
              rows={5}
            />
          </label>

          <button className="website-button website-button--primary" type="submit">
            Send demo request
          </button>

          {submitted ? (
            <p className="request-demo-form__confirmation">
              Your email client should now open with a prepared request.
            </p>
          ) : null}
        </form>
      </div>
    </section>
  );
}

export default RequestDemoForm;