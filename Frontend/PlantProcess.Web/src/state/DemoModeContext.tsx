import {
  createContext,
  useContext,
  useMemo,
  useState,
  type ReactNode,
} from "react";

import {
  customerReportSections,
  demoChecklist,
  demoConnectors,
  demoJobs,
  demoMappings,
  demoMlPreview,
  demoPrivilegeGroups,
  demoUsers,
  demoWidgets,
  executiveFiveMinuteScript,
  forbiddenLanguageExamples,
  initialMlTrainingForm,
  licensePlans,
  objections,
  safeLanguageExamples,
  screenshotPack,
  twentyMinuteScript,
  type DemoChecklistItem,
  type DemoConnector,
  type DemoJob,
  type DemoLicensePlan,
  type DemoMlPreview,
  type DemoMlTrainingForm,
  type DemoObjection,
  type DemoPrivilegeGroup,
  type DemoReportSection,
  type DemoSchemaMapping,
  type DemoScreenshotItem,
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

  mlTrainingForm: DemoMlTrainingForm;
  setMlTrainingForm: (value: DemoMlTrainingForm) => void;

  connectors: DemoConnector[];
  jobs: DemoJob[];
  mappings: DemoSchemaMapping[];
  widgets: DemoWidget[];
  users: DemoUserRole[];
  privilegeGroups: DemoPrivilegeGroup[];
  mlPreview: DemoMlPreview;
  checklist: DemoChecklistItem[];
  screenshots: DemoScreenshotItem[];
  objections: DemoObjection[];
  customerReportSections: DemoReportSection[];
  executiveFiveMinuteScript: string[];
  twentyMinuteScript: string[];
  forbiddenLanguageExamples: string[];
  safeLanguageExamples: string[];

  visibleWidgets: DemoWidget[];
  isFeatureAvailable: (requiredPlan: DemoLicensePlan) => boolean;
  resetDemoState: () => void;
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
  const [mlTrainingForm, setMlTrainingForm] =
    useState<DemoMlTrainingForm>(initialMlTrainingForm);

  const value = useMemo<DemoModeContextValue>(() => {
    const isFeatureAvailable = (requiredPlan: DemoLicensePlan) =>
      planRank[selectedPlan] >= planRank[requiredPlan];

    const resetDemoState = () => {
      setDemoModeEnabled(true);
      setSelectedPlan("ProPlus");
      setActiveLifecycleStep(0);
      setMlTrainingForm(initialMlTrainingForm);
    };

    return {
      demoModeEnabled,
      setDemoModeEnabled,

      selectedPlan,
      setSelectedPlan,

      activeLifecycleStep,
      setActiveLifecycleStep,

      mlTrainingForm,
      setMlTrainingForm,

      connectors: demoConnectors,
      jobs: demoJobs,
      mappings: demoMappings,
      widgets: demoWidgets,
      users: demoUsers,
      privilegeGroups: demoPrivilegeGroups,
      mlPreview: demoMlPreview,
      checklist: demoChecklist,
      screenshots: screenshotPack,
      objections,
      customerReportSections,
      executiveFiveMinuteScript,
      twentyMinuteScript,
      forbiddenLanguageExamples,
      safeLanguageExamples,

      visibleWidgets: demoWidgets,
      isFeatureAvailable,
      resetDemoState,
    };
  }, [activeLifecycleStep, demoModeEnabled, mlTrainingForm, selectedPlan]);

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