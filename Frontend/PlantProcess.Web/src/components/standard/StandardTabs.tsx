
import { useEffect, useMemo, useRef, type KeyboardEvent, type ReactNode } from "react";
import "./standard-components.css";

export type StandardTabsOrientation = "horizontal" | "vertical";

export type StandardTabItem<TId extends string = string> = {
  id: TId;
  label: ReactNode;
  icon?: ReactNode;
  badge?: ReactNode;
  disabled?: boolean;
  content: ReactNode;
  preload?: boolean;
};

export type StandardTabsProps<TId extends string = string> = {
  items: ReadonlyArray<StandardTabItem<TId>>;
  value: TId;
  onChange: (value: TId) => void;
  orientation?: StandardTabsOrientation;
  lazy?: boolean;
  searchParam?: string;
  ariaLabel: string;
  className?: string;
};

function cx(...values: Array<string | false | null | undefined>) {
  return values.filter(Boolean).join(" ");
}

export function StandardTabs<TId extends string = string>({
  items,
  value,
  onChange,
  orientation = "horizontal",
  lazy = true,
  searchParam,
  ariaLabel,
  className,
}: StandardTabsProps<TId>) {
  const refs = useRef<Array<HTMLButtonElement | null>>([]);

  const enabledItems = useMemo(() => items.filter((item) => !item.disabled), [items]);
  const activeIndex = items.findIndex((item) => item.id === value);
  const activeItem = items[activeIndex] ?? items[0];

  useEffect(() => {
    if (!searchParam) return;

    const params = new URLSearchParams(window.location.search);
    const tabFromUrl = params.get(searchParam);
    const match = items.find((item) => item.id === tabFromUrl && !item.disabled);

    if (match && match.id !== value) {
      onChange(match.id);
    }
  }, []);

  useEffect(() => {
    if (!searchParam) return;

    const url = new URL(window.location.href);
    url.searchParams.set(searchParam, value);
    window.history.replaceState(null, "", url.toString());
  }, [searchParam, value]);

  function activateByOffset(offset: number) {
    const currentEnabledIndex = enabledItems.findIndex((item) => item.id === value);
    const nextIndex = (currentEnabledIndex + offset + enabledItems.length) % enabledItems.length;
    const next = enabledItems[nextIndex];

    if (next) {
      onChange(next.id);
      const realIndex = items.findIndex((item) => item.id === next.id);
      refs.current[realIndex]?.focus();
    }
  }

  function onKeyDown(event: KeyboardEvent<HTMLDivElement>) {
    const nextKey = orientation === "horizontal" ? "ArrowRight" : "ArrowDown";
    const previousKey = orientation === "horizontal" ? "ArrowLeft" : "ArrowUp";

    if (event.key === nextKey) {
      event.preventDefault();
      activateByOffset(1);
    }

    if (event.key === previousKey) {
      event.preventDefault();
      activateByOffset(-1);
    }

    if (event.key === "Home") {
      event.preventDefault();
      const first = enabledItems[0];
      if (first) onChange(first.id);
    }

    if (event.key === "End") {
      event.preventDefault();
      const last = enabledItems[enabledItems.length - 1];
      if (last) onChange(last.id);
    }

    if (event.key === "Enter" || event.key === " ") {
      event.preventDefault();
      const focusedIndex = refs.current.findIndex((item) => item === document.activeElement);
      const item = items[focusedIndex];
      if (item && !item.disabled) onChange(item.id);
    }
  }

  return (
    <div className={cx("ppiq-std-tabs", "ppiq-std-tabs--" + orientation, className)}>
      <div
        className="ppiq-std-tabs__list"
        role="tablist"
        aria-label={ariaLabel}
        aria-orientation={orientation}
        onKeyDown={onKeyDown}
      >
        {items.map((item, index) => (
          <button
            key={item.id}
            ref={(node) => {
              refs.current[index] = node;
            }}
            id={"ppiq-tab-" + item.id}
            type="button"
            role="tab"
            className="ppiq-std-tabs__button"
            aria-selected={item.id === value}
            aria-controls={"ppiq-tab-panel-" + item.id}
            disabled={item.disabled}
            tabIndex={item.id === value ? 0 : -1}
            onClick={() => {
              if (!item.disabled) onChange(item.id);
            }}
          >
            {item.icon ? <span aria-hidden="true">{item.icon}</span> : null}
            <span>{item.label}</span>
            {item.badge ? <span className="ppiq-std-tabs__badge">{item.badge}</span> : null}
          </button>
        ))}
      </div>

      <div className="ppiq-std-tabs__panel">
        {items.map((item) => {
          const isActive = item.id === value;

          if (lazy && !isActive && !item.preload) {
            return null;
          }

          return (
            <div
              key={item.id}
              id={"ppiq-tab-panel-" + item.id}
              role="tabpanel"
              aria-labelledby={"ppiq-tab-" + item.id}
              hidden={!isActive}
            >
              {item.content}
            </div>
          );
        })}

        {!activeItem ? null : null}
      </div>
    </div>
  );
}
