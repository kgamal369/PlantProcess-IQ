// PPIQ T-032. Chapter 4 section 5.2.5 - the toolbox, the INLINE-END region of
// the shell: grouped, searchable, and driven entirely by the block registry.
// This component knows about no individual block; adding one is a registry row.
//
// Section 5.2.3: in SQL mode the toolbox is HIDDEN ENTIRELY AND NOT MERELY
// DISABLED, because a disabled palette invites clicking and an absent one does
// not. That decision belongs to the shell, which does not render this
// component at all in SQL mode.
//
// Section 5.2.14: the advanced groups are COLLAPSED BY DEFAULT and expand with
// one click. They are not hidden behind a tier.

import { useMemo, useState } from "react";
import { StandardP2Button, StandardP2Input } from "@/components/standard/StandardP2Controls";
import { blocksInGroup, groupsForPalette, type BlockDefinition } from "./blockRegistry";

export interface AuthoringToolboxProps {
  /** Group ids this purpose presents, from the purpose registry. */
  paletteGroups: readonly string[];
  /** Stated once, at the head of the region, for blocks not yet wired. */
  unavailableReason: string;
  /**
   * T-033. Which block ids THIS surface can actually put on its board. A block
   * is clickable only when it is available in the registry AND its id is here,
   * so a surface can never present a control that does nothing. Absent means
   * none, which is exactly the T-032 behaviour.
   */
  addableBlockIds?: readonly string[];
  onAddBlock?: (blockId: string) => void;
}

export function AuthoringToolbox({
  paletteGroups, unavailableReason, addableBlockIds = [], onAddBlock,
}: AuthoringToolboxProps) {
  const canAdd = (b: BlockDefinition) => b.available && addableBlockIds.indexOf(b.id) >= 0;
  const [search, setSearch] = useState("");
  const [collapsed, setCollapsed] = useState<Record<string, boolean>>({});

  const groups = useMemo(() => groupsForPalette(paletteGroups), [paletteGroups]);
  const needle = search.trim().toLowerCase();

  const matches = (b: BlockDefinition) =>
    needle === "" || b.label.toLowerCase().indexOf(needle) >= 0;

  return (
    <div className="authoring-toolbox" data-testid="authoring-toolbox">
      <StandardP2Input
        className="authoring-toolbox__search"
        aria-label="Search the toolbox"
        placeholder="Search blocks"
        value={search}
        onChange={(e) => setSearch(e.target.value)}
      />

      <div className="authoring-toolbox__note">{unavailableReason}</div>

      {groups.map((g) => {
        const blocks = blocksInGroup(g.id).filter(matches);
        const isCollapsed = collapsed[g.id] === undefined ? g.advanced : collapsed[g.id];
        if (blocks.length === 0) { return null; }
        return (
          <div className="authoring-toolbox__group" key={g.id} data-testid={"toolbox-group-" + g.id}>
            <StandardP2Button
              variant="ghost"
              type="button"
              className="authoring-toolbox__grouphead"
              aria-expanded={!isCollapsed}
              onClick={() => setCollapsed((c) => ({ ...c, [g.id]: !isCollapsed }))}
            >
              <span className="authoring-toolbox__grouplabel">{g.label}</span>
              <span className="authoring-toolbox__groupcount">{blocks.length}</span>
            </StandardP2Button>

            {!isCollapsed && blocks.map((b) => (
              <StandardP2Button
                key={b.id}
                variant="ghost"
                type="button"
                className="authoring-toolbox__block"
                disabled={!canAdd(b)}
                aria-disabled={!canAdd(b)}
                title={b.inputs + " -> " + b.outputs}
                onClick={() => onAddBlock?.(b.id)}
              >
                <span className="authoring-toolbox__blocklabel">{b.label}</span>
                <span className="authoring-toolbox__blockports">{b.inputs} to {b.outputs}</span>
              </StandardP2Button>
            ))}
          </div>
        );
      })}
    </div>
  );
}