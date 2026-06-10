import {
  type KeyboardEvent,
  type ReactNode,
  useEffect,
  useMemo,
  useRef,
} from "react";
import "./standard-components.css";

export const P2T011_TABS_NAVIGATION_MARKER =
  "PPIQ_P2_T011_TABS_NAVIGATION_STANDARDIZATION";

export type StandardTabsOrientation = "horizontal" | "vertical";
export type StandardTabsVariant = "tabs" | "segmented" | "pills" | "breadcrumb";

export type StandardTabItem = {
  id: string;
  label: ReactNode;
  badge?: ReactNode;
  icon?: ReactNode;
  disabled?: boolean;
  href?: string;
  ariaLabel?: string;
  lazy?: boolean;

  // Backward-compatible API used by existing pages/tests.
  content?: ReactNode;
};

export type StandardTabsProps = {
  items: readonly StandardTabItem[];

  // New API.
  activeId?: string;
  syncSearchParam?: string;
  mountMode?: "all" | "active-only";
  renderPanel?: (item: StandardTabItem) => ReactNode;

  // Backward-compatible API used by existing pages/tests.
  value?: string;
  searchParam?: string;
  lazy?: boolean;

  onChange?: (id: string) => void;
  orientation?: StandardTabsOrientation;
  variant?: StandardTabsVariant;
  ariaLabel?: string;
  className?: string;
};

function cx(...values: Array<string | false | null | undefined>) {
  return values.filter(Boolean).join(" ");
}

function getEnabledItems(items: readonly StandardTabItem[]) {
  return items.filter((item) => !item.disabled);
}

function nextIndex(current: number, direction: 1 | -1, total: number) {
  if (total <= 0) return 0;
  return (current + direction + total) % total;
}

function ariaCurrent(
  variant: StandardTabsVariant,
  selected: boolean,
): boolean | "page" | "step" | "location" | "date" | "time" | "true" | "false" | undefined {
  if (variant !== "breadcrumb") return undefined;
  return selected ? "page" : undefined;
}

export function StandardTabs({
  items,
  activeId,
  value,
  onChange,
  orientation = "horizontal",
  variant = "tabs",
  ariaLabel = "Tabs",
  syncSearchParam,
  searchParam,
  className,
  mountMode,
  lazy,
  renderPanel,
}: StandardTabsProps) {
  const tabsRef = useRef<Array<HTMLButtonElement | HTMLAnchorElement | null>>([]);

  const enabledItems = useMemo(() => getEnabledItems(items), [items]);
  const resolvedActiveId = activeId ?? value ?? enabledItems[0]?.id ?? items[0]?.id ?? "";
  const activeItem = items.find((item) => item.id === resolvedActiveId) ?? enabledItems[0] ?? items[0];

  const resolvedSearchParam = syncSearchParam ?? searchParam;
  const resolvedMountMode: "all" | "active-only" =
    mountMode ?? (lazy ? "active-only" : "all");

  const hasPanels =
    Boolean(renderPanel) || items.some((item) => item.content !== undefined);

  useEffect(() => {
    if (!resolvedSearchParam || !activeItem?.id) return;

    const url = new URL(window.location.href);
    if (url.searchParams.get(resolvedSearchParam) === activeItem.id) return;

    url.searchParams.set(resolvedSearchParam, activeItem.id);
    window.history.replaceState({}, "", url);
  }, [activeItem?.id, resolvedSearchParam]);

  function activate(id: string) {
    const item = items.find((candidate) => candidate.id === id);
    if (!item || item.disabled) return;
    onChange?.(id);
  }

  function focusById(id: string) {
    const index = items.findIndex((item) => item.id === id);
    tabsRef.current[index]?.focus();
  }

  function onKeyDown(event: KeyboardEvent<HTMLDivElement>) {
    const currentEnabledIndex = enabledItems.findIndex((item) => item.id === activeItem?.id);
    const horizontal = orientation === "horizontal";

    if (
      event.key !== "ArrowRight" &&
      event.key !== "ArrowLeft" &&
      event.key !== "ArrowDown" &&
      event.key !== "ArrowUp" &&
      event.key !== "Home" &&
      event.key !== "End" &&
      event.key !== "Enter" &&
      event.key !== " "
    ) {
      return;
    }

    if (event.key === "Enter" || event.key === " ") {
      event.preventDefault();
      if (activeItem?.id) activate(activeItem.id);
      return;
    }

    let nextItem = activeItem;

    if (event.key === "Home") {
      nextItem = enabledItems[0];
    } else if (event.key === "End") {
      nextItem = enabledItems[enabledItems.length - 1];
    } else if (
      (horizontal && event.key === "ArrowRight") ||
      (!horizontal && event.key === "ArrowDown")
    ) {
      nextItem = enabledItems[nextIndex(currentEnabledIndex, 1, enabledItems.length)];
    } else if (
      (horizontal && event.key === "ArrowLeft") ||
      (!horizontal && event.key === "ArrowUp")
    ) {
      nextItem = enabledItems[nextIndex(currentEnabledIndex, -1, enabledItems.length)];
    }

    if (nextItem?.id) {
      event.preventDefault();
      activate(nextItem.id);
      focusById(nextItem.id);
    }
  }

  function renderItemContent(item: StandardTabItem) {
    return (
      <>
        {item.icon ? <span className="ppiq-std-tabs__icon">{item.icon}</span> : null}
        <span className="ppiq-std-tabs__label">{item.label}</span>
        {item.badge ? <span className="ppiq-std-tabs__badge">{item.badge}</span> : null}
      </>
    );
  }

  function renderTabButton(item: StandardTabItem, index: number) {
    const selected = item.id === activeItem?.id;
    const classNameForItem = cx(
      "ppiq-std-tabs__item",
      selected && "ppiq-std-tabs__item--active",
      item.disabled && "ppiq-std-tabs__item--disabled",
    );

    const ariaSelected = variant === "breadcrumb" ? undefined : selected;
    const ariaControls = hasPanels ? "ppiq-tab-panel-" + item.id : undefined;

    if (item.href) {
      return (
        <a
          key={item.id}
          ref={(node) => {
            tabsRef.current[index] = node;
          }}
          href={item.href}
          className={classNameForItem}
          role={variant === "breadcrumb" ? "link" : "tab"}
          aria-selected={ariaSelected}
          aria-current={ariaCurrent(variant, selected)}
          aria-disabled={item.disabled || undefined}
          aria-controls={ariaControls}
          tabIndex={selected ? 0 : -1}
          aria-label={item.ariaLabel}
          onClick={(event) => {
            if (item.disabled) {
              event.preventDefault();
              return;
            }

            activate(item.id);
          }}
        >
          {renderItemContent(item)}
        </a>
      );
    }

    return (
      <button
        key={item.id}
        ref={(node) => {
          tabsRef.current[index] = node;
        }}
        type="button"
        disabled={item.disabled}
        className={classNameForItem}
        role={variant === "breadcrumb" ? undefined : "tab"}
        aria-selected={ariaSelected}
        aria-current={ariaCurrent(variant, selected)}
        aria-disabled={item.disabled || undefined}
        aria-controls={ariaControls}
        tabIndex={selected ? 0 : -1}
        aria-label={item.ariaLabel}
        onClick={() => activate(item.id)}
      >
        {renderItemContent(item)}
      </button>
    );
  }

  function panelContent(item: StandardTabItem) {
    if (renderPanel) return renderPanel(item);
    return item.content;
  }

  return (
    <div
      className={cx(
        "ppiq-std-tabs",
        "ppiq-std-tabs--" + orientation,
        "ppiq-std-tabs--" + variant,
        className,
      )}
      data-ppiq-tabs-standardization="P2-T011"
    >
      <div
        className="ppiq-std-tabs__list"
        role={variant === "breadcrumb" ? "navigation" : "tablist"}
        aria-label={ariaLabel}
        aria-orientation={variant === "breadcrumb" ? undefined : orientation}
        onKeyDown={onKeyDown}
      >
        {items.map((item, index) => renderTabButton(item, index))}
      </div>

      {hasPanels ? (
        <div className="ppiq-std-tabs__panels">
          {items.map((item) => {
            const selected = item.id === activeItem?.id;

            if (resolvedMountMode === "active-only" && !selected) return null;

            return (
              <div
                key={item.id}
                id={"ppiq-tab-panel-" + item.id}
                role="tabpanel"
                hidden={!selected}
                aria-labelledby={"ppiq-tab-" + item.id}
                className="ppiq-std-tabs__panel"
              >
                {selected || resolvedMountMode === "all" ? panelContent(item) : null}
              </div>
            );
          })}
        </div>
      ) : null}
    </div>
  );
}
