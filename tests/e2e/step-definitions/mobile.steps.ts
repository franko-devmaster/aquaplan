import { Given, When, Then } from '@cucumber/cucumber';
import { expect } from '@playwright/test';
import { AquaPlanWorld } from '../support/world.js';

// ─── Mobile / responsive steps ──────────────────────────────
// These steps detect a subset of layout defects that functional assertions miss:
// horizontal page overflow and controls whose content overflows their box
// (e.g. button labels longer than the button on a narrow viewport).

/** Capture a named screenshot (used for the manual UX/UI audit). */
Then(/^je capture l'écran "(.+)"$/, async function (this: AquaPlanWorld, name: string) {
  if (!this.page) { return 'pending'; }
  const safe = name.replace(/[^a-zA-Z0-9]/g, '_');
  await this.page.screenshot({ path: `reports/screenshots/audit_${safe}.png`, fullPage: true });
});

/** The page must not scroll horizontally (classic responsive defect). */
Then('la page ne déborde pas horizontalement', async function (this: AquaPlanWorld) {
  if (!this.page) { return 'pending'; }
  await this.page.waitForTimeout(500);
  const overflow = await this.page.evaluate(() => {
    const el = document.scrollingElement || document.documentElement;
    return { scroll: el.scrollWidth, client: el.clientWidth };
  });
  // Tolerate a 2px rounding margin.
  expect(
    overflow.scroll,
    `Débordement horizontal: scrollWidth=${overflow.scroll} > clientWidth=${overflow.client}`
  ).toBeLessThanOrEqual(overflow.client + 2);
});

/** No interactive control's content should overflow its own box. */
Then('aucun contrôle ne déborde de sa taille', async function (this: AquaPlanWorld) {
  if (!this.page) { return 'pending'; }
  await this.page.waitForTimeout(500);
  const offenders = await this.page.evaluate(() => {
    // Labelled buttons only — icon-only buttons (mat-icon-button) report a few px of
    // scroll overflow from the icon font/ripple metrics, which is not a real defect.
    const sel = 'button, a[mat-button], a[mat-raised-button], .mat-mdc-button, .mat-mdc-raised-button, [role="button"]';
    const TOL = 8; // px — only flag overflow a user would actually see
    const bad: string[] = [];
    document.querySelectorAll(sel).forEach((el) => {
      const h = el as HTMLElement;
      if (h.offsetParent === null) return; // not visible
      const cls = h.className || '';
      if (/icon-button/.test(cls)) return; // icon-only control
      const text = (h.innerText || h.getAttribute('aria-label') || '').trim();
      if (!text || text.length < 2) return; // no real label
      const overflowX = h.scrollWidth - h.clientWidth;
      const overflowY = h.scrollHeight - h.clientHeight;
      if (overflowX > TOL || overflowY > TOL) {
        bad.push(`"${text.slice(0, 40)}" (+${overflowX}x/${overflowY}y)`);
      }
    });
    return bad;
  });
  expect(offenders, `Contrôles dont le contenu déborde:\n  ${offenders.join('\n  ')}`).toHaveLength(0);
});

/** Open the mobile navigation (hamburger) if the sidebar is collapsed. */
When('j\'ouvre le menu de navigation', async function (this: AquaPlanWorld) {
  if (!this.page) { return 'pending'; }
  const toggle = this.page.getByRole('button', { name: /menu|navigation/i })
    .or(this.page.locator('button:has(mat-icon:text("menu"))'))
    .or(this.page.locator('[aria-label*="menu" i]'));
  if (await toggle.first().isVisible().catch(() => false)) {
    await toggle.first().click();
    await this.page.waitForTimeout(500);
  }
});
