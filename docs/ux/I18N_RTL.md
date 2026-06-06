# PlantProcess IQ Phase 7 — i18n and Arabic RTL

Runtime key: `plantprocess.locale.v1`.

Source files:
- `Frontend/PlantProcess.Web/src/i18n/phase78/phase78I18n.ts`
- `Frontend/PlantProcess.Web/src/i18n/phase78/phase78I18nRuntime.ts`
- `Frontend/PlantProcess.Web/src/styles/phase78/phase78-i18n-rtl.css`

Add a string by extending `Phase78MessageKey`, then adding values in both `phase78Messages.en` and `phase78Messages.ar`.
Add a locale by extending `Phase78Locale`, adding a resource bundle, and updating `getPhase78Direction` when the locale is RTL.
