FROM mcr.microsoft.com/playwright:v1.52.0-noble

WORKDIR /app

# Copy package files
COPY tests/e2e/package.json tests/e2e/package-lock.json* ./

# Install dependencies
RUN npm ci

# Copy test files
COPY tests/e2e/ .

# Default command: run all tests
CMD ["npx", "cucumber-js"]
