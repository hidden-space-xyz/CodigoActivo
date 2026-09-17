import pluginVue from 'eslint-plugin-vue'
import vueI18n from '@intlify/eslint-plugin-vue-i18n'
import { withVueTs, vueTsConfigs } from '@vue/eslint-config-typescript'
import eslintConfigPrettier from 'eslint-config-prettier'
import jsdoc from 'eslint-plugin-jsdoc'
import vueAccessibility from 'eslint-plugin-vuejs-accessibility'
import * as jsoncParser from 'jsonc-eslint-parser'

const disableTypeChecked = Array.isArray(vueTsConfigs.disableTypeChecked)
  ? vueTsConfigs.disableTypeChecked[0]
  : vueTsConfigs.disableTypeChecked

if (!disableTypeChecked) throw new Error('Missing disable-type-checked ESLint configuration')

// Test names must explain themselves, so tests are exempt from documentation rules.
const testSources = ['src/**/*.{spec,test}.ts', 'src/**/__tests__/**', 'tests/**']

// Mirrors CS1591 in the backend: top-level exports and public class members form a module's public API.
const publicApiContexts = [
  'Program > ExportDefaultDeclaration',
  'Program > ExportNamedDeclaration > ClassDeclaration',
  'Program > ExportNamedDeclaration > FunctionDeclaration',
  'Program > ExportNamedDeclaration > TSDeclareFunction',
  'Program > ExportNamedDeclaration > TSEnumDeclaration',
  'Program > ExportNamedDeclaration > TSInterfaceDeclaration',
  'Program > ExportNamedDeclaration > TSTypeAliasDeclaration',
  'Program > ExportNamedDeclaration[declaration.type="VariableDeclaration"]',
  'ExportNamedDeclaration > ClassDeclaration > ClassBody > MethodDefinition[kind!="constructor"][accessibility!="private"][accessibility!="protected"]',
  'ExportNamedDeclaration > ClassDeclaration > ClassBody > PropertyDefinition[accessibility!="private"][accessibility!="protected"]',
]

// Props, emits and exposed members form a component's public API.
const componentApiContexts = [
  'CallExpression[callee.name="defineEmits"] > TSTypeParameterInstantiation > TSTypeLiteral > TSPropertySignature',
  'CallExpression[callee.name="defineExpose"] > ObjectExpression > Property',
]

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
      '!src/shared/i18n/locales/*.json',
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
    settings: {
      'vue-i18n': {
        localeDir: {
          pattern: './src/shared/i18n/locales/*.json',
          localeKey: 'file',
        },
        messageSyntaxVersion: '^11.0.0',
      },
    },
    rules: {
      '@intlify/vue-i18n/no-deprecated-i18n-component': 'error',
      '@intlify/vue-i18n/no-deprecated-i18n-place-attr': 'error',
      '@intlify/vue-i18n/no-deprecated-i18n-places-prop': 'error',
      '@intlify/vue-i18n/no-deprecated-modulo-syntax': 'error',
      '@intlify/vue-i18n/no-deprecated-tc': 'error',
      '@intlify/vue-i18n/no-deprecated-v-t': 'error',
      '@intlify/vue-i18n/no-i18n-t-path-prop': 'error',
      '@intlify/vue-i18n/no-missing-keys': 'error',
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
  {
    name: 'app/documentation',
    files: ['src/**/*.{ts,vue}'],
    ignores: testSources,
    plugins: {
      jsdoc,
    },
    settings: {
      jsdoc: {
        mode: 'typescript',
      },
    },
    rules: {
      'jsdoc/check-alignment': 'error',
      'jsdoc/check-param-names': 'error',
      'jsdoc/check-tag-names': 'error',
      'jsdoc/empty-tags': 'error',
      'jsdoc/informative-docs': 'error',
      'jsdoc/no-blank-block-descriptions': 'error',
      'jsdoc/no-blank-blocks': 'error',
      'jsdoc/no-types': 'error',
      'jsdoc/require-description': 'error',
      'jsdoc/require-jsdoc': [
        'error',
        {
          require: {
            FunctionDeclaration: false,
          },
          contexts: publicApiContexts,
        },
      ],
    },
  },
  {
    name: 'app/documentation-components',
    files: ['src/**/*.vue'],
    ignores: testSources,
    rules: {
      'vue/require-prop-comment': ['error', { type: 'JSDoc' }],
      'jsdoc/require-jsdoc': [
        'error',
        {
          require: {
            FunctionDeclaration: false,
          },
          contexts: [...publicApiContexts, ...componentApiContexts],
        },
      ],
    },
  },
  {
    ...disableTypeChecked,
    name: 'app/i18n-locales',
    files: ['src/shared/i18n/locales/*.json'],
    languageOptions: {
      ...disableTypeChecked.languageOptions,
      parser: jsoncParser,
    },
    rules: {
      ...disableTypeChecked.rules,
      '@intlify/vue-i18n/no-duplicate-keys-in-locale': 'error',
      '@intlify/vue-i18n/no-html-messages': 'error',
      '@intlify/vue-i18n/no-unused-keys': [
        'error',
        {
          src: './src',
          extensions: ['.ts', '.vue'],
          // These namespaces are resolved through TranslationKey-typed maps or backend ErrorCode values.
          ignores: [
            '/^nav\\./u',
            '/^adminNav\\./u',
            '/^seo\\.routes\\./u',
            '/^errors\\./u',
            '/^features\\.account\\.certificates\\.sheet\\./u',
            '/^entities\\.event\\.status\\./u',
            '/^entities\\.user\\.gender\\./u',
          ],
        },
      ],
      '@intlify/vue-i18n/valid-message-syntax': 'error',
    },
  },
  eslintConfigPrettier,
)
