import pluginVue from 'eslint-plugin-vue'
import vueI18n from '@intlify/eslint-plugin-vue-i18n'
import { withVueTs, vueTsConfigs } from '@vue/eslint-config-typescript'
import eslintConfigPrettier from 'eslint-config-prettier'
import vueAccessibility from 'eslint-plugin-vuejs-accessibility'

export default withVueTs(
  {
    name: 'app/files',
    files: ['**/*.{ts,mts,tsx,vue}'],
  },
  {
    name: 'app/ignores',
    ignores: [
      'dist/**',
      'node_modules/**',
      'coverage/**',
      'src/shared/api/generated/**',
      '**/*.{json,json5,yaml,yml}',
    ],
  },
  pluginVue.configs['flat/recommended'],
  vueTsConfigs.recommendedTypeChecked,
  vueAccessibility.configs['flat/recommended'],
  {
    name: 'app/i18n',
    plugins: {
      '@intlify/vue-i18n': vueI18n,
    },
    rules: {
      '@intlify/vue-i18n/no-deprecated-i18n-component': 'error',
      '@intlify/vue-i18n/no-deprecated-i18n-place-attr': 'error',
      '@intlify/vue-i18n/no-deprecated-i18n-places-prop': 'error',
      '@intlify/vue-i18n/no-deprecated-modulo-syntax': 'error',
      '@intlify/vue-i18n/no-deprecated-tc': 'error',
      '@intlify/vue-i18n/no-deprecated-v-t': 'error',
      '@intlify/vue-i18n/no-i18n-t-path-prop': 'error',
      '@intlify/vue-i18n/no-raw-text': [
        'error',
        {
          attributes: {
            '/.+/': ['aria-label', 'alt', 'placeholder', 'title'],
          },
          ignorePattern: '^[\\p{P}\\p{S}\\p{N}\\s]*$',
          ignoreText: ['A', 'B', 'H1', 'H2', 'H3', 'I', 'S', 'U'],
        },
      ],
      '@intlify/vue-i18n/no-v-html': 'error',
      'vuejs-accessibility/label-has-for': [
        'error',
        {
          required: {
            some: ['id', 'nesting'],
          },
        },
      ],
    },
  },
  eslintConfigPrettier,
)
