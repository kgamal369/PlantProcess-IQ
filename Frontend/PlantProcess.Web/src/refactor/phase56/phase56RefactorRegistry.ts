export type Phase56RefactorTarget = {
  taskId: string;
  source: string;
  targetModule: string;
  status: 'protected' | 'split' | 'documented';
  rationale: string;
};

export const phase56RefactorTargets: Phase56RefactorTarget[] = [
  {
    taskId: 'T-036',
    source: 'src/components/dashboarding/WidgetBuilderWizardContent.implementation.tsx',
    targetModule: 'src/components/dashboarding/widget-builder/',
    status: 'protected',
    rationale: 'God-file split target is now tracked by file-size and interaction gates. Manual AST-aware split must preserve wizard behavior exactly.'
  },
  {
    taskId: 'T-037',
    source: 'src/api/productCoreApiClient.implementation.ts',
    targetModule: 'src/api/product-core/',
    status: 'protected',
    rationale: 'Endpoint-domain split is tracked by implementation convention docs and file-size gates.'
  },
  {
    taskId: 'T-037',
    source: 'src/pages/Admin/AdminDbConfigurationTab.implementation.tsx',
    targetModule: 'src/pages/Admin/db-configuration/',
    status: 'protected',
    rationale: 'Admin DB tab section split target is tracked without changing runtime behavior in this safe installer.'
  },
  {
    taskId: 'T-037',
    source: 'src/pages/MaterialAnalytics/MaterialAnalyticsPages.implementation.tsx',
    targetModule: 'src/pages/MaterialAnalytics/sections/',
    status: 'protected',
    rationale: 'Material analytics section split target is tracked by regression and file-size gates.'
  }
];
