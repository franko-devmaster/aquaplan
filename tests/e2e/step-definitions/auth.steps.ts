import { Given, When, Then } from '@cucumber/cucumber';
import { expect } from '@playwright/test';
import { AquaPlanWorld } from '../support/world.js';
import { getCredentials } from '../support/credentials.js';

// ─── API Auth ───────────────────────────────────────────────

Given(
  'l\'utilisateur est authentifie en tant que {string}',
  async function (this: AquaPlanWorld, role: string) {
    const { email, password } = getCredentials(role);
    await this.apiLogin(email, password);
  }
);

Given(
  'l\'utilisateur est authentifie avec {string} et {string}',
  async function (this: AquaPlanWorld, email: string, password: string) {
    await this.apiLogin(email, password);
  }
);

Given(
  'l\'utilisateur n\'est pas authentifie',
  async function (this: AquaPlanWorld) {
    this.accessToken = null;
    this.currentUserEmail = null;
  }
);

// ─── UI Auth ────────────────────────────────────────────────

Given(
  'l\'utilisateur est connecte en tant que {string}',
  async function (this: AquaPlanWorld, role: string) {
    const { email, password } = getCredentials(role);
    // Login via API first
    await this.apiLogin(email, password);
    // Set token in browser
    await this.setBrowserAuth();
  }
);

When(
  'l\'utilisateur se connecte avec {string} et {string}',
  async function (this: AquaPlanWorld, email: string, password: string) {
    await this.page.goto(`${this.baseUrl}/login`);
    await this.page.fill('input[formcontrolname="email"], input[type="email"]', email);
    await this.page.fill('input[formcontrolname="password"], input[type="password"]', password);
    await this.page.click('button[type="submit"]');
    await this.page.waitForURL(/.*(?!.*login)/, { timeout: 10_000 });
  }
);

When(
  'l\'utilisateur se deconnecte',
  async function (this: AquaPlanWorld) {
    // Try UI logout button first
    const logoutBtn = this.page.locator('button:has-text("Déconnexion"), [data-testid="logout"]');
    if (await logoutBtn.isVisible({ timeout: 2000 }).catch(() => false)) {
      await logoutBtn.click();
    } else {
      // Fallback: clear session
      await this.page.evaluate(() => {
        sessionStorage.clear();
      });
      await this.page.goto(`${this.baseUrl}/login`);
    }
  }
);

Then(
  'l\'utilisateur est redirige vers la page de connexion',
  async function (this: AquaPlanWorld) {
    await this.page.waitForURL(/.*\/login/, { timeout: 10_000 });
    expect(this.page.url()).toContain('/login');
  }
);

Then(
  'l\'utilisateur est redirige vers {string}',
  async function (this: AquaPlanWorld, expectedPath: string) {
    await this.page.waitForURL(`**${expectedPath}`, { timeout: 10_000 });
    expect(this.page.url()).toContain(expectedPath);
  }
);
