import { fileURLToPath } from 'node:url'

import { defineConfig, mergeConfig } from 'vitest/config'

import viteConfig from './vite.config.ts'

const browser = { environment: 'jsdom', setupFiles: ['tests/setup.ts'] }

export default defineConfig((env) =>
  mergeConfig(viteConfig(env), {
    test: {
      root: fileURLToPath(new URL('./', import.meta.url)),
      restoreMocks: true,
      unstubGlobals: true,
      unstubEnvs: true,
      projects: [
        {
          extends: true,
          test: { ...browser, name: 'unit', include: ['tests/unit/**/*.spec.ts'] },
        },
        {
          extends: true,
          test: { ...browser, name: 'integration', include: ['tests/integration/**/*.spec.ts'] },
        },
        {
          extends: true,
          test: {
            name: 'architecture',
            environment: 'node',
            include: ['tests/architecture/**/*.spec.ts'],
          },
        },
      ],
      coverage: {
        provider: 'v8',
        include: ['src/**/*.{ts,vue}'],
        exclude: ['src/shared/api/generated/**', 'src/app/main.ts'],
        reporter: ['text-summary', 'html', 'lcov'],
        thresholds: {
          statements: 90,
          branches: 90,
          functions: 90,
          lines: 90,
        },
      },
    },
  }),
)
