import { createContext, useContext, useMemo, useState, type ReactNode } from "react";

export type V5LocaleCode = "en" | "de" | "ar";

export type V5Locale = {
  code: V5LocaleCode;
  label: string;
  direction: "ltr" | "rtl";
};

export const v5Locales: V5Locale[] = [
  { code: "en", label: "English", direction: "ltr" },
  { code: "de", label: "Deutsch", direction: "ltr" },
  { code: "ar", label: "العربية", direction: "rtl" },
];

const translations: Record<V5LocaleCode, Record<string, string>> = {
  en: {
    "v5.p11p12.title": "Outbound Notifications + Internationalization",
    "v5.p11p12.leadCta": "Capture demo lead",
    "v5.p11p12.notifyCta": "Send high severity notification",
    "v5.p11p12.language": "Language",
    "v5.p11p12.mobileProof": "Mobile consume-and-act proof",
    "v5.p11p12.description":
      "Backend lead capture, mock SMTP/webhook delivery, suggestion outcome tracking, German and Arabic catalogs, RTL layout, and mobile action proof.",

    "v5.p3.cost.title": "Cost Assumption Management",
    "v5.p3.cost.description": "Versioned tenant cost bands for credible value-impact ranges.",
    "v5.p3.cost.save": "Save cost bands",
  },
  de: {
    "v5.p11p12.title": "Ausgehende Benachrichtigungen + Internationalisierung",
    "v5.p11p12.leadCta": "Demo-Lead erfassen",
    "v5.p11p12.notifyCta": "Benachrichtigung mit hoher Priorität senden",
    "v5.p11p12.language": "Sprache",
    "v5.p11p12.mobileProof": "Mobiler Nachweis für Prüfen und Handeln",
    "v5.p11p12.description":
      "Backend-Lead-Erfassung, Mock-SMTP/Webhook-Zustellung, Ergebnisverfolgung für Vorschläge, deutsche und arabische Kataloge, RTL-Layout und mobiler Aktionsnachweis.",

    "v5.p3.cost.title": "Kostenannahmen verwalten",
    "v5.p3.cost.description": "Versionierte Kostenbänder pro Mandant für glaubwürdige Wertspannen.",
    "v5.p3.cost.save": "Kostenbänder speichern",
  },
  ar: {
    "v5.p11p12.title": "الإشعارات الخارجية + التدويل",
    "v5.p11p12.leadCta": "تسجيل عميل تجريبي",
    "v5.p11p12.notifyCta": "إرسال تنبيه عالي الخطورة",
    "v5.p11p12.language": "اللغة",
    "v5.p11p12.mobileProof": "إثبات الاستخدام على الهاتف والتابلت",
    "v5.p11p12.description":
      "تسجيل العملاء في قاعدة البيانات، تسليم تجريبي عبر البريد والويب هوك، قياس نتائج التوصيات، كتالوجات ألمانية وعربية، واجهة RTL، وتجربة موبايل واضحة.",

    "v5.p3.cost.title": "إدارة افتراضات التكلفة",
    "v5.p3.cost.description": "نطاقات تكلفة بإصدارات لكل مستأجر لعرض تأثير مالي موثوق.",
    "v5.p3.cost.save": "حفظ نطاقات التكلفة",
  },
};

type V5I18nContextValue = {
  locale: V5Locale;
  setLocaleCode: (locale: V5LocaleCode) => void;
  t: (key: string) => string;
};

const V5I18nContext = createContext<V5I18nContextValue | null>(null);

export function V5I18nProvider({ children }: { children: ReactNode }) {
  const [localeCode, setLocaleCode] = useState<V5LocaleCode>("en");

  const locale = useMemo(
    () => v5Locales.find((x) => x.code === localeCode) ?? v5Locales[0],
    [localeCode],
  );

  const value = useMemo<V5I18nContextValue>(
    () => ({
      locale,
      setLocaleCode,
      t: (key: string) => translations[locale.code][key] ?? translations.en[key] ?? key,
    }),
    [locale],
  );

  return <V5I18nContext.Provider value={value}>{children}</V5I18nContext.Provider>;
}

export function useV5I18n() {
  const value = useContext(V5I18nContext);

  if (!value) {
    throw new Error("useV5I18n must be used inside V5I18nProvider");
  }

  return value;
}

export function getHardcodedStringExtractionBudget() {
  return {
    screenCode: "v5-p11-p12-and-p3-proof",
    maxHardcodedUserFacingStrings: 12,
    coveredLocales: v5Locales.map((x) => x.code),
    rtlSupported: true,
  };
}