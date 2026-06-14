import { Given, When, Then } from '@cucumber/cucumber';
import { expect } from '@playwright/test';
import { AquaPlanWorld } from '../support/world.js';

// ─── AQ — Order creation + add-to-round (regression for the [property:] 500) ──

/** Resolve a round, a matching sampling location and a program, then create an order. */
async function createOrderForFirstRound(this: AquaPlanWorld): Promise<void> {
  const roundsResp = await this.apiRequest('GET', '/api/sampling-rounds');
  const rounds = roundsResp.body as Record<string, unknown>[];
  const list = Array.isArray(rounds) ? rounds : ((roundsResp.body as Record<string, unknown>)['items'] as Record<string, unknown>[]);
  const roundId = list[0]['id'] as string;
  this.testData['roundId'] = roundId;

  const detail = await this.apiRequest('GET', `/api/sampling-rounds/${roundId}`);
  const distributorId = (detail.body as Record<string, unknown>)['distributorId'] as string;

  const slResp = await this.apiRequest('GET', '/api/sampling-locations');
  const sls = slResp.body as Record<string, unknown>[];
  const sl = sls.find(x => x['distributorId'] === distributorId) ?? sls.find(x => x['distributorId']);

  const progResp = await this.apiRequest('GET', '/api/analysis-programs');
  const progs = progResp.body as Record<string, unknown>[];
  const progList = Array.isArray(progs) ? progs : ((progResp.body as Record<string, unknown>)['items'] as Record<string, unknown>[]);

  await this.apiRequest('POST', '/api/orders', {
    distributorId,
    samplingLocationId: sl?.['id'] ?? null,
    preleveurId: null,
    plannedDate: null,
    analysisProgramIds: [progList[0]['id']],
    notes: 'e2e order-create regression',
    isUnplanned: false,
    unplannedReason: null,
  });
  if (this.lastResponse?.status === 201) {
    this.testData['orderId'] = (this.lastResponse.body as Record<string, unknown>)['id'];
  }
}

When('il crée un mandat pour un lieu et un programme valides', createOrderForFirstRound);
When('il crée un mandat pour la tournée existante', createOrderForFirstRound);

When('il crée un mandat sans distributeur', async function (this: AquaPlanWorld) {
  await this.apiRequest('POST', '/api/orders', { isUnplanned: false });
});

When('il ajoute ce mandat à la tournée', async function (this: AquaPlanWorld) {
  const roundId = this.testData['roundId'] as string;
  const orderId = this.testData['orderId'] as string;
  await this.apiRequest('POST', `/api/sampling-rounds/${roundId}/orders`, { orderId });
});

Then('le mandat créé porte un numéro', async function (this: AquaPlanWorld) {
  expect(this.lastResponse).not.toBeNull();
  expect(this.lastResponse!.status).toBe(201);
  const body = this.lastResponse!.body as Record<string, unknown>;
  expect(body['orderNumber']).toBeTruthy();
});

Then('la tournée contient le mandat', async function (this: AquaPlanWorld) {
  expect(this.lastResponse).not.toBeNull();
  expect(this.lastResponse!.status).toBe(200);
  const body = this.lastResponse!.body as Record<string, unknown>;
  const orders = (body['orders'] as Record<string, unknown>[]) ?? [];
  expect(orders.some(o => o['id'] === this.testData['orderId'])).toBeTruthy();
});
