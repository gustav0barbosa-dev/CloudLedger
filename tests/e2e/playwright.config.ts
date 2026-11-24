import path from 'path';
import { defineConfig } from '@playwright/test';

const root = path.resolve(__dirname, '../..');

export default defineConfig({
  testDir: './tests',
  timeout: 60_000,
  expect: { timeout: 10_000 },
  fullyParallel: true,
  retries: process.env.CI ? 1 : 0,
  reporter: [['list']],
  use: {
    baseURL: 'http://localhost:4200',
    channel: 'chrome',
    headless: true,
    trace: 'on-first-retry'
  },
  webServer: [
    {
      command: 'dotnet run --project backend/CloudLedger.Api --no-launch-profile',
      cwd: root,
      url: 'http://localhost:5080/health/live',
      reuseExistingServer: !process.env.CI,
      timeout: 120_000,
      env: {
        ASPNETCORE_ENVIRONMENT: 'Development',
        ASPNETCORE_URLS: 'http://localhost:5080',
        Database__Provider: 'InMemory',
        Database__AutoMigrate: 'true',
        Mongo__Enabled: 'false',
        Aws__Enabled: 'false',
        RateLimiting__Auth__PermitLimit: '1000'
      }
    },
    {
      command: 'npm start -- --host 127.0.0.1 --port 4200',
      cwd: path.join(root, 'frontend/cloudledger-web'),
      url: 'http://localhost:4200',
      reuseExistingServer: !process.env.CI,
      timeout: 120_000
    }
  ]
});
