import { Given, When, Then } from '@cucumber/cucumber';
import { expect } from '@playwright/test';
import { AquaPlanWorld } from '../support/world.js';

// ─── Navigation ─────────────────────────────────────────────

Given(
  'l\'utilisateur est sur la page {string}',
  async function (this: AquaPlanWorld, path: string) {
    await this.page.goto(`${this.baseUrl}${path}`, { waitUntil: 'domcontentloaded' });
    // Brief wait for Angular routing to settle (guards, redirects)
    await this.page.waitForTimeout(1000);
  }
);

When(
  'l\'utilisateur navigue vers {string}',
  async function (this: AquaPlanWorld, path: string) {
    await this.page.goto(`${this.baseUrl}${path}`, { waitUntil: 'domcontentloaded' });
    await this.page.waitForTimeout(1000);
  }
);

// ─── Clicks ─────────────────────────────────────────────────

When(
  'l\'utilisateur clique sur {string}',
  async function (this: AquaPlanWorld, text: string) {
    await this.page.getByRole('button', { name: text }).or(
      this.page.getByRole('link', { name: text })
    ).or(
      this.page.getByRole('menuitem', { name: text })
    ).or(
      this.page.getByText(text, { exact: false })
    ).first().click();
  }
);

When(
  'l\'utilisateur clique sur le bouton {string}',
  async function (this: AquaPlanWorld, text: string) {
    await this.page.getByRole('button', { name: text }).click();
  }
);

When(
  'l\'utilisateur clique sur le lien {string}',
  async function (this: AquaPlanWorld, text: string) {
    await this.page.getByRole('link', { name: text }).click();
  }
);

When(
  'l\'utilisateur clique sur le menu {string}',
  async function (this: AquaPlanWorld, text: string) {
    const menuItem = this.page.locator(`mat-nav-list a:has-text("${text}"), mat-list-item:has-text("${text}"), [routerLink]:has-text("${text}")`);
    await menuItem.first().click();
    await this.page.waitForLoadState('networkidle');
  }
);

// ─── Forms ──────────────────────────────────────────────────

When(
  'l\'utilisateur remplit le champ {string} avec {string}',
  async function (this: AquaPlanWorld, fieldLabel: string, value: string) {
    const field = this.page.getByLabel(fieldLabel).or(
      this.page.locator(`input[placeholder="${fieldLabel}"], input[formcontrolname="${fieldLabel}"], textarea[formcontrolname="${fieldLabel}"]`)
    );
    await field.first().fill(value);
  }
);

When(
  'l\'utilisateur selectionne {string} dans {string}',
  async function (this: AquaPlanWorld, option: string, selectLabel: string) {
    // Angular Material select
    const trigger = this.page.getByLabel(selectLabel).or(
      this.page.locator(`mat-select[formcontrolname="${selectLabel}"]`)
    );
    await trigger.first().click();
    await this.page.getByRole('option', { name: option }).click();
  }
);

When(
  'l\'utilisateur coche {string}',
  async function (this: AquaPlanWorld, label: string) {
    await this.page.getByLabel(label).check();
  }
);

When(
  'l\'utilisateur decoche {string}',
  async function (this: AquaPlanWorld, label: string) {
    await this.page.getByLabel(label).uncheck();
  }
);

When(
  'l\'utilisateur soumet le formulaire',
  async function (this: AquaPlanWorld) {
    await this.page.locator('button[type="submit"]').click();
  }
);

// ─── Page Content Assertions ────────────────────────────────

Then(
  'la page affiche {string}',
  async function (this: AquaPlanWorld, text: string) {
    await expect(this.page.getByText(text, { exact: false })).toBeVisible({ timeout: 10_000 });
  }
);

Then(
  'la page n\'affiche pas {string}',
  async function (this: AquaPlanWorld, text: string) {
    await expect(this.page.getByText(text, { exact: false })).not.toBeVisible({ timeout: 5_000 });
  }
);

Then(
  'le titre de la page est {string}',
  async function (this: AquaPlanWorld, title: string) {
    await expect(this.page.locator('h1, h2, .page-title, mat-card-title').first()).toContainText(title);
  }
);

Then(
  'un message de succes s\'affiche',
  async function (this: AquaPlanWorld) {
    await expect(
      this.page.locator('mat-snack-bar-container, .mat-mdc-snack-bar-container, .alert-success').first()
    ).toBeVisible({ timeout: 10_000 });
  }
);

Then(
  'un message d\'erreur s\'affiche',
  async function (this: AquaPlanWorld) {
    await expect(
      this.page.locator('mat-snack-bar-container, .mat-mdc-snack-bar-container, .alert-danger, mat-error').first()
    ).toBeVisible({ timeout: 10_000 });
  }
);

Then(
  'un message {string} s\'affiche',
  async function (this: AquaPlanWorld, text: string) {
    await expect(
      this.page.locator(`mat-snack-bar-container:has-text("${text}"), .alert:has-text("${text}")`)
    ).toBeVisible({ timeout: 10_000 });
  }
);

// ─── Table Assertions ───────────────────────────────────────

Then(
  'le tableau contient {int} lignes',
  async function (this: AquaPlanWorld, expectedCount: number) {
    const rows = this.page.locator('mat-table mat-row, table tbody tr');
    await expect(rows).toHaveCount(expectedCount, { timeout: 10_000 });
  }
);

Then(
  'le tableau contient au moins {int} lignes',
  async function (this: AquaPlanWorld, minCount: number) {
    const rows = this.page.locator('mat-table mat-row, table tbody tr');
    const count = await rows.count();
    expect(count).toBeGreaterThanOrEqual(minCount);
  }
);

Then(
  'le tableau contient une ligne avec {string}',
  async function (this: AquaPlanWorld, text: string) {
    const row = this.page.locator(`mat-row:has-text("${text}"), tr:has-text("${text}")`);
    await expect(row.first()).toBeVisible({ timeout: 10_000 });
  }
);

// ─── Element State ──────────────────────────────────────────

Then(
  'le bouton {string} est visible',
  async function (this: AquaPlanWorld, text: string) {
    await expect(this.page.getByRole('button', { name: text })).toBeVisible();
  }
);

Then(
  'le bouton {string} est desactive',
  async function (this: AquaPlanWorld, text: string) {
    await expect(this.page.getByRole('button', { name: text })).toBeDisabled();
  }
);

Then(
  'le bouton {string} n\'est pas visible',
  async function (this: AquaPlanWorld, text: string) {
    await expect(this.page.getByRole('button', { name: text })).not.toBeVisible();
  }
);

Then(
  'le menu {string} est visible',
  async function (this: AquaPlanWorld, text: string) {
    const menu = this.page.locator(`mat-nav-list a:has-text("${text}"), mat-list-item:has-text("${text}")`);
    await expect(menu.first()).toBeVisible();
  }
);

Then(
  'le menu {string} n\'est pas visible',
  async function (this: AquaPlanWorld, text: string) {
    const menu = this.page.locator(`mat-nav-list a:has-text("${text}"), mat-list-item:has-text("${text}")`);
    await expect(menu.first()).not.toBeVisible({ timeout: 3_000 });
  }
);

// ─── Wait ───────────────────────────────────────────────────

When(
  'l\'utilisateur attend {int} secondes',
  async function (this: AquaPlanWorld, seconds: number) {
    await this.page.waitForTimeout(seconds * 1000);
  }
);

When(
  'la page est chargee',
  async function (this: AquaPlanWorld) {
    await this.page.waitForLoadState('networkidle');
  }
);
