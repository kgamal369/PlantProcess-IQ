// PPIQ T-242 Stage 2. A BOOLEAN IS A PORT TYPE.
//
// Comparison produces one, Boolean logic consumes and produces one, and
// IF/ELSE is steered by one. Without it those three families cannot state an
// honest port signature, and "incompatible ports refuse before execution"
// cannot be proven - the board would have to smuggle a decision through a
// text or number port and hope nobody wired it into a join.
//
// This remains the SINGLE declaration site for PortType, which is what the
// C241-06 architecture guard exists to hold.
export type PortType = "key" | "number" | "text" | "date" | "boolean" | "flow";

export const PORT_COLORS: Record<PortType, string> = {
  key: "#00d4ff", number: "#0a84ff", text: "#8ea7c1", date: "#b48ef6",
  boolean: "#ff9f45", flow: "#2ce6a2",
};

/** Spec S4: a connection is valid only between compatible port types. */
export function portsCompatible(a: PortType, b: PortType): boolean {
  if (a === "flow" || b === "flow") return a === b;
  // T-242. A boolean is a DECISION, not a joinable column value, so it never
  // takes part in the key wildcard below. Without this line a comparison
  // result could be wired into a join key and silently accepted.
  if (a === "boolean" || b === "boolean") return a === b;
  if (a === "key" || b === "key") return true; // keys may join typed columns
  return a === b;
}

export function inferPortType(sqlType: string): PortType {
  const t = sqlType.toLowerCase();
  if (/(int|numeric|decimal|float|double|real)/.test(t)) return "number";
  if (/(date|time)/.test(t)) return "date";
  return "text";
}