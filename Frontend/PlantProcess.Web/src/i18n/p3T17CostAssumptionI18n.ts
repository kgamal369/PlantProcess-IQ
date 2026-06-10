
export const P3_T17_COST_I18N_CONTRACT = "PPIQ_P3_T17_COST_I18N_CONTRACT";

export type P3T17LocaleCode = "en" | "de" | "ar";

export type P3T17Direction = "ltr" | "rtl";

export type P3T17CostI18nKey = "title" | "description" | "save";

export type P3T17QualifiedCostI18nKey =
  | "v5.p3.cost.title"
  | "v5.p3.cost.description"
  | "v5.p3.cost.save";

export type P3T17CostI18nRow = {
  localeCode: P3T17LocaleCode;
  namespace: "v5.p3.cost";
  stringKey: P3T17CostI18nKey;
  qualifiedKey: P3T17QualifiedCostI18nKey;
  translatedText: string;
  direction: P3T17Direction;
  screenCode: "v5-p3-cost-assumptions";
  isHighTraffic: true;
};

export const p3T17CostI18nNamespace = "v5.p3.cost" as const;

export const p3T17CostI18nScreenCode = "v5-p3-cost-assumptions" as const;

export const p3T17RequiredLocales: P3T17LocaleCode[] = ["en", "de", "ar"];

export const p3T17LocaleDirections: Record<P3T17LocaleCode, P3T17Direction> = {
  en: "ltr",
  de: "ltr",
  ar: "rtl",
};

export const p3T17RequiredCostKeys: P3T17CostI18nKey[] = [
  "title",
  "description",
  "save",
];

export const p3T17CostTranslations: Record<P3T17LocaleCode, Record<P3T17CostI18nKey, string>> = {
  en: {
    title: "Cost Assumption Management",
    description: "Versioned tenant cost bands for credible value-impact ranges.",
    save: "Save cost bands",
  },
  de: {
    title: "Kostenannahmen verwalten",
    description: "Versionierte Kostenbänder pro Mandant für glaubwürdige Wertspannen.",
    save: "Kostenbänder speichern",
  },
  ar: {
    title: "إدارة افتراضات التكلفة",
    description: "نطاقات تكلفة بإصدارات لكل مستأجر لعرض تأثير مالي موثوق.",
    save: "حفظ نطاقات التكلفة",
  },
};

export function p3T17QualifiedKey(key: P3T17CostI18nKey): P3T17QualifiedCostI18nKey {
  return (p3T17CostI18nNamespace + "." + key) as P3T17QualifiedCostI18nKey;
}

export function p3T17TranslateCostKey(
  localeCode: P3T17LocaleCode | string,
  key: P3T17CostI18nKey,
): string {
  const safeLocale = p3T17RequiredLocales.includes(localeCode as P3T17LocaleCode)
    ? (localeCode as P3T17LocaleCode)
    : "en";

  return p3T17CostTranslations[safeLocale][key] ?? p3T17CostTranslations.en[key];
}

export function p3T17CostI18nRows(): P3T17CostI18nRow[] {
  const rows: P3T17CostI18nRow[] = [];

  for (const localeCode of p3T17RequiredLocales) {
    for (const stringKey of p3T17RequiredCostKeys) {
      rows.push({
        localeCode,
        namespace: p3T17CostI18nNamespace,
        stringKey,
        qualifiedKey: p3T17QualifiedKey(stringKey),
        translatedText: p3T17CostTranslations[localeCode][stringKey],
        direction: p3T17LocaleDirections[localeCode],
        screenCode: p3T17CostI18nScreenCode,
        isHighTraffic: true,
      });
    }
  }

  return rows;
}

export function p3T17ValidateCostI18nCatalog() {
  const missing: string[] = [];
  const duplicateKeys = new Set<string>();
  const seen = new Set<string>();
  const rows = p3T17CostI18nRows();

  for (const localeCode of p3T17RequiredLocales) {
    for (const stringKey of p3T17RequiredCostKeys) {
      const value = p3T17CostTranslations[localeCode]?.[stringKey];

      if (!value || !value.trim()) {
        missing.push(localeCode + ":" + p3T17QualifiedKey(stringKey));
      }
    }
  }

  for (const row of rows) {
    const id = row.localeCode + ":" + row.qualifiedKey;

    if (seen.has(id)) {
      duplicateKeys.add(id);
    }

    seen.add(id);
  }

  const arabicTitle = p3T17CostTranslations.ar.title;
  const arabicLooksArabic = /[\u0600-\u06FF]/.test(arabicTitle);

  if (p3T17LocaleDirections.ar !== "rtl") {
    missing.push("ar:direction:rtl");
  }

  if (!arabicLooksArabic) {
    missing.push("ar:arabic-script");
  }

  return {
    marker: P3_T17_COST_I18N_CONTRACT,
    isGreen: missing.length === 0 && duplicateKeys.size === 0 && rows.length === 9,
    namespace: p3T17CostI18nNamespace,
    screenCode: p3T17CostI18nScreenCode,
    localeCount: p3T17RequiredLocales.length,
    keyCount: p3T17RequiredCostKeys.length,
    rowCount: rows.length,
    missing,
    duplicateKeys: Array.from(duplicateKeys),
    rows,
  };
}

export function p3T17BuildDatabaseSeedPreview(): string {
  return p3T17CostI18nRows()
    .map((row) =>
      row.localeCode +
      "|" +
      row.namespace +
      "|" +
      row.stringKey +
      "|" +
      row.direction +
      "|" +
      row.translatedText,
    )
    .join("\n");
}
