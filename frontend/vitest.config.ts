import { fileURLToPath } from 'node:url'

import { defineConfig, mergeConfig } from 'vitest/config'

import viteConfig from './vite.config.ts'

export default defineConfig((env) =>
  mergeConfig(viteConfig(env), {
    test: {
      environment: 'jsdom',
      root: fileURLToPath(new URL('./', import.meta.url)),
      setupFiles: ['tests/setup.ts'],
      restoreMocks: true,
      unstubGlobals: true,
      unstubEnvs: true,
      projects: [
        {
          extends: true,
          test: { name: 'unit', include: ['tests/unit/**/*.spec.ts'] },
        },
        {
          extends: true,
          test: { name: 'integration', include: ['tests/integration/**/*.spec.ts'] },
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
