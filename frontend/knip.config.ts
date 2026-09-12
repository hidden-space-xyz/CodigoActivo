import type { KnipConfig } from 'knip'

const config: KnipConfig = {
  entry: ['steiger.config.ts'],
  project: ['src/**/*.{ts,vue}'],
  ignore: ['src/shared/api/generated/**'],
}

export default config
