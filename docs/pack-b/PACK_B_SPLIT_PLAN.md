# Pack B Split Plan

Generated: 2026-06-06T10:44:14.465Z

| Task | File | Lines | Limit | Status |
|---|---|---:|---:|---|
| T-036 | `Frontend/PlantProcess.Web/src/components/dashboard/widget-builder/WidgetBuilderWizard.implementation.tsx` | 1512 | 400 | **NEEDS_SPLIT** |
| T-036 | `Frontend/PlantProcess.Web/src/components/dashboard/widget-builder/WidgetBuilderWizardContent.implementation.tsx` | 1710 | 400 | **NEEDS_SPLIT** |
| T-037 | `Frontend/PlantProcess.Web/src/api/productCoreApiClient.implementation.ts` | 599 | 450 | **NEEDS_SPLIT** |
| T-037 | `Frontend/PlantProcess.Web/src/pages/Admin/AdminDbConfigurationTab.implementation.tsx` | 1128 | 450 | **NEEDS_SPLIT** |
| T-037 | `Frontend/PlantProcess.Web/src/pages/MaterialAnalytics/MaterialAnalyticsPages.implementation.tsx` | 1103 | 450 | **NEEDS_SPLIT** |

## Top-level declaration hints

### Frontend/PlantProcess.Web/src/components/dashboard/widget-builder/WidgetBuilderWizard.implementation.tsx

- Line 40: interface WidgetBuilderWizardProps
- Line 48: type WizardStep
- Line 49: type RelativeDateUnit
- Line 51: interface WidgetBuilderState
- Line 67: interface ValidationIssue
- Line 72: const stepOrder
- Line 80: const defaultState
- Line 91: function generateWidgetCode
- Line 92: const slug
- Line 101: function parseJson
- Line 111: function toInputDateTime
- Line 114: const parsed
- Line 120: function fromInputDateTime
- Line 123: const parsed
- Line 129: function relativeFromUtc
- Line 130: const date
- Line 147: function formatError
- Line 152: function mapValidationIssues
- Line 153: const raw
- Line 156: const parsed
- Line 191: function isCompatible
- Line 202: const exactRule
- Line 212: function inferCategoryKey
- Line 215: const dimensionCode
- Line 227: function inferValueKey
- Line 237: function selectFieldForDimension
- Line 274: function WidgetBuilderWizardContent
- Line 297: const effectiveDashboardDefinitionId
- Line 306: let ignore
- Line 330: const filters
- Line 335: const displayOptions
- Line 382: const selectedChartType
- Line 387: const selectedDimension
- Line 392: const selectedMeasure
- Line 397: const compatibleDimensions
- Line 407: const compatibleMeasures
- Line 417: const validationIssues
- Line 418: const issues
- Line 516: const currentStepIndex
- Line 517: const canGoBack
- Line 518: const canGoNext
- Line 520: function patchState
- Line 530: function patchFilters
- Line 543: function cleanFilters
- Line 544: const filters
- Line 574: function buildQuery
- Line 596: const result
- Line 625: const filterJson
- Line 626: const displayOptionsJson
- Line 651: const saved
- Line 685: function goNext
- Line 688: const next
- Line 696: function goBack
- Line 703: const previewRows
- Line 704: const categoryKey
- Line 705: const valueKey
- Line 764: const purpose
- Line 786: const currentDimensionStillCompatible
- Line 791: const currentMeasureStillCompatible
- Line 920: function PurposeStep
- Line 954: function ChartTypeStep
- Line 988: function DataStep
- Line 1007: const parameterRequired
- Line 1121: function FilterStep
- Line 1343: function PreviewStep
- Line 1458: function PreviewTable
- Line 1461: const columns
- Line 1487: function WizardSection

### Frontend/PlantProcess.Web/src/components/dashboard/widget-builder/WidgetBuilderWizardContent.implementation.tsx

- Line 40: interface WidgetBuilderWizardProps
- Line 48: type WizardStep
- Line 56: type RelativeDateUnit
- Line 58: interface WidgetBuilderState
- Line 76: interface ValidationIssue
- Line 81: const stepOrder
- Line 90: const stepLabels
- Line 99: const defaultState
- Line 112: function generateWidgetCode
- Line 113: const slug
- Line 122: function parseJson
- Line 132: function toInputDateTime
- Line 135: const parsed
- Line 141: function fromInputDateTime
- Line 144: const parsed
- Line 150: function relativeFromUtc
- Line 151: const date
- Line 168: function formatError
- Line 173: function mapValidationIssues
- Line 174: const raw
- Line 177: const parsed
- Line 212: function isCompatible
- Line 223: const exactRule
- Line 233: function inferCategoryKey
- Line 236: const dimensionCode
- Line 248: function inferValueKey
- Line 261: function selectFieldForDimension
- Line 298: function WidgetBuilderWizardContent
- Line 323: const effectiveDashboardDefinitionId
- Line 332: let ignore
- Line 356: const filters
- Line 361: const displayOptions
- Line 415: const selectedChartType
- Line 423: const selectedDimension
- Line 431: const selectedMeasure
- Line 437: const compatibleDimensions
- Line 447: const compatibleMeasures
- Line 457: const validationIssues
- Line 458: const issues
- Line 569: const currentStepIndex
- Line 570: const canGoBack
- Line 571: const canGoNext
- Line 574: function patchState
- Line 584: function patchFilters
- Line 597: function cleanFilters
- Line 598: const filters
- Line 628: function buildQuery
- Line 651: const options
- Line 658: const result
- Line 687: const filterJson
- Line 688: const displayOptionsJson
- Line 722: const saved
- Line 756: function goNext
- Line 759: const next
- Line 767: function goBack
- Line 774: const previewRows
- Line 775: const categoryKey
- Line 776: const valueKey
- Line 837: const purpose
- Line 862: const currentDimensionStillCompatible
- Line 867: const currentMeasureStillCompatible
- Line 1023: function PurposeStep
- Line 1057: function ChartTypeStep
- Line 1091: function DataStep
- Line 1110: const parameterRequired
- Line 1224: function FilterStep
- Line 1454: function ScriptStep
- Line 1470: const widgetScriptOptions
- Line 1530: function PreviewStep
- Line 1655: function PreviewTable
- Line 1658: const columns
- Line 1684: function WizardSection

### Frontend/PlantProcess.Web/src/api/productCoreApiClient.implementation.ts

- Line 10: type QueryParams
- Line 127: function dashboardQuery
- Line 146: function dashboardBody
- Line 164: function createClientCorrelationId
- Line 176: const productApi
- Line 398: const params

### Frontend/PlantProcess.Web/src/pages/Admin/AdminDbConfigurationTab.implementation.tsx

- Line 38: type ConnectionProfileRecord
- Line 39: type CreateConnectionProfileRequest
- Line 40: type DbConfigurationSummary
- Line 41: type ProviderTypeRecord
- Line 42: type SourceDatasetDefinitionRecord
- Line 43: type SourceFieldDefinitionRecord
- Line 44: type UpdateConnectionImportScheduleRequest
- Line 61: interface ConnectionTestResult
- Line 68: type ViewMode
- Line 70: const PROVIDER_DEFAULTS
- Line 81: function DbConfigurationTab
- Line 122: function openCreate
- Line 123: function openEdit
- Line 124: function openTables
- Line 125: function backToList
- Line 259: function ConnectionProfileList
- Line 275: const result
- Line 401: function ConnectionProfileForm
- Line 409: const isEdit
- Line 425: const isFileProvider
- Line 426: const isDbProvider
- Line 428: type ConnectionProfileField
- Line 437: const validation
- Line 462: function handleProviderChange
- Line 463: const defaults
- Line 472: function set
- Line 509: const request
- Line 741: function TableBrowser
- Line 749: const profile
- Line 770: const result
- Line 789: const tableName
- Line 1030: function ImportJobSchedulePanel
- Line 1057: const request

### Frontend/PlantProcess.Web/src/pages/MaterialAnalytics/MaterialAnalyticsPages.implementation.tsx

- Line 35: type StandardTableColumn
- Line 36: type StandardTabItem
- Line 49: type Row
- Line 51: type AsyncState
- Line 58: function useResource
- Line 65: let active
- Line 97: function text
- Line 107: const row
- Line 108: const preferred
- Line 127: function number
- Line 130: const parsed
- Line 136: function percent
- Line 137: const n
- Line 142: function rows
- Line 145: const record
- Line 155: function cssKind
- Line 156: const v
- Line 172: function Chip
- Line 173: const kind
- Line 177: function PageShell
- Line 206: function Metric
- Line 227: function StandardDataState
- Line 240: const isEmpty
- Line 264: function ChartPlaceholder
- Line 291: function materialColumns
- Line 333: function MaterialAnalyticsCommandDashboardPage
- Line 335: const navigate
- Line 337: const workspace
- Line 350: const materialRows
- Line 409: function MaterialAnalyticsMaterialInvestigationPage
- Line 411: const navigate
- Line 418: const materials
- Line 423: const selectedMaterialId
- Line 424: const investigation
- Line 429: const genericRows
- Line 430: const record
- Line 441: const genericColumns
- Line 448: const tabItems
- Line 551: function MaterialAnalyticsRiskIntelligencePage
- Line 553: const risk
- Line 555: const highRisk
- Line 557: const highRiskColumns
- Line 602: function MaterialAnalyticsDataQualityPage
- Line 603: const navigate
- Line 605: const dataQuality
- Line 610: const issueRows
- Line 612: const columns
- Line 663: function MaterialAnalyticsCorrelationPage
- Line 664: const navigate
- Line 669: const correlation
- Line 694: const topBins
- Line 696: const columns
- Line 748: function MaterialAnalyticsMlReadinessPage
- Line 750: const ml
- Line 772: const metricColumns
- Line 780: const labelColumns
- Line 815: function MaterialAnalyticsDemoLifecyclePage
- Line 819: const lifecycle
- Line 846: const stepColumns
- Line 853: const progressRows
- Line 924: function MaterialAnalyticsAdminPreviewPage
- Line 926: const license
- Line 928: const roles
- Line 934: const scripts
- Line 940: const columns
- Line 979: function MaterialAnalyticsAdministratorPage
- Line 980: const admin
- Line 1002: const genericColumns
- Line 1009: const adminTabs
- Line 1065: function MaterialAnalyticsBrandIdentityPage
- Line 1066: const tokenRows
- Line 1068: const columns

