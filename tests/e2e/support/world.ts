import { World, IWorldOptions, setDefaultTimeout, setWorldConstructor } from '@cucumber/cucumber';
import { Browser, BrowserContext, Page, chromium } from '@playwright/test';

// 30 seconds per step
setDefaultTimeout(30_000);

export interface AquaPlanWorldParams {
  baseUrl: string;
  apiUrl: string;
}

export class AquaPlanWorld extends World<AquaPlanWorldParams> {
  browser!: Browser;
  context!: BrowserContext;
  page!: Page;

  // Auth state
  accessToken: string | null = null;
  currentUserEmail: string | null = null;

  // API response state (for API tests)
  lastResponse: {
    status: number;
    body: unknown;
    headers: Record<string, string>;
  } | null = null;

  // Shared test data
  testData: Record<string, unknown> = {};

  constructor(options: IWorldOptions<AquaPlanWorldParams>) {
    super(options);
  }

  get baseUrl(): string {
    return this.parameters.baseUrl;
  }

  get apiUrl(): string {
    return this.parameters.apiUrl;
  }

  /** Perform API login and store the access token */
  async apiLogin(email: string, password: string): Promise<string> {
    const response = await fetch(`${this.apiUrl}/api/auth/login`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ email, password }),
    });

    if (!response.ok) {
      throw new Error(`Login failed for ${email}: ${response.status} ${response.statusText}`);
    }

    const data = (await response.json()) as { accessToken: string; refreshToken: string };
    this.accessToken = data.accessToken;
    this.currentUserEmail = email;
    return data.accessToken;
  }

  /** Make an authenticated API request */
  async apiRequest(
    method: string,
    path: string,
    body?: unknown
  ): Promise<{ status: number; body: unknown; headers: Record<string, string> }> {
    const headers: Record<string, string> = {
      'Content-Type': 'application/json',
    };
    if (this.accessToken) {
      headers['Authorization'] = `Bearer ${this.accessToken}`;
    }

    const response = await fetch(`${this.apiUrl}${path}`, {
      method,
      headers,
      body: body ? JSON.stringify(body) : undefined,
    });

    let responseBody: unknown;
    const contentType = response.headers.get('content-type');
    if (contentType?.includes('application/json')) {
      responseBody = await response.json();
    } else {
      responseBody = await response.text();
    }

    const responseHeaders: Record<string, string> = {};
    response.headers.forEach((value, key) => {
      responseHeaders[key] = value;
    });

    this.lastResponse = {
      status: response.status,
      body: responseBody,
      headers: responseHeaders,
    };

    return this.lastResponse;
  }

  /** Set the access token in the browser's sessionStorage (for UI tests) */
  async setBrowserAuth(): Promise<void> {
    if (!this.accessToken || !this.page) {
      return;
    }
    await this.page.goto(this.baseUrl);
    await this.page.evaluate((token: string) => {
      sessionStorage.setItem('access_token', token);
    }, this.accessToken);
  }
}

setWorldConstructor(AquaPlanWorld);
