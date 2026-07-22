export type PortType = "key" | "number" | "text" | "date" | "flow";

export const PORT_COLORS: Record<PortType, string> = {
  key: "#00d4ff", number: "#0a84ff", text: "#8ea7c1", date: "#b48ef6", flow: "#2ce6a2",
};

/** Spec S4: a connection is valid only between compatible port types. */
export function portsCompatible(a: PortType, b: PortType): boolean {
  if (a === "flow" || b === "flow") return a === b;
  if (a === "key" || b === "key") return true; // keys may join typed columns
  return a === b;
}

export function inferPortType(sqlType: string): PortType {
  const t = sqlType.toLowerCase();
  if (/(int|numeric|decimal|float|double|real)/.test(t)) return "number";
  if (/(date|time)/.test(t)) return "date";
  return "text";
}