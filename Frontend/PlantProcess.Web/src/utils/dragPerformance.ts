type DragMetric = {
  name: string;
  startedAt: number;
  frameCount: number;
  longFrameCount: number;
  maxFrameMs: number;
  lastFrameAt: number;
  rafId: number | null;
};

const active = new Map<string, DragMetric>();

export function startDragPerformanceProbe(name: string) {
  const now = performance.now();

  const metric: DragMetric = {
    name,
    startedAt: now,
    frameCount: 0,
    longFrameCount: 0,
    maxFrameMs: 0,
    lastFrameAt: now,
    rafId: null,
  };

  function tick(frameAt: number) {
    const delta = frameAt - metric.lastFrameAt;
    metric.lastFrameAt = frameAt;
    metric.frameCount += 1;
    metric.maxFrameMs = Math.max(metric.maxFrameMs, delta);

    if (delta > 16.7) {
      metric.longFrameCount += 1;
    }

    metric.rafId = requestAnimationFrame(tick);
  }

  metric.rafId = requestAnimationFrame(tick);
  active.set(name, metric);

  performance.mark(`${name}:drag-start`);
}

export function stopDragPerformanceProbe(name: string) {
  const metric = active.get(name);

  if (!metric) return null;

  if (metric.rafId !== null) {
    cancelAnimationFrame(metric.rafId);
  }

  performance.mark(`${name}:drag-end`);
  performance.measure(`${name}:drag-duration`, `${name}:drag-start`, `${name}:drag-end`);

  const durationMs = performance.now() - metric.startedAt;
  const fps = metric.frameCount > 0 ? Math.round((metric.frameCount / durationMs) * 1000) : 0;

  const result = {
    name,
    durationMs: Math.round(durationMs),
    frameCount: metric.frameCount,
    fps,
    longFrameCount: metric.longFrameCount,
    maxFrameMs: Math.round(metric.maxFrameMs),
    passed: fps >= 50 && metric.maxFrameMs <= 32,
  };

  active.delete(name);

  if (!result.passed) {
    console.warn("[PPIQ drag performance warning]", result);
  } else {
    console.info("[PPIQ drag performance ok]", result);
  }

  return result;
}