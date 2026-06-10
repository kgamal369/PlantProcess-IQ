import { BadgeEuro, FlaskConical, PlayCircle } from "lucide-react";

import { licensePlans, useDemoMode } from "@/state/DemoModeContext";
import { StandardButton } from "@/components/standard";

import { StandardP2Select } from "@/components/standard/StandardP2Controls";
export function DemoModeControl() {
  const {
    demoModeEnabled,
    setDemoModeEnabled,
    selectedPlan,
    setSelectedPlan,
  } = useDemoMode();

  return (
    <div className="demo-mode-control">
      <StandardButton
        className={`demo-mode-toggle ${demoModeEnabled ? "active" : ""}`}
        type="button"
        onClick={() => setDemoModeEnabled(!demoModeEnabled)}
        title="Toggle frontend demo mode"
      >
        <FlaskConical size={15} />
        {demoModeEnabled ? "Demo mode on" : "Demo mode off"}
      </StandardButton>

      <label className="demo-plan-select">
        <BadgeEuro size={15} />
        <StandardP2Select
          value={selectedPlan}
          onChange={(event) => setSelectedPlan(event.target.value as typeof selectedPlan)}
        >
          {licensePlans.map((plan) => (
            <option key={plan.code} value={plan.code}>
              {plan.name}
            </option>
          ))}
        </StandardP2Select>
      </label>

      <a className="demo-run-link" href="/demo-lifecycle">
        <PlayCircle size={15} />
        Run flow
      </a>
    </div>
  );
}