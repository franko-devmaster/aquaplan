import { Before, After, BeforeAll, AfterAll, BeforeStep, Status } from '@cucumber/cucumber';
import { chromium, Browser } from '@playwright/test';
import { AquaPlanWorld } from './world.js';

let sharedBrowser: Browser | null = null;

BeforeAll(async function () {
  sharedBrowser = await chromium.launch({
    headless: process.env.HEADLESS !== 'false',
    slowMo: process.env.SLOW_MO ? parseInt(process.env.SLOW_MO, 10) : 0,
  });
});

AfterAll(async function () {
  if (sharedBrowser) {
    await sharedBrowser.close();
    sharedBrowser = null;
  }
});

Before({ tags: '@ui or @e2e' }, async function (this: AquaPlanWorld) {
  if (!sharedBrowser) {
    throw new Error('Browser not initialized');
  }
  this.browser = sharedBrowser;
  this.context = await this.browser.newContext({
    viewport: { width: 1280, height: 720 },
    locale: 'fr-CH',
  });
  this.page = await this.context.newPage();
});

After({ tags: '@ui or @e2e' }, async function (this: AquaPlanWorld, scenario) {
  // Take screenshot on failure
  if (scenario.result?.status === Status.FAILED && this.page) {
    const name = scenario.pickle.name.replace(/[^a-zA-Z0-9]/g, '_');
    const screenshotPath = `reports/screenshots/${name}.png`;
    await this.page.screenshot({ path: screenshotPath, fullPage: true });
    this.attach(await this.page.screenshot({ fullPage: true }), 'image/png');
  }

  if (this.context) {
    await this.context.close();
  }
});

Before({ tags: '@api or @e2e' }, async function (this: AquaPlanWorld) {
  // Reset API state before each API/E2E scenario
  this.accessToken = null;
  this.currentUserEmail = null;
  this.lastResponse = null;
  this.testData = {};
});
