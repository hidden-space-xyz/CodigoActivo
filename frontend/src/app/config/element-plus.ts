import type { ConfigProviderContext } from 'element-plus'
import ElementPlus from 'element-plus'
import es from 'element-plus/es/locale/lang/es'

import 'element-plus/dist/index.css'
import 'element-plus/theme-chalk/dark/css-vars.css'
import '@/app/styles/element-plus-overrides.css'

const options = {
  locale: es,
  size: 'default',
} as unknown as ConfigProviderContext

/**
 * Element Plus plugin and its Spanish locale options. Importing this module also loads the base
 * styles, the dark-mode variables and the project overrides.
 */
export const elementPlus = {
  plugin: ElementPlus,
  options,
}
