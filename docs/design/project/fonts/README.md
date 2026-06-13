# Fonts

## Design-system choice: Public Sans

The design system uses **Public Sans** as the primary UI typeface.

Why Public Sans (and not Roboto, the repo's current choice):

- **Purpose-built for dense government/institutional apps** — designed by the US Web Design System team for tabular, form-heavy, high-density interfaces. Exactly Aquaplan's context.
- **Excellent number legibility** — tabular figures, clear 0/O and 1/l/I distinction (critical for sample codes like `A-2041` and values like `2.4 mg/L`).
- **Full Latin Extended coverage** — FR accents, German umlauts, Swiss ß all handled natively.
- **Slightly warmer and more distinctive than Roboto** — feels less "default Material app", which aligns with the "Swiss precision / clear water" direction. Still utterly sober: no flourishes, no personality overrides.
- **Open source (SIL OFL 1.1)** — safe for government deployment.

The repo's current `Roboto` fallback chain is preserved as a safety net, so if the Public Sans files fail to load the app still renders with reasonable defaults.

## What to ship

Self-host the following WOFF2 files inside the Aquaplan Angular app's `src/assets/fonts/` (required for offline PWA):

| Family | Weights | Source |
|---|---|---|
| **Public Sans** | 400, 500, 600, 700 | https://fonts.google.com/specimen/Public+Sans |
| **JetBrains Mono** | 400, 500 | https://www.jetbrains.com/lp/mono/ — used only for sample codes, IDs, barcodes as text |
| **Material Icons Outlined** | — | https://fonts.google.com/icons — download the outlined ttf/woff2 |

Register them with `@font-face` inside `_fonts.scss` (new file) and `@use` it from `styles.scss` **before** any token use:

```scss
@font-face {
  font-family: "Roboto";
  src: url("/assets/fonts/Roboto-Regular.woff2") format("woff2");
  font-weight: 400;
  font-style: normal;
  font-display: swap;
}
/* …repeat for 500, 700, JetBrains Mono, Material Icons Outlined */
```

## Why not bundle the TTFs here?

This design system repo is a **specification + preview**, not a build artifact. The actual font binaries belong in the Angular app's `src/assets/fonts/`. Copying them here would create a second source of truth and invite drift.

## Migration note

The repo currently declares `font-family: Roboto, "Helvetica Neue", sans-serif` at the root. To align with this design system, update `styles.scss` to `"Public Sans", "Helvetica Neue", Arial, sans-serif` and self-host the Public Sans WOFF2 files as documented above.

JetBrains Mono is optional — if you want zero extra dependencies, drop `--font-family-mono` back to `"SF Mono", Consolas, monospace` and you lose nothing critical.
