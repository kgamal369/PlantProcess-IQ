
import { describe, expect, it } from "vitest";
import {
  p3T17BuildDatabaseSeedPreview,
  p3T17CostI18nRows,
  p3T17CostTranslations,
  p3T17LocaleDirections,
  p3T17QualifiedKey,
  p3T17RequiredCostKeys,
  p3T17RequiredLocales,
  p3T17TranslateCostKey,
  p3T17ValidateCostI18nCatalog,
} from "./p3T17CostAssumptionI18n";

describe("P3-T17 cost-assumption i18n contract", () => {
  it("has every Cost Assumption Management key in English, German, and Arabic", () => {
    const validation = p3T17ValidateCostI18nCatalog();

    expect(validation.isGreen).toBe(true);
    expect(validation.localeCount).toBe(3);
    expect(validation.keyCount).toBe(3);
    expect(validation.rowCount).toBe(9);
    expect(validation.missing).toEqual([]);
  });

  it("uses stable qualified keys under v5.p3.cost namespace", () => {
    expect(p3T17QualifiedKey("title")).toBe("v5.p3.cost.title");
    expect(p3T17QualifiedKey("description")).toBe("v5.p3.cost.description");
    expect(p3T17QualifiedKey("save")).toBe("v5.p3.cost.save");
  });

  it("certifies Arabic as RTL and verifies Arabic script is present", () => {
    expect(p3T17LocaleDirections.ar).toBe("rtl");
    expect(p3T17CostTranslations.ar.title).toMatch(/[\u0600-\u06FF]/);
    expect(p3T17TranslateCostKey("ar", "save")).toBe("حفظ نطاقات التكلفة");
  });

  it("falls back to English for unknown locale without leaking raw keys to the screen", () => {
    expect(p3T17TranslateCostKey("fr", "title")).toBe("Cost Assumption Management");
    expect(p3T17TranslateCostKey("fr", "title")).not.toBe("v5.p3.cost.title");
  });

  it("builds nine unique database seed rows for the DB i18n catalog", () => {
    const rows = p3T17CostI18nRows();
    const ids = new Set(rows.map((row) => row.localeCode + ":" + row.qualifiedKey));

    expect(rows).toHaveLength(p3T17RequiredLocales.length * p3T17RequiredCostKeys.length);
    expect(ids.size).toBe(rows.length);

    const preview = p3T17BuildDatabaseSeedPreview();
    expect(preview).toContain("en|v5.p3.cost|title|ltr|Cost Assumption Management");
    expect(preview).toContain("de|v5.p3.cost|save|ltr|Kostenbänder speichern");
    expect(preview).toContain("ar|v5.p3.cost|title|rtl|إدارة افتراضات التكلفة");
  });
});
