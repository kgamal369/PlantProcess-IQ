import { useCallback, useEffect, useRef, useState } from "react";

type PollingState<T> = {
  data: T | null;
  error: Error | null;
  isLoading: boolean;
  isRefreshing: boolean;
  lastUpdatedAt: Date | null;
  refresh: () => Promise<void>;
};

export function useLatestOnlyPolling<T>(
  loader: (signal: AbortSignal) => Promise<T>,
  intervalMs: number,
  enabled = true
): PollingState<T> {
  const [data, setData] = useState<T | null>(null);
  const [error, setError] = useState<Error | null>(null);
  const [isLoading, setIsLoading] = useState(enabled);
  const [isRefreshing, setIsRefreshing] = useState(false);
  const [lastUpdatedAt, setLastUpdatedAt] = useState<Date | null>(null);

  const requestIdRef = useRef(0);
  const abortRef = useRef<AbortController | null>(null);
  const mountedRef = useRef(false);
  const hasLoadedOnceRef = useRef(false);

  const refresh = useCallback(async () => {
    if (!enabled) return;

    const requestId = requestIdRef.current + 1;
    requestIdRef.current = requestId;

    abortRef.current?.abort();

    const controller = new AbortController();
    abortRef.current = controller;

    const firstLoad = !hasLoadedOnceRef.current;

    setIsLoading(firstLoad);
    setIsRefreshing(!firstLoad);

    try {
      const result = await loader(controller.signal);

      if (!mountedRef.current || requestIdRef.current !== requestId) {
        return;
      }

      hasLoadedOnceRef.current = true;
      setData(result);
      setError(null);
      setLastUpdatedAt(new Date());
    } catch (caught) {
      if (controller.signal.aborted) return;

      if (!mountedRef.current || requestIdRef.current !== requestId) {
        return;
      }

      setError(caught instanceof Error ? caught : new Error("Polling failed."));
    } finally {
      if (mountedRef.current && requestIdRef.current === requestId) {
        setIsLoading(false);
        setIsRefreshing(false);
      }
    }
  }, [enabled, loader]);

  useEffect(() => {
    mountedRef.current = true;

    if (!enabled) {
      setIsLoading(false);
      setIsRefreshing(false);
      return () => {
        mountedRef.current = false;
        abortRef.current?.abort();
      };
    }

    void refresh();

    const timer = window.setInterval(() => {
      void refresh();
    }, intervalMs);

    return () => {
      mountedRef.current = false;
      window.clearInterval(timer);
      abortRef.current?.abort();
    };
  }, [enabled, intervalMs, refresh]);

  return {
    data,
    error,
    isLoading,
    isRefreshing,
    lastUpdatedAt,
    refresh,
  };
}