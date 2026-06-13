# Aquaplan Web App — UI kit

The Aquaplan Angular 19 PWA is the **only product** — it serves desktop, iPad, and mobile from a single responsive codebase. This folder is a React-based visual recreation of its key screens, built for design exploration. **It is not production code** and does not replace the Angular implementation.

## Structure

| File | Purpose |
|---|---|
| `index.html` | Interactive shell that stitches the components into a click-through prototype (login → orders → results; rounds view; dialog flow) |
| `Primitives.jsx` | `StatusChip`, `Button`, `Field`, `Card`, `PageHeader`, `SectionLabel` — shared building blocks |
| `Layout.jsx` | `Sidebar`, `TopHeader`, `Shell` — app chrome |
| `LoginScreen.jsx` | Login card, 400 px, SSO button, water-pattern bg |
| `OrderListPage.jsx` | The quintessential screen: filters row + data table + pagination |
| `OrderCreateDialog.jsx` | "Formulaire dans dialog" pattern including the round-section conditional block |
| `RoundDetailPage.jsx` | Field-sampling tournée view with offline banner and scan CTA |
| `ResultsPage.jsx` | Analysis results table with synthesis tiles |

## Conventions followed from the context doc

- **Inline styles on every component** — tokens consumed as `var(--color-…)`.
- **Page header pattern** (`PageHeader`) — title left, action right, bottom border.
- **Filters row pattern** — search field + pill filters above the table.
- **Status chip pattern** — one `<StatusChip status="...">` with a central `STATUS_MAP`.
- **Round-section pattern** — bordered framed block with small uppercase label, revealed conditionally (`scheduleMode === "scheduled"`).
- **Dialog pattern** — title + content + actions row (right-aligned, text + primary).
- **Login fullscreen on mobile** — inherited from `.login-card` responsive rule (not demoed at mobile width here, but mirrored structurally).

## Known gaps / intentional simplifications

- **Mobile card layout** (`.responsive-table-container`) is not replicated — would duplicate structure. The desktop table is correct; trust the existing repo CSS for the mobile transform.
- **Barcode scanner** (`@zxing`) — not mocked. The "Scanner un échantillon" button is a stub.
- **i18n toggle** — we only show FR strings. German / English exist in the real app and labels must tolerate +30% length.
- **Real data**: all names and codes are plausible Canton-de-Fribourg placeholders — swap for real samples when integrating.

## ⚠️ Fidelity caveat

This kit was built **without access to the Aquaplan codebase or Figma**. It is a faithful projection of the patterns described in `uploads/aquaplan-design-context.md`. Expect adjustments once you attach:

- `status-chip.component.ts` → confirm status ids match the metier enum
- `header.component.ts` + `sidebar.component.ts` + `layout.component.ts` → verify chrome structure
- `styles.scss` → verify we haven't missed a global utility
- 2-3 representative screenshots → sanity-check the "vibe"
