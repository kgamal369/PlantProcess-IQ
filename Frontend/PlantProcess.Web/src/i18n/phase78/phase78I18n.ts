// PlantProcess IQ Phase 7 — i18n + Arabic RTL source of truth.
// PPIQ_PHASE7_I18N_RTL
export type Phase78Locale = "en" | "ar";

export type Phase78MessageKey =
  | "language"
  | "english"
  | "arabic"
  | "toggleLanguage"
  | "readinessTitle"
  | "readinessSubtitle"
  | "direction"
  | "locale"
  | "numberSample"
  | "dateSample"
  | "themeDirectionMatrix"
  | "mobileReady"
  | "rtlReady"
  | "backendHygieneReady";

export const phase78LocaleStorageKey = "plantprocess.locale.v1";

export const phase78Messages: Record<Phase78Locale, Record<Phase78MessageKey, string>> = {
  en: {
    language: "Language",
    english: "English",
    arabic: "Arabic",
    toggleLanguage: "Switch language",
    readinessTitle: "Internationalization and RTL readiness",
    readinessSubtitle: "PlantProcess IQ is prepared for English and Arabic MENA workflows with locale-aware formatting.",
    direction: "Direction",
    locale: "Locale",
    numberSample: "Number sample",
    dateSample: "Date sample",
    themeDirectionMatrix: "Theme, locale, and direction matrix",
    mobileReady: "Mobile/touch target hardening is active.",
    rtlReady: "RTL shell support is active.",
    backendHygieneReady: "Backend API hygiene guard is active."
  },
  ar: {
    language: "اللغة",
    english: "الإنجليزية",
    arabic: "العربية",
    toggleLanguage: "تغيير اللغة",
    readinessTitle: "جاهزية الترجمة واتجاه الواجهة من اليمين إلى اليسار",
    readinessSubtitle: "PlantProcess IQ جاهز لتجارب عمل بالإنجليزية والعربية مع تنسيق محلي للأرقام والتواريخ.",
    direction: "اتجاه الواجهة",
    locale: "اللغة المحلية",
    numberSample: "مثال رقم",
    dateSample: "مثال تاريخ",
    themeDirectionMatrix: "مصفوفة السمة واللغة واتجاه الواجهة",
    mobileReady: "تحسينات الهاتف واللمس مفعلة.",
    rtlReady: "دعم الواجهة العربية من اليمين إلى اليسار مفعل.",
    backendHygieneReady: "بوابة نظافة واجهات الخلفية مفعلة."
  }
};

export function isPhase78Locale(value: string | null | undefined): value is Phase78Locale {
  return value === "en" || value === "ar";
}

export function getPhase78StoredLocale(): Phase78Locale {
  if (typeof window === "undefined") return "en";
  const stored = window.localStorage.getItem(phase78LocaleStorageKey);
  return isPhase78Locale(stored) ? stored : "en";
}

export function getPhase78Direction(locale: Phase78Locale): "ltr" | "rtl" {
  return locale === "ar" ? "rtl" : "ltr";
}

export function t(key: Phase78MessageKey, locale: Phase78Locale = getPhase78StoredLocale()): string {
  return phase78Messages[locale]?.[key] ?? phase78Messages.en[key] ?? key;
}

export function formatPhase78Number(value: number, locale: Phase78Locale = getPhase78StoredLocale()): string {
  return new Intl.NumberFormat(locale === "ar" ? "ar-EG" : "en-US", { maximumFractionDigits: 2 }).format(value);
}

export function formatPhase78Date(value: Date, locale: Phase78Locale = getPhase78StoredLocale()): string {
  return new Intl.DateTimeFormat(locale === "ar" ? "ar-EG" : "en-US", { dateStyle: "medium", timeStyle: "short" }).format(value);
}
