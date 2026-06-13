# Aquaplan Design System

Design system for **Aquaplan** — a LIMS (Laboratory Information Management System) for the digital management of drinking-water and bathing-water analyses for the Canton of Fribourg, Switzerland.

Built by **Softcom Technologies** for the Canton of Fribourg.

---

## What Aquaplan is

A single Angular 19 PWA supporting a regulated water-analysis workflow end-to-end:

- Creating and tracking **analysis orders** (prescriptions)
- Planning **sampling rounds** (tournées)
- Managing **sampling locations** and **water distributors** (communes, syndicates)
- Maintaining the catalog of **analysis programs** (which parameters to measure)
- On-site **sample collection** with barcode scanning (mobile/tablet, offline-capable)
- Viewing **analysis results**
- Administration (users, delegations, sectors)

### Users & contexts

| Profile | Context | Primary device |
|---|---|---|
| LIMS / lab agents | Desk, order & result management | Desktop |
| Field samplers (préleveurs) | Outdoors, on sampling rounds | iPad, mobile |
| Managers | Planning, validation, KPIs | Desktop |
| Distributors (communes) | Viewing their orders & results | Desktop + mobile |
| Administrators | Configuration, delegations | Desktop |

### Hard constraints (inherited, non-negotiable)

- **Stack is frozen**: Angular 19 + Angular Material + Bootstrap grid/utilities + Material Icons. No Tailwind, no PrimeNG, no headless kits.
- **Architecture is frozen**: 100% standalone, OnPush, signals, **inline templates & styles**. Tokens must be consumable from inline styles via `var(--token)`.
- **Offline / PWA safe**: no CSS loaded from network at runtime.
- **i18n**: FR / DE / EN — components must tolerate **+30%** label growth.
- **Mobile & iPad are first-class**: field samplers use them daily.
- **Touch targets**: ≥44 px on mobile, ≥48 px on iPad. Non-negotiable.
- **WCAG AA** minimum (regulated industry — AA+ where affordable).

---

## Sources used to build this system

Two markdown documents supplied by the user — **no codebase or Figma link was attached to this project**. The system below is derived from:

- `uploads/aquaplan-design-brief.md` — role framing, constraints, iteration plan
- `uploads/aquaplan-design-context.md` — full reverse-engineered tour of the current implicit system (tokens, patterns, components, observations)

Useful references cited in those docs:

- Repo: `francisuster/aquaplan` on Bitbucket (branch `main`) — **not currently accessible to this project**
- Angular Material `azure-blue` prebuilt theme — current primary source
- Bootstrap 5.3 (grid + utilities only)
- Material Icons (sole iconography today)
- `@ngx-translate/core` 17 for i18n, `idb` for offline, `@angular/service-worker` for PWA, `@zxing/*` for barcode scanning

> ⚠️ **Blocker to flag to the user**: we do not have the actual component source, the `status-chip.component.ts` palette, the current `styles.scss`, or any screenshots. The visual identity proposed here is a best-effort synthesis; the UI-kit recreations are built from the patterns described in the context doc, not from reading the live code. See the caveats at the end of the conversation. Please attach the codebase (or at least `styles.scss`, `_responsive.scss`, `status-chip.component.ts`, and 2-3 representative screenshots) so we can tighten fidelity.

---

## Design direction — "clear water, Swiss precision"

The brief gives creative freedom ("app métier interne, pas de charte cantonale imposée"). Rather than keep `azure-blue` as-is — which reads as "generic Material app" — we've taken a small deliberate step:

- **Primary** evolves into **Aquaplan Deep** `#1f7aa6` — a slightly more saturated, slightly deeper teal-blue. Still obviously "water / institutional / trustworthy", but now it's *ours*, and it holds contrast better against `#fff` and `#fafaf9`.
- **Neutrals** are very slightly warm (+2 on hue). Field tablets under fluorescent light make cold grays feel clinical; the warm offset reads as "workshop / Swiss industry" instead of "hospital corridor".
- **Status chips** are explicitly tinted (soft bg + readable fg) rather than solid fills — so a row of 50 orders doesn't turn into a carnival of saturated squares.
- **No dark mode in v1** — the context doc confirms it's out of scope.

Everything is exposed as CSS custom properties in [`colors_and_type.css`](./colors_and_type.css) so Angular inline styles can consume them as `var(--token)` without any build-step changes.

---

## Content fundamentals

**Language.** The app runs in **French (Suisse romande)** as the primary locale, with **German** and **English** translations. Copy is authored in French first.

**Voice.**

- Sober, institutional, professional — this is a regulated tool, not a consumer app. Avoid marketing register.
- **Direct and action-oriented** — users have a job to do. Lead with the verb ("Planifier une tournée", "Valider les résultats"), not with fluff.
- **Factual, never jokey.** No wink, no emoji, no exclamation marks in UI copy. Errors state the problem and, when possible, the fix.
- **Formal "vous"** in user-facing strings (French convention for institutional / B2B tools). German uses "Sie".
- **Domain-first vocabulary.** "Prélèvement", "ordre d'analyse", "tournée", "distributeur", "lieu de prélèvement", "paramètre" — these are the real words of the trade and we use them unflinchingly. Do not soften them.

**Casing.**

- **Sentence case** everywhere — page titles, buttons, labels, menu items. ("Créer un ordre", not "Créer Un Ordre".)
- **ALL CAPS only for section labels** (`.ap-section-label`), with wide letter-spacing — a muted micro-label signaling a subsection inside a form or card.
- **Button text** is a short imperative verb phrase: `Enregistrer`, `Annuler`, `Planifier`, `Valider`.

**Numbers, dates, units.**

- Swiss French conventions: `1'234.50` for thousands, `.` for decimals (per Swiss usage — not `,`).
- Dates: `DD.MM.YYYY` (e.g. `21.04.2026`).
- Times: 24-hour `HH:mm`.
- Units are always shown next to values, with a non-breaking space: `2.4 mg/L`, `12.3 °C`, `pH 7.8`.
- Parameter codes (e.g. `E. coli`, `NO₃⁻`) are italic for species names only; chemical formulas stay upright.

**Tone examples.**

| Context | ✅ Write | ❌ Avoid |
|---|---|---|
| Empty state | "Aucun ordre à planifier." | "Rien à voir ici pour l'instant 👀" |
| Error | "Identifiants invalides. Vérifiez votre e-mail et votre mot de passe." | "Oups, ça n'a pas fonctionné !" |
| Success toast | "Ordre #A-2041 enregistré." | "Bravo, c'est parti !" |
| Confirmation | "Supprimer cet ordre ? Cette action est irréversible." | "Es-tu sûr ?" |
| Loading | "Chargement des résultats…" | "Un instant, la magie opère…" |

**Emoji & decorative glyphs.** Not used. Material Icons carry all semantic weight. Unicode symbols are acceptable inside scientific content (`°C`, `µg/L`, `±`, `≤`), never as decoration.

---

## Visual foundations

### Color

- **Primary** is `#1f7aa6` (Aquaplan Deep). Use sparingly — the surface of a LIMS is mostly neutral; primary appears on the main CTA per dialog/page, active nav, focused field, and selected row.
- **Semantic palette** (success / warning / error / info) is engineered for WCAG AA on both white and `--color-neutral-50`.
- **Neutrals** carry the UI. 80%+ of the screen area is neutral 0 / 25 / 50 / 100.
- **No gradients.** Period. Flat fills only. A water-utility LIMS that looks like a crypto landing page destroys its own credibility.
- **Status chips** use soft-tint backgrounds with a high-contrast foreground label (see `--chip-*-bg` / `--chip-*-fg`). Every metier status maps to one pair.

### Type

- **Public Sans** (body), with `Helvetica Neue`, `Arial`, `sans-serif` as fallbacks — a humanist sans designed for government/institutional dense UIs. Chosen for number legibility, strong Latin Extended coverage (FR/DE), and a slightly warmer feel than generic Material defaults. See `fonts/README.md` for the migration note from the repo's current Roboto.
- **JetBrains Mono** (new) for codes, sample IDs, barcodes in UI.
- Scale is **16 px base, ratio ≈ 1.200** (minor third) — dense enough for tables, large enough for field use on an iPad.
- Body stays at **16 px on mobile** (keeps iOS form inputs from auto-zooming, per the repo's existing convention).
- `h1` = 32, `h2` = 26, `h3` = 22, `h4` = 20, `body` = 16, `body-sm` = 14, `caption` = 13, `micro` = 12.

### Spacing

- **4 px base grid** — `--space-1` (4) through `--space-16` (64). Only even multiples. No `10px`, no `18px` — the repo has these as one-offs and they should migrate to the scale.
- Inside a form: rows gap `--space-2` (8). Between sections: `--space-6` (24). Between page and first content: `--space-6` desktop, `--space-3` mobile.

### Radii

- `--radius-md` (8 px) is the workhorse — cards, round-sections, chips-on-cards.
- `--radius-sm` (4 px) — inputs, buttons (matches Material defaults).
- `--radius-chip` (6 px) — status chips.
- `--radius-pill` — only for counters / badges that need to signal a numeric value.
- **Full-screen surfaces drop to 0** on mobile (login card, dialogs) — unchanged from the repo convention.

### Elevation / shadow

- 4 steps, subtle, all with a **cool navy tint** (`rgba(17, 47, 66, …)`) instead of pure black — the shadow feels like a part of the same palette.
- `--elevation-1` — resting cards, list rows on mobile.
- `--elevation-2` — menus, popovers, sticky table headers when scrolling.
- `--elevation-3` — dialogs, bottom sheets.
- `--elevation-4` — reserved for anything that fully dims the app behind it (rare).

### Borders

- Default `1px solid var(--color-border-default)`.
- Use borders **or** elevation for separation, rarely both. Tables use borders; floating surfaces use elevation.
- Input focus: **border turns `--color-border-focus`** + a 3px outer ring `var(--focus-ring)`. Never rely on color alone — the ring is what satisfies accessibility.

### Backgrounds / imagery

- **Solid fills.** No photos in-product. No illustrations inside dense UI.
- **Login** uses a subtle water-themed motif (see `assets/bg-login-pattern.svg`) — a single low-contrast element on a `--color-neutral-50` field. Never repeats, never animated.
- No full-bleed hero images. No Lottie. No gradients.

### Motion

- **Short, functional, no bounce.** Durations in `var(--duration-fast|normal|slow)` = 120 / 180 / 280 ms.
- Default easing `var(--easing-standard)` — Material-aligned.
- Enter → decelerate, Exit → accelerate.
- **Reduce-motion** is respected: animations drop to 0 ms duration under `prefers-reduced-motion`.
- No "delightful" animations. Dialogs fade + minor Y-shift, chips don't pulse, nothing bounces.

### Hover / press / focus

| State | Treatment |
|---|---|
| Button hover | `background` darkens by one step (e.g. primary-500 → primary-600) |
| Button active/press | Darkens by two steps + very slight `translateY(0.5px)` or no movement on touch |
| Row hover (desktop) | Background → `--color-row-hover` |
| Row press (mobile) | Background → `--color-row-active` |
| Input focus | Border `--color-border-focus` + ring `--focus-ring` |
| Link hover | `text-decoration: underline` (not color change alone) |
| Card hover (clickable) | Elevation steps up by 1 |
| Disabled | `opacity: 0.5` + `cursor: not-allowed` |

### Transparency & blur

- **Rarely used.** Sticky table headers may use `backdrop-filter: blur(8px)` with a 90% opaque fill.
- No frosted-glass navbars, no translucent dialogs. The app must remain legible on low-end tablets; blur is a last resort.

### Cards

- `background: var(--color-bg-surface)` on a `--color-neutral-50` page.
- `border: 1px solid var(--color-border-default)` — the *border* is the card's edge, not the shadow.
- `border-radius: var(--radius-md)` (8 px).
- `padding: var(--space-4)` minimum, `--space-6` for dense forms.
- `box-shadow: var(--elevation-1)` only on clickable list rows (mobile cards view). Static info cards = no shadow, border only.

### Layout rules

- **Content max-width 1440 px** on desktop (LIMS tables benefit from width); sidebar is **fixed**, 240 px wide, dark-navy background (`--color-primary-800`).
- **Header is fixed**, 64 px tall, border-bottom `--color-border-default`.
- **Page padding** `--space-6` desktop, `--space-4` tablet, `--space-3` mobile.
- **Dialogs**: 560 px default, 720 px wide variant, fullscreen on mobile (`.responsive-dialog`).

---

## Iconography

**Primary system: Material Icons (outlined variant)** — matches the existing app and keeps `mat-icon` usage unchanged. We prefer the **`outlined`** weight over `filled` for everything except when the icon is inside a filled chip (status icons, filters-on badge).

- **No emoji.** No unicode icons. No hand-drawn SVG.
- **Size tokens**: `16px` inline, `20px` default, `24px` primary actions, `32px` empty states.
- **Color**: `currentColor` always — so icons inherit from text. Explicit colors only for status icons inside status chips (reuses the chip `fg` token).
- **Stroke alignment**: all Material Icons render at a consistent 2 px visual stroke. Do not mix with a second icon set — it breaks the optical weight of toolbars.

A small LIMS-specific icon need may emerge later (e.g. sample bottle, chlorine, pH meter). Our recommendation is to **commission a small custom outlined set** using the same 24×24 grid and 2 px stroke as Material — then ship as inline SVG. Not in scope for v1.

> ⚠️ **Substitution flag**: Material Icons loads from Google Fonts by default, which conflicts with the offline-PWA constraint. The Angular app should **self-host** the Material Icons font file — we've created `fonts/material-icons-README.md` with instructions and have *not* shipped the font file itself (it's ~140 KB and needs to come from the real codebase to match existing usage).

---

## Index

| File | Purpose |
|---|---|
| [`README.md`](./README.md) | This file — system overview, content rules, visual foundations, iconography |
| [`colors_and_type.css`](./colors_and_type.css) | All design tokens as CSS custom properties (colors, type, spacing, radii, elevation, z-index, touch, motion) |
| [`SKILL.md`](./SKILL.md) | Agent-Skill descriptor for reuse in Claude Code |
| [`fonts/`](./fonts/) | Public Sans + JetBrains Mono self-host notes |
| [`assets/`](./assets/) | Logos (`logo.svg`, `logo-on-dark.svg`, `mark.svg`), login pattern bg, `ICONOGRAPHY.md` |
| [`preview/`](./preview/) | Per-concept preview cards rendered in the Design System tab |
| [`ui_kits/web-app/`](./ui_kits/web-app/) | React recreation of the Aquaplan LIMS — login, orders list, order-create dialog, round detail, results |

There is **one product**: the Aquaplan Angular PWA. It serves desktop, iPad, and mobile from a single responsive codebase, so we ship a single UI kit that demonstrates all three contexts.

---
