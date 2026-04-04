import { Given, Then } from '@cucumber/cucumber';
import { expect } from '@playwright/test';
import { AquaPlanWorld } from '../support/world.js';

// ─── Smoke Test Steps ───────────────────────────────────────

Given(
  'l\'API est accessible',
  async function (this: AquaPlanWorld) {
    const response = await fetch(`${this.apiUrl}/api/health`);
    expect(response.status).toBe(200);
  }
);

Given(
  'le frontend est accessible',
  async function (this: AquaPlanWorld) {
    const response = await fetch(this.baseUrl);
    expect(response.ok).toBeTruthy();
  }
);

Then(
  'le endpoint {string} retourne {int}',
  async function (this: AquaPlanWorld, path: string, expectedStatus: number) {
    const response = await this.apiRequest('GET', path);
    expect(response.status).toBe(expectedStatus);
  }
);

Then(
  'le login avec {string} et {string} retourne un token',
  async function (this: AquaPlanWorld, email: string, password: string) {
    const token = await this.apiLogin(email, password);
    expect(token).toBeTruthy();
    expect(token.length).toBeGreaterThan(10);
  }
);
