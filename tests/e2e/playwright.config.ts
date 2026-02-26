import { defineConfig, devices } from '@playwright/test';

/**
 * Playwright configuration for BananaGame E2E tests.
 * - Headless Chromium + mobile (iPhone 12) only (as per prompt constraints).
 * - webServer starts the ASP.NET + Vite dev environment before tests run.
 * - baseURL points at the Vite proxy (all /gamehub requests forwarded to C# server).
 */
export default defineConfig({
  testDir: './tests',
  timeout: 30_000,
  retries: 1,
  workers: 1, // serial execution — required for state-dependent game tests

  reporter: [
    ['list'],
    ['html', { outputFolder: 'playwright-report', open: 'never' }],
  ],

  use: {
    baseURL: 'http://localhost:5173',
    trace: 'on-first-retry',
    // Capture all console messages so JS errors can be surfaced in reports
    // (collected via page.on('console') in each test)
  },

  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] },
    },
    {
      name: 'mobile-chrome',
      use: { ...devices['iPhone 12'] },
    },
  ],

  // Assumes the server is already running (per PoTest instructions).
  // Uncomment webServer block if you want Playwright to auto-start it:
  //
  // webServer: {
  //   command: 'cd ../../client && npm run dev',
  //   url: 'http://localhost:5173',
  //   reuseExistingServer: true,
  //   timeout: 30_000,
  // },
});
