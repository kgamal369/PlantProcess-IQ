import { formatPhase78Date, formatPhase78Number, getPhase78Direction, getPhase78StoredLocale, phase78LocaleStorageKey, t, type Phase78Locale } from "./phase78I18n";

function applyLocale(locale: Phase78Locale, announce = false): void {
  const direction = getPhase78Direction(locale);
  document.documentElement.lang = locale;
  document.documentElement.dir = direction;
  document.documentElement.dataset.locale = locale;
  document.documentElement.dataset.phase78Direction = direction;
  window.localStorage.setItem(phase78LocaleStorageKey, locale);

  const state = document.getElementById("ppiq-phase78-language-state");
  if (state) state.textContent = locale.toUpperCase();

  const toggle = document.getElementById("ppiq-phase78-language-toggle");
  if (toggle) {
    toggle.setAttribute("aria-label", t("toggleLanguage", locale));
    toggle.setAttribute("title", t("toggleLanguage", locale));
  }

  const sample = document.getElementById("ppiq-phase78-locale-sample");
  if (sample) {
    sample.textContent = t("numberSample", locale) + ": " + formatPhase78Number(5670.25, locale) + " · " + t("dateSample", locale) + ": " + formatPhase78Date(new Date(), locale);
  }

  const live = document.getElementById("ppiq-phase56-live-region") || document.getElementById("ppiq-phase78-live-region");
  if (announce && live) live.textContent = locale === "ar" ? "تم تغيير اللغة إلى العربية." : "Language changed to English.";

  window.dispatchEvent(new CustomEvent("ppiq:locale-changed", { detail: { locale, direction } }));
}

function ensureLiveRegion(): void {
  if (document.getElementById("ppiq-phase78-live-region")) return;
  const live = document.createElement("div");
  live.id = "ppiq-phase78-live-region";
  live.className = "ppiq-sr-only";
  live.setAttribute("aria-live", "polite");
  live.setAttribute("aria-atomic", "true");
  document.body.appendChild(live);
}

function ensureToggle(): void {
  if (document.getElementById("ppiq-phase78-language-toggle")) return;
  const button = document.createElement("button");
  button.id = "ppiq-phase78-language-toggle";
  button.type = "button";
  button.className = "phase78-language-toggle";
  button.innerHTML = '<span aria-hidden="true">ع/A</span><span id="ppiq-phase78-language-state"></span>';
  button.addEventListener("click", () => {
    const current = getPhase78StoredLocale();
    applyLocale(current === "ar" ? "en" : "ar", true);
  });
  document.body.appendChild(button);
}

function ensureSample(): void {
  if (document.getElementById("ppiq-phase78-locale-sample")) return;
  const sample = document.createElement("div");
  sample.id = "ppiq-phase78-locale-sample";
  sample.className = "ppiq-sr-only";
  document.body.appendChild(sample);
}

export function initializePhase78I18nRuntime(): void {
  if (typeof window === "undefined") return;
  applyLocale(getPhase78StoredLocale());
  window.addEventListener("DOMContentLoaded", () => {
    ensureLiveRegion();
    ensureSample();
    ensureToggle();
    applyLocale(getPhase78StoredLocale());
  });
}

initializePhase78I18nRuntime();
