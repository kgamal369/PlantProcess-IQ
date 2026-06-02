import { useCallback, useEffect, useState } from "react";
import { apiClient } from "@/api/http";

export type ApiResource<T> = {
  data: T | null;
  isLoading: boolean;
  error: unknown;
  reload: () => void;
};

/** Minimal GET resource over the shared apiClient. Build any query string into `path`. */
export function useApiResource<T>(path: string): ApiResource<T> {
  const [data, setData] = useState<T | null>(null);
  const [isLoading, setIsLoading] = useState<boolean>(true);
  const [error, setError] = useState<unknown>(null);
  const [tick, setTick] = useState<number>(0);

  const reload = useCallback(() => setTick((t) => t + 1), []);

  useEffect(() => {
    let cancelled = false;
    setIsLoading(true);
    setError(null);
    apiClient
      .get<T>(path, undefined, { suppressToast: true })
      .then((res) => { if (!cancelled) { setData(res); setIsLoading(false); } })
      .catch((err) => { if (!cancelled) { setError(err); setIsLoading(false); } });
    return () => { cancelled = true; };
  }, [path, tick]);

  return { data, isLoading, error, reload };
}