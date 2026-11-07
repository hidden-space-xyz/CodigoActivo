import { defineConfig, devices } from '@playwright/test'

import { environment, TIME_ZONE } from './support/config/environment'

export default defineConfig({
  testDir: './specs',
  outputDir: './results/artifacts',
  timeout: 90_000,
  expect: { timeout: 10_000 },
  fullyParallel: false,
  forbidOnly: !!process.env.CI,
  retries: 0,
  workers: 4,
  reporter: [['list'], ['html', { outputFolder: './results/report', open: 'never' }]],
  use: {
    baseURL: environment.baseUrl,
    actionTimeout: 15_000,
    navigationTimeout: 30_000,
    locale: 'es-ES',
    timezoneId: TIME_ZONE,
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
    video: 'off',
  },
  projects: [
    {
      name: 'setup',
      testMatch: 'setup/*.setup.ts',
    },
    {
      name: 'desktop',
      testIgnore: 'setup/**',
      grepInvert: /@mobile/,
      dependencies: ['setup'],
      use: { ...devices['Desktop Chrome'] },
    },
    {
      name: 'mobile',
      testIgnore: 'setup/**',
      grep: /@mobile/,
      dependencies: ['setup'],
      use: { ...devices['Pixel 7'] },
    },
  ],
})
