import type { KnipConfig } from 'knip'

const config: KnipConfig = {
  entry: ['steiger.config.ts'],
  project: ['src/**/*.{ts,vue}', 'tests/**/*.ts'],
  ignore: ['src/shared/api/generated/**'],
  vitest: {
    config: ['vitest.config.ts'],
    entry: ['tests/setup.ts', 'tests/**/*.spec.ts'],
  },
}

export default config
