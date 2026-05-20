import {
  createContext,
  useContext,
  useMemo,
  useState,
  type ReactNode,
} from "react";

import {
  demoConnectors,
  demoJobs,
  demoMappings,
  demoMlPreview,
  demoUsers,
  demoWidgets,
  licensePlans,
  type DemoConnector,
  type DemoJob,
  type DemoLicensePlan,
  type DemoMlPreview,
  type DemoSchemaMapping,
  type DemoUserRole,
  type DemoWidget,
} from "@/demo/plantProcessDemoScenario";

type DemoModeContextValue = {
  demoModeEnabled: boolean;
  setDemoModeEnabled: (value: boolean) => void;

  selectedPlan: DemoLicensePlan;
  setSelectedPlan: (value: DemoLicensePlan) => void;

  activeLifecycleStep: number;
  setActiveLifecycleStep: (value: number) => void;

  connectors: DemoConnector[];
  jobs: DemoJob[];
  mappings: DemoSchemaMapping[];
  widgets: DemoWidget[];
  users: DemoUserRole[];
  mlPreview: DemoMlPreview;

  visibleWidgets: DemoWidget[];
  isFeatureAvailable: (requiredPlan: DemoLicensePlan) => boolean;
};

const planRank: Record<DemoLicensePlan, number> = {
  Light: 1,
  Pro: 2,
  ProPlus: 3,
  Enterprise: 4,
};

const DemoModeContext = createContext<DemoModeContextValue | null>(null);

export function DemoModeProvider({ children }: { children: ReactNode }) {
  const [demoModeEnabled, setDemoModeEnabled] = useState(true);
  const [selectedPlan, setSelectedPlan] = useState<DemoLicensePlan>("ProPlus");
  const [activeLifecycleStep, setActiveLifecycleStep] = useState(0);

  const value = useMemo<DemoModeContextValue>(() => {
    const isFeatureAvailable = (requiredPlan: DemoLicensePlan) =>
      planRank[selectedPlan] >= planRank[requiredPlan];

    return {
      demoModeEnabled,
      setDemoModeEnabled,

      selectedPlan,
      setSelectedPlan,

      activeLifecycleStep,
      setActiveLifecycleStep,

      connectors: demoConnectors,
      jobs: demoJobs,
      mappings: demoMappings,
      widgets: demoWidgets,
      users: demoUsers,
      mlPreview: demoMlPreview,

      visibleWidgets: demoWidgets,
      isFeatureAvailable,
    };
  }, [activeLifecycleStep, demoModeEnabled, selectedPlan]);

  return (
    <DemoModeContext.Provider value={value}>
      {children}
    </DemoModeContext.Provider>
  );
}

export function useDemoMode() {
  const context = useContext(DemoModeContext);

  if (!context) {
    throw new Error("useDemoMode must be used inside DemoModeProvider");
  }

  return context;
}

export { licensePlans };