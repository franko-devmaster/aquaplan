// Cucumber.js configuration
// https://github.com/cucumber/cucumber-js/blob/main/docs/configuration.md

export default {
  // Feature files location
  paths: ['features/**/*.feature'],

  // Step definitions + support files — use require with tsx for TypeScript
  require: [
    'step-definitions/**/*.ts',
    'support/**/*.ts',
  ],

  // Use tsx for TypeScript transpilation
  requireModule: ['tsx'],

  // Output formatters
  format: [
    'summary',
    'json:reports/cucumber-results.json',
    'html:reports/cucumber-report.html',
  ],

  // Parallel execution (1 = sequential, useful for debugging)
  parallel: 1,

  // World parameters (injected into World constructor)
  worldParameters: {
    baseUrl: process.env.BASE_URL || 'http://localhost:4200',
    apiUrl: process.env.API_URL || 'http://localhost:5002',
  },
};
