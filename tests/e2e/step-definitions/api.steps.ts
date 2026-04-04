import { When, Then } from '@cucumber/cucumber';
import { expect } from '@playwright/test';
import { AquaPlanWorld } from '../support/world.js';

// ─── API Requests ───────────────────────────────────────────

When(
  'une requete GET est envoyee a {string}',
  async function (this: AquaPlanWorld, path: string) {
    await this.apiRequest('GET', path);
  }
);

When(
  'une requete POST est envoyee a {string} avec:',
  async function (this: AquaPlanWorld, path: string, docString: string) {
    const body = JSON.parse(docString);
    await this.apiRequest('POST', path, body);
  }
);

When(
  'une requete PUT est envoyee a {string} avec:',
  async function (this: AquaPlanWorld, path: string, docString: string) {
    const body = JSON.parse(docString);
    await this.apiRequest('PUT', path, body);
  }
);

When(
  'une requete PATCH est envoyee a {string} avec:',
  async function (this: AquaPlanWorld, path: string, docString: string) {
    const body = JSON.parse(docString);
    await this.apiRequest('PATCH', path, body);
  }
);

When(
  'une requete DELETE est envoyee a {string}',
  async function (this: AquaPlanWorld, path: string) {
    await this.apiRequest('DELETE', path);
  }
);

// ─── Response Assertions ────────────────────────────────────

Then(
  'le code de reponse est {int}',
  async function (this: AquaPlanWorld, expectedStatus: number) {
    expect(this.lastResponse).not.toBeNull();
    expect(this.lastResponse!.status).toBe(expectedStatus);
  }
);

Then(
  'la reponse contient {int} elements',
  async function (this: AquaPlanWorld, expectedCount: number) {
    expect(this.lastResponse).not.toBeNull();
    const body = this.lastResponse!.body;
    expect(Array.isArray(body)).toBeTruthy();
    expect((body as unknown[]).length).toBe(expectedCount);
  }
);

Then(
  'la reponse contient au moins {int} elements',
  async function (this: AquaPlanWorld, minCount: number) {
    expect(this.lastResponse).not.toBeNull();
    const body = this.lastResponse!.body;
    expect(Array.isArray(body)).toBeTruthy();
    expect((body as unknown[]).length).toBeGreaterThanOrEqual(minCount);
  }
);

Then(
  'la reponse contient le champ {string} avec la valeur {string}',
  async function (this: AquaPlanWorld, field: string, expectedValue: string) {
    expect(this.lastResponse).not.toBeNull();
    const body = this.lastResponse!.body as Record<string, unknown>;
    expect(String(body[field])).toBe(expectedValue);
  }
);

Then(
  'la reponse contient le champ {string}',
  async function (this: AquaPlanWorld, field: string) {
    expect(this.lastResponse).not.toBeNull();
    const body = this.lastResponse!.body as Record<string, unknown>;
    expect(body).toHaveProperty(field);
  }
);

Then(
  'la reponse est un objet avec les proprietes:',
  async function (this: AquaPlanWorld, dataTable: { rawTable: string[][] }) {
    expect(this.lastResponse).not.toBeNull();
    const body = this.lastResponse!.body as Record<string, unknown>;
    for (const [key, value] of dataTable.rawTable) {
      expect(String(body[key])).toBe(value);
    }
  }
);

Then(
  'chaque element contient le champ {string}',
  async function (this: AquaPlanWorld, field: string) {
    expect(this.lastResponse).not.toBeNull();
    const body = this.lastResponse!.body as Record<string, unknown>[];
    expect(Array.isArray(body)).toBeTruthy();
    for (const item of body) {
      expect(item).toHaveProperty(field);
    }
  }
);
