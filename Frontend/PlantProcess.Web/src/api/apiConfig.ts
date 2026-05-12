function normalizeBaseUrl(value: string | undefined): string {
  const normalized = value?.trim().replace(/\/$/, "");

  if (!normalized) {
    throw new Error(
      [
        "Missing VITE_API_BASE_URL.",
        "Create Frontend/PlantProcess.Web/.env.development or root .env and set:",
        "VITE_API_BASE_URL=http://localhost:5063",
      ].join(" ")
    );
  }

  try {
    new URL(normalized);
  } catch {
    throw new Error(`Invalid VITE_API_BASE_URL: ${normalized}`);
  }

  return normalized;
}

export const API_BASE_URL = normalizeBaseUrl(
  import.meta.env.VITE_API_BASE_URL as string | undefined
);