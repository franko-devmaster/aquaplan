---
name: aquaplan-design
description: Use this skill to generate well-branded interfaces and assets for Aquaplan (LIMS for water-analysis management, Canton de Fribourg), either for production or throwaway prototypes/mocks/etc. Contains essential design guidelines, colors, type, fonts, assets, and UI kit components for prototyping.
user-invocable: true
---

Read the README.md file within this skill, and explore the other available files.

If creating visual artifacts (slides, mocks, throwaway prototypes, etc), copy assets out and create static HTML files for the user to view. If working on production code (the Angular 19 + Material + Bootstrap codebase), you can copy assets and read the rules here to become an expert in designing with this brand.

Key files:
- `README.md` — content fundamentals, visual foundations, iconography
- `colors_and_type.css` — all design tokens as CSS custom properties
- `fonts/README.md` — Public Sans + JetBrains Mono self-host notes
- `assets/` — logos (`logo.svg`, `logo-on-dark.svg`, `mark.svg`), login bg, iconography notes
- `preview/` — per-concept preview cards (colors, type, spacing, components, brand)
- `ui_kits/web-app/` — React recreation of the Aquaplan LIMS key screens (login, orders list, order dialog, round detail, results)

Hard constraints inherited from the Angular app:
- Stack is frozen: Angular Material + Bootstrap grid. No Tailwind, no PrimeNG.
- Inline templates/styles — tokens consumed via `var(--token)`.
- Offline-capable PWA — no runtime CSS fetch.
- i18n FR/DE/EN, components tolerate +30% label growth.
- Touch targets ≥44 mobile, ≥48 iPad.
- WCAG AA.

If the user invokes this skill without any other guidance, ask them what they want to build or design, ask some questions, and act as an expert designer who outputs HTML artifacts or production code, depending on the need.
