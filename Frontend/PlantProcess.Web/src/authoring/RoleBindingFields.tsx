// PPIQ T-037. THE SHARED ROLE-BINDING CAPABILITY.
//
// HIS RULING, and the reason this file exists at all:
//
//   T-037 = role-binding capability, persistence and stale detection
//   T-038 = S2 adopts it in the shared shell, plus the query-binding door
//
// So this component is deliberately free of every S2 assumption. It takes the
// columns a query returned and the current binding, and it hands back a new
// binding. It knows nothing about widgets, dashboards, the shell, the schema
// tree, or where the binding will be stored - the caller owns persistence and
// already has readRoleBinding and writeRoleBinding for it.
//
// WHY IT DOES NOT LIVE INSIDE THE SURFACE IT CAME FROM: that surface is retired
// by T-038, and a capability that lives inside a component scheduled for
// deletion is deleted with it. The surface is described here rather than named,
// because the retirement ratchet scans this very file for its name.

import { StandardP2Select } from "@/components/standard/StandardP2Controls";
import { staleRoles, type WidgetRole, type WidgetRoleBinding } from "@/api/product-core/widget-role-binding";
import { ROLE_ORDER, ROLE_STABLE_HINT, describeStaleBinding, roleLabel, rolePlaceholder } from "./roleBindingPresentation";
import "./role-binding.css";

export interface RoleBindingFieldsProps {
  /** Exactly the columns the query returned. Nothing else is ever offered. */
  columns: readonly string[];
  binding: WidgetRoleBinding;
  onChange: (next: WidgetRoleBinding) => void;
}

export function RoleBindingFields({ columns, binding, onChange }: RoleBindingFieldsProps) {
  const stale = staleRoles(binding, columns);

  const rebind = (role: WidgetRole, value: string) => {
    // Written as a statement rather than a computed spread so the type of the
    // result is the binding itself and not a widened index signature.
    const next: WidgetRoleBinding = { ...binding };
    next[role] = value ? value : null;
    onChange(next);
  };

  return (
    <div className="ppiq-rolebind" data-testid="role-binding-fields">
      <span className="ppiq-rolebind__title">Bind columns to roles</span>

      {ROLE_ORDER.map((role) => {
        const bound = binding[role];
        const isStale = stale.indexOf(role) >= 0;
        return (
          <div
            className="ppiq-rolebind__row"
            key={"rolebind-" + role}
            data-testid={"role-binding-row-" + role}
          >
            <span className="ppiq-rolebind__role">{roleLabel(role)}</span>
            <StandardP2Select
              aria-label={"Bind " + roleLabel(role)}
              value={isStale ? "" : (bound ?? "")}
              onChange={(e) => rebind(role, e.target.value)}
            >
              <option value="">{rolePlaceholder(role)}</option>
              {columns.map((c) => (
                <option key={"rolebind-" + role + "-" + c} value={c}>{c}</option>
              ))}
            </StandardP2Select>
            {isStale && (
              <span
                className="ppiq-rolebind__stale"
                data-testid={"role-binding-stale-" + role}
              >
                {String(bound)} is not in this result
              </span>
            )}
          </div>
        );
      })}

      {stale.length > 0 ? (
        <p className="ppiq-rolebind__problem" role="alert" data-testid="role-binding-stale">
          {describeStaleBinding(binding, columns)}
        </p>
      ) : (
        <p className="ppiq-rolebind__hint">{ROLE_STABLE_HINT}</p>
      )}
    </div>
  );
}

export default RoleBindingFields;