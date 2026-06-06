import { useEffect, useState } from "react";
import { formatPhase78Date, formatPhase78Number, getPhase78Direction, getPhase78StoredLocale, phase78LocaleStorageKey, t, type Phase78Locale } from "../i18n/phase78/phase78I18n";

function applyLocale(locale: Phase78Locale): void {
  window.localStorage.setItem(phase78LocaleStorageKey, locale);
  document.documentElement.lang = locale;
  document.documentElement.dir = getPhase78Direction(locale);
  document.documentElement.dataset.locale = locale;
  window.dispatchEvent(new CustomEvent("ppiq:locale-changed", { detail: { locale, direction: getPhase78Direction(locale) } }));
}

export function I18nRtlReadinessPage() {
  const [locale, setLocale] = useState<Phase78Locale>(() => getPhase78StoredLocale());
  const direction = getPhase78Direction(locale);

  useEffect(() => { applyLocale(locale); }, [locale]);

  return (
    <main className="phase78-readiness-page" id="main-content">
      <section className="phase78-readiness-hero">
        <p>{t("themeDirectionMatrix", locale)}</p>
        <h1>{t("readinessTitle", locale)}</h1>
        <p>{t("readinessSubtitle", locale)}</p>
        <div className="phase78-readiness-actions" aria-label={t("toggleLanguage", locale)}>
          <button type="button" onClick={() => setLocale("en")}>{t("english", locale)}</button>
          <button type="button" onClick={() => setLocale("ar")}>{t("arabic", locale)}</button>
        </div>
      </section>
      <section className="phase78-readiness-grid" aria-label="Phase 7 and Phase 8 readiness cards">
        <article className="phase78-readiness-card"><span>{t("locale", locale)}</span><strong>{locale}</strong></article>
        <article className="phase78-readiness-card"><span>{t("direction", locale)}</span><strong>{direction}</strong></article>
        <article className="phase78-readiness-card"><span>{t("numberSample", locale)}</span><strong>{formatPhase78Number(5670.25, locale)}</strong></article>
        <article className="phase78-readiness-card"><span>{t("dateSample", locale)}</span><strong>{formatPhase78Date(new Date("2026-06-06T08:00:00Z"), locale)}</strong></article>
        <article className="phase78-readiness-card"><span>{t("rtlReady", locale)}</span><strong>RTL</strong></article>
        <article className="phase78-readiness-card"><span>{t("backendHygieneReady", locale)}</span><strong>P08</strong></article>
      </section>
    </main>
  );
}
