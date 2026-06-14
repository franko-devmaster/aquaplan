import { Before, After, BeforeAll, AfterAll, Status } from '@cucumber/cucumber';
import { chromium, devices, Browser } from '@playwright/test';
import { AquaPlanWorld } from './world.js';

let sharedBrowser: Browser | null = null;

async function getOrLaunchBrowser(): Promise<Browser> {
  if (!sharedBrowser) {
    sharedBrowser = await chromium.launch({
      headless: process.env.HEADLESS !== 'false',
      slowMo: process.env.SLOW_MO ? parseInt(process.env.SLOW_MO, 10) : 0,
    });
  }
  return sharedBrowser;
}

AfterAll(async function () {
  if (sharedBrowser) {
    await sharedBrowser.close();
    sharedBrowser = null;
  }
});

// Desktop UI scenarios (everything @ui/@e2e that is NOT explicitly @mobile).
Before({ tags: '(@ui or @e2e) and not @mobile' }, async function (this: AquaPlanWorld) {
  this.browser = await getOrLaunchBrowser();
  this.context = await this.browser.newContext({
    viewport: { width: 1280, height: 720 },
    locale: 'fr-CH',
  });
  this.page = await this.context.newPage();
  this.viewportLabel = 'desktop';
});

// Mobile scenarios — emulate a phone (Pixel 5: 393x851, touch, mobile UA, DPR 3).
// Overridable via MOBILE_DEVICE env (any Playwright device name).
Before({ tags: '@mobile' }, async function (this: AquaPlanWorld) {
  this.browser = await getOrLaunchBrowser();
  const deviceName = process.env.MOBILE_DEVICE || 'Pixel 5';
  const device = devices[deviceName] ?? devices['Pixel 5'];
  this.context = await this.browser.newContext({
    ...device,
    locale: 'fr-CH',
  });
  this.page = await this.context.newPage();
  this.viewportLabel = `mobile:${deviceName}`;
});

After({ tags: '@ui or @e2e or @mobile' }, async function (this: AquaPlanWorld, scenario) {
  // Take screenshot on failure
  if (scenario.result?.status === Status.FAILED && this.page) {
    const name = scenario.pickle.name.replace(/[^a-zA-Z0-9]/g, '_');
    const suffix = this.viewportLabel?.startsWith('mobile') ? '_mobile' : '';
    const screenshotPath = `reports/screenshots/${name}${suffix}.png`;
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
