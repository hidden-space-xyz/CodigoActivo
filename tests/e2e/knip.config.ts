import type { KnipConfig } from 'knip'

const config: KnipConfig = {
  project: ['support/**/*.ts', 'specs/**/*.ts'],
  playwright: {
    config: ['playwright.config.ts'],
    entry: ['specs/**/*.{spec,setup}.ts'],
  },
}

export default config
