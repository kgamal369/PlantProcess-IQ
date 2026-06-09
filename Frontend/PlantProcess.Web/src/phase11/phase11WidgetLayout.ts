export type Phase11WidgetLayoutItem = {
  id: string;
  x: number;
  y: number;
  w: number;
  h: number;
  minW: number;
  minH: number;
  maxW: number;
  maxH: number;
  minimized: boolean;
  maximized: boolean;
};

export type Phase11GridBounds = {
  cols: number;
  maxRows: number;
};

export function normalizeWidgetLayout(item: Phase11WidgetLayoutItem, bounds: Phase11GridBounds): Phase11WidgetLayoutItem {
  const w = clamp(item.w, item.minW, Math.min(item.maxW, bounds.cols));
  const h = clamp(item.h, item.minH, item.maxH);
  const x = clamp(item.x, 0, Math.max(0, bounds.cols - w));
  const y = clamp(item.y, 0, Math.max(0, bounds.maxRows - h));

  return {
    ...item,
    x,
    y,
    w,
    h,
    minimized: item.maximized ? false : item.minimized,
  };
}

export function moveWidget(
  item: Phase11WidgetLayoutItem,
  bounds: Phase11GridBounds,
  deltaX: number,
  deltaY: number,
) {
  return normalizeWidgetLayout(
    {
      ...item,
      x: item.x + deltaX,
      y: item.y + deltaY,
    },
    bounds,
  );
}

export function resizeWidget(
  item: Phase11WidgetLayoutItem,
  bounds: Phase11GridBounds,
  deltaW: number,
  deltaH: number,
) {
  return normalizeWidgetLayout(
    {
      ...item,
      w: item.w + deltaW,
      h: item.h + deltaH,
    },
    bounds,
  );
}

export function minimizeWidget(item: Phase11WidgetLayoutItem): Phase11WidgetLayoutItem {
  return {
    ...item,
    minimized: true,
    maximized: false,
  };
}

export function maximizeWidget(item: Phase11WidgetLayoutItem, bounds: Phase11GridBounds): Phase11WidgetLayoutItem {
  return normalizeWidgetLayout(
    {
      ...item,
      x: 0,
      y: 0,
      w: bounds.cols,
      h: Math.min(bounds.maxRows, item.maxH),
      minimized: false,
      maximized: true,
    },
    bounds,
  );
}

export function serializeWidgetLayout(items: Phase11WidgetLayoutItem[]) {
  return JSON.stringify(items.map((item) => ({
    ...item,
    id: item.id.trim(),
  })));
}

export function restoreWidgetLayout(serialized: string, bounds: Phase11GridBounds) {
  const parsed = JSON.parse(serialized) as Phase11WidgetLayoutItem[];

  return parsed.map((item) => normalizeWidgetLayout(item, bounds));
}

function clamp(value: number, min: number, max: number) {
  return Math.max(min, Math.min(max, value));
}