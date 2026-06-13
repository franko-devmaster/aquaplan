# Iconography assets

## Current state (per the context doc)

- The Aquaplan codebase uses **Material Icons exclusively** via `<mat-icon>` (Angular Material).
- The icon font is loaded from Google Fonts in the default Material setup.
- No custom icon sprite, no SVG set, no other icon library is present.

## What this folder contains

Nothing yet beyond this README. We intentionally did **not** copy the Material Icons font file here because:

1. We don't have the Aquaplan codebase attached to inspect which exact subset is used.
2. The Material Icons ttf is ~140 KB and should live in the Angular app's `src/assets/fonts/`, not in the design-system repo.
3. Previewing Material Icons in this design system is handled via the Google Fonts CDN inside the preview/ card HTML files — this is acceptable for previews, but **not for production**, where offline support is mandatory.

## Production recommendation

Inside the Aquaplan Angular app:

1. Download `Material Icons Outlined` (ttf + woff2) from https://fonts.google.com/icons
2. Drop into `src/assets/fonts/`
3. Register with `@font-face` in a shared SCSS partial loaded from `styles.scss`
4. Remove the Google Fonts `<link>` tag from `index.html`
5. Verify offline: Chrome DevTools → Network → Offline → reload, icons should still render

## Usage rules

- **Outlined variant** is the default, for every icon except inside a filled chip (status icons embedded in `<app-status-chip>`).
- **Never mix weights** within the same surface (e.g. don't put outlined next to filled in the same toolbar).
- **Color = `currentColor`** — icons inherit from text color. Explicit fills only when the icon carries a status meaning, in which case use the matching chip foreground token (`--chip-*-fg`).
- **Sizes**: 16 / 20 / 24 / 32. Stick to these.
- **No emoji**, no unicode pictograms, no hand-rolled SVG icons. Scientific symbols like `°`, `µ`, `±`, `≤` are fine inside scientific text content, never as standalone UI icons.

## Possible future custom set

If the team ever wants LIMS-specific icons (bottle, pipette, pH meter, chlorine, barcode-scanned sample), commission a small outlined SVG set on a 24×24 grid, 2 px stroke — matches Material Icons' optical weight. Ship inline as SVG, not a font.
