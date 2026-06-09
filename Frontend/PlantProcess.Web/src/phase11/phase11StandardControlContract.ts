export type Phase11StandardControlKind =
  | "button"
  | "table"
  | "input"
  | "select"
  | "textarea"
  | "tabs";

export type Phase11StandardControlRule = {
  nativeElement: Phase11StandardControlKind;
  standardComponent: string;
  required: boolean;
  reason: string;
};

export const phase11StandardControlRules: Phase11StandardControlRule[] = [
  {
    nativeElement: "button",
    standardComponent: "StandardButton",
    required: true,
    reason: "All click actions must share one disabled/loading/variant contract.",
  },
  {
    nativeElement: "table",
    standardComponent: "StandardTable",
    required: true,
    reason: "Tables must share sorting, empty, loading and visual styling behavior.",
  },
  {
    nativeElement: "input",
    standardComponent: "StandardField",
    required: true,
    reason: "Fields must share label, helper, error and disabled reason rendering.",
  },
  {
    nativeElement: "select",
    standardComponent: "StandardSelect",
    required: true,
    reason: "Select controls must share styling and accessibility behavior.",
  },
  {
    nativeElement: "textarea",
    standardComponent: "StandardTextarea",
    required: true,
    reason: "Text areas must share sizing, helper and validation behavior.",
  },
  {
    nativeElement: "tabs",
    standardComponent: "StandardTabs",
    required: true,
    reason: "Tabs must share keyboard and active indicator behavior.",
  },
];

export function standardComponentFor(nativeElement: Phase11StandardControlKind) {
  return phase11StandardControlRules.find((rule) => rule.nativeElement === nativeElement)?.standardComponent ?? "StandardSurface";
}

export function isNativeControlAllowedInNewCode(filePath: string) {
  const normalized = filePath.replaceAll("\\", "/").toLowerCase();

  if (normalized.includes("/components/standard/")) return true;
  if (normalized.includes(".generated.")) return true;
  if (normalized.includes("__tests__")) return true;
  if (normalized.endsWith(".test.tsx")) return true;

  return false;
}