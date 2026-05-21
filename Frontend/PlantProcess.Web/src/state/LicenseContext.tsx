import {
  createContext,
  type ReactNode,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
} from "react";
import {
  licenseApi,
  licenseUsageApi,
  type LicenseFeatureStatus,
  type LicenseStatus,
  type LicenseUsageResponse,
  type CommercialReadinessResponse,
} from "../api/license";

interface LicenseContextValue {
  license: LicenseStatus | null;
  usage: LicenseUsageResponse | null;
  readiness: CommercialReadinessResponse | null;
  isLoading: boolean;
  error: string | null;
  refresh: () => Promise<void>;
  hasFeature: (feature: string) => boolean;
  getFeature: (feature: string) => LicenseFeatureStatus | undefined;
}

const LicenseContext = createContext<LicenseContextValue | undefined>(
  undefined
);

export function LicenseProvider({ children }: { children: ReactNode }) {
  const [license, setLicense] = useState<LicenseStatus | null>(null);
  const [usage, setUsage] = useState<LicenseUsageResponse | null>(null);
  const [readiness, setReadiness] =
    useState<CommercialReadinessResponse | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const refresh = useCallback(async () => {
    setIsLoading(true);
    setError(null);

    try {
      const [licenseResult, usageResult, readinessResult] = await Promise.all([
        licenseApi.getCurrent(),
        licenseUsageApi.getUsage(),
        licenseUsageApi.getCommercialReadiness(),
      ]);

      setLicense(licenseResult);
      setUsage(usageResult);
      setReadiness(readinessResult);
    } catch (err) {
      const message =
        err instanceof Error
          ? err.message
          : "Failed to load license configuration.";

      setError(message);
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    void refresh();
  }, [refresh]);

  const getFeature = useCallback(
    (feature: string) => {
      return license?.features.find(
        (item) => item.feature.toLowerCase() === feature.toLowerCase()
      );
    },
    [license]
  );

  const hasFeature = useCallback(
    (feature: string) => {
      return getFeature(feature)?.isEnabled ?? false;
    },
    [getFeature]
  );

  const value = useMemo<LicenseContextValue>(
    () => ({
      license,
      usage,
      readiness,
      isLoading,
      error,
      refresh,
      hasFeature,
      getFeature,
    }),
    [license, usage, readiness, isLoading, error, refresh, hasFeature, getFeature]
  );

  return (
    <LicenseContext.Provider value={value}>{children}</LicenseContext.Provider>
  );
}

export function useLicense() {
  const value = useContext(LicenseContext);

  if (!value) {
    throw new Error("useLicense must be used inside LicenseProvider.");
  }

  return value;
}