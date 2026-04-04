#!/usr/bin/env node
/**
 * Import Cucumber JSON results into Xray Cloud Test Execution
 *
 * Usage:
 *   node scripts/xray-import.mjs <test-execution-key>
 *   node scripts/xray-import.mjs AQ-192
 *
 * Reads reports/cucumber-results.json and imports into Xray.
 * Maps feature file names (AQ-xxx.feature) to test issue keys.
 */

import { readFileSync, existsSync } from 'fs';
import { resolve, dirname } from 'path';
import { fileURLToPath } from 'url';
import { config } from 'dotenv';

const __dirname = dirname(fileURLToPath(import.meta.url));
const projectRoot = resolve(__dirname, '..');

// Load .env from AquaPlan root
config({ path: resolve(projectRoot, '../../.env') });

const XRAY_API = 'https://xray.cloud.getxray.app/api/v2';

async function getXrayToken() {
  const clientId = process.env.XRAY_CLIENT_ID;
  const clientSecret = process.env.XRAY_CLIENT_SECRET;

  if (!clientId || !clientSecret) {
    throw new Error('XRAY_CLIENT_ID and XRAY_CLIENT_SECRET must be set in .env');
  }

  const response = await fetch(`${XRAY_API}/authenticate`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ client_id: clientId, client_secret: clientSecret }),
  });

  if (!response.ok) {
    throw new Error(`Xray auth failed: ${response.status}`);
  }

  return (await response.text()).replace(/"/g, '');
}

function loadResults() {
  const resultsPath = resolve(projectRoot, 'reports', 'cucumber-results.json');

  if (!existsSync(resultsPath)) {
    throw new Error(`Results file not found: ${resultsPath}\nRun 'npm test' first.`);
  }

  return JSON.parse(readFileSync(resultsPath, 'utf-8'));
}

function mapResultsToXray(cucumberResults, executionKey) {
  const tests = [];

  for (const feature of cucumberResults) {
    for (const scenario of feature.elements || []) {
      // Extract test key from tags (@AQ-xxx)
      const testKeyTag = (scenario.tags || []).find(
        (t) => /^@AQ-\d+$/.test(t.name)
      );

      if (!testKeyTag) {
        console.warn(`  SKIP scenario "${scenario.name}" — no @AQ-xxx tag found`);
        continue;
      }

      const testKey = testKeyTag.name.replace('@', '');

      // Determine status from step results
      const steps = scenario.steps || [];
      const allPassed = steps.every((s) => s.result?.status === 'passed');
      const anyFailed = steps.some((s) => s.result?.status === 'failed');
      const status = anyFailed ? 'FAILED' : allPassed ? 'PASSED' : 'TO DO';

      // Build step-by-step comment
      const comment = steps
        .map((s) => {
          const icon = s.result?.status === 'passed' ? '✅' : s.result?.status === 'failed' ? '❌' : '⏭️';
          const duration = s.result?.duration
            ? ` (${(s.result.duration / 1_000_000_000).toFixed(2)}s)`
            : '';
          const error = s.result?.error_message ? `\n    Error: ${s.result.error_message.split('\n')[0]}` : '';
          return `${icon} ${s.keyword.trim()} ${s.name}${duration}${error}`;
        })
        .join('\n');

      tests.push({ testKey, status, comment });
    }
  }

  return { testExecutionKey: executionKey, tests };
}

async function importToXray(xrayToken, payload) {
  const response = await fetch(`${XRAY_API}/import/execution`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      Authorization: `Bearer ${xrayToken}`,
    },
    body: JSON.stringify(payload),
  });

  if (!response.ok) {
    const text = await response.text();
    throw new Error(`Xray import failed: ${response.status} ${text}`);
  }

  return await response.json();
}

function printSummary(payload) {
  const passed = payload.tests.filter((t) => t.status === 'PASSED').length;
  const failed = payload.tests.filter((t) => t.status === 'FAILED').length;
  const todo = payload.tests.filter((t) => t.status === 'TO DO').length;
  const total = payload.tests.length;

  console.log('\n=== Import Summary ===');
  console.log(`Execution: ${payload.testExecutionKey}`);
  console.log(`Total:     ${total}`);
  console.log(`PASSED:    ${passed}`);
  console.log(`FAILED:    ${failed}`);
  console.log(`TO DO:     ${todo}`);
  console.log(`Rate:      ${total > 0 ? ((passed / total) * 100).toFixed(1) : 0}%`);

  if (failed > 0) {
    console.log('\nFailed tests:');
    for (const t of payload.tests.filter((t) => t.status === 'FAILED')) {
      console.log(`  - ${t.testKey}`);
    }
  }
}

// ─── Main ───────────────────────────────────────────────────

const executionKey = process.argv[2];
if (!executionKey) {
  console.error('Usage: node scripts/xray-import.mjs <test-execution-key>');
  console.error('Example: node scripts/xray-import.mjs AQ-192');
  process.exit(1);
}

console.log(`Importing results into Test Execution ${executionKey}...`);

try {
  const cucumberResults = loadResults();
  console.log(`Loaded ${cucumberResults.length} feature results`);

  const payload = mapResultsToXray(cucumberResults, executionKey);
  console.log(`Mapped ${payload.tests.length} test results`);

  if (payload.tests.length === 0) {
    console.warn('No test results to import. Check that feature files have @AQ-xxx tags.');
    process.exit(0);
  }

  printSummary(payload);

  const token = await getXrayToken();
  const result = await importToXray(token, payload);
  console.log('\nImport successful:', JSON.stringify(result));
} catch (error) {
  console.error('Import failed:', error.message);
  process.exit(1);
}
