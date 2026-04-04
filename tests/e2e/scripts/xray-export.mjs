#!/usr/bin/env node
/**
 * Export Gherkin feature files from Xray Cloud Test Execution
 *
 * Usage:
 *   node scripts/xray-export.mjs <test-execution-key>
 *   node scripts/xray-export.mjs AQ-192
 *
 * Exports all Gherkin scenarios from the given Test Execution
 * into individual .feature files in features/xray/
 */

import { writeFileSync, mkdirSync } from 'fs';
import { resolve, dirname } from 'path';
import { fileURLToPath } from 'url';
import { config } from 'dotenv';

const __dirname = dirname(fileURLToPath(import.meta.url));
const projectRoot = resolve(__dirname, '..');

// Load .env from project root (two levels up)
config({ path: resolve(projectRoot, '../../.env') });

const XRAY_API = 'https://xray.cloud.getxray.app/api/v2';
const JIRA_API = 'https://chfr.atlassian.net/rest/api/3';

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

async function getJiraIssueId(issueKey) {
  const email = process.env.JIRA_USER_EMAIL;
  const token = process.env.JIRA_API_TOKEN;
  const auth = Buffer.from(`${email}:${token}`).toString('base64');

  const response = await fetch(`${JIRA_API}/issue/${issueKey}?fields=summary`, {
    headers: { Authorization: `Basic ${auth}` },
  });

  if (!response.ok) {
    throw new Error(`Failed to get Jira issue ${issueKey}: ${response.status}`);
  }

  const data = await response.json();
  return data.id; // Jira internal numeric ID
}

async function getTestsFromExecution(xrayToken, executionKey) {
  // First get the Jira internal ID
  const jiraId = await getJiraIssueId(executionKey);

  const query = `{
    getTestExecution(issueId: "${jiraId}") {
      issueId
      testRuns(limit: 100) {
        results {
          id
          status { name }
          test {
            issueId
            jira(fields: ["key", "summary"])
            testType { name }
            gherkin
          }
        }
      }
    }
  }`;

  const response = await fetch(`${XRAY_API}/graphql`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      Authorization: `Bearer ${xrayToken}`,
    },
    body: JSON.stringify({ query }),
  });

  if (!response.ok) {
    throw new Error(`Xray GraphQL failed: ${response.status}`);
  }

  const data = await response.json();

  if (data.errors) {
    throw new Error(`Xray GraphQL errors: ${JSON.stringify(data.errors)}`);
  }

  return data.data.getTestExecution.testRuns.results;
}

function writeFeatureFiles(testRuns, executionKey) {
  const outDir = resolve(projectRoot, 'features', 'xray');
  mkdirSync(outDir, { recursive: true });

  let exportedCount = 0;

  for (const run of testRuns) {
    const test = run.test;
    if (!test.gherkin) {
      console.warn(`  SKIP ${test.jira?.key || test.issueId} — no Gherkin defined`);
      continue;
    }

    const key = test.jira?.key || `test-${test.issueId}`;
    const summary = test.jira?.summary || 'Untitled test';
    const filename = `${key}.feature`;
    const filepath = resolve(outDir, filename);

    // Check if Gherkin already has Feature: header
    let content = test.gherkin;
    if (!content.trim().startsWith('Feature:')) {
      content = `Feature: ${summary}\n\n  ${content}`;
    }

    // Add tags for the test key and execution
    if (!content.includes(`@${key}`)) {
      content = `@${key} @${executionKey} @regression\n${content}`;
    }

    writeFileSync(filepath, content, 'utf-8');
    console.log(`  EXPORTED ${key} → ${filename} (status: ${run.status?.name || 'unknown'})`);
    exportedCount++;
  }

  return exportedCount;
}

// ─── Main ───────────────────────────────────────────────────

const executionKey = process.argv[2];
if (!executionKey) {
  console.error('Usage: node scripts/xray-export.mjs <test-execution-key>');
  console.error('Example: node scripts/xray-export.mjs AQ-192');
  process.exit(1);
}

console.log(`Exporting Gherkin from Test Execution ${executionKey}...`);

try {
  const token = await getXrayToken();
  console.log('Xray authentication OK');

  const testRuns = await getTestsFromExecution(token, executionKey);
  console.log(`Found ${testRuns.length} test runs`);

  const count = writeFeatureFiles(testRuns, executionKey);
  console.log(`\nExported ${count} feature files to features/xray/`);
} catch (error) {
  console.error('Export failed:', error.message);
  process.exit(1);
}
