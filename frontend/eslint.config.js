import { existsSync } from 'node:fs'
import path from 'node:path'
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

const testSources = ['src/**/*.{spec,test}.ts', 'src/**/__tests__/**', 'tests/**']

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

const componentApiContexts = [
  'CallExpression[callee.name="defineEmits"] > TSTypeParameterInstantiation > TSTypeLiteral > TSPropertySignature',
  'CallExpression[callee.name="defineExpose"] > ObjectExpression > Property',
]

const globalI18nImport = {
  name: '@/shared/i18n',
  importNames: ['i18n'],
  message: 'Translate with `useI18n`, or pass a `Translate` to pure helpers.',
}

const imperativeFeedbackImport = {
  name: 'element-plus',
  importNames: ['ElMessage', 'ElMessageBox', 'ElNotification', 'ElLoading'],
  message: 'Give feedback through `@/shared/lib/feedback`.',
}

const generatedClientPattern = {
  regex: '(^|/)api/generated(/|$)',
  message: 'Only `api` segments use the generated client; map its DTOs to model types there.',
}

const viewOnlyImports = [
  {
    name: '@tanstack/vue-query',
    importNames: ['useMutation', 'useQueryClient'],
    message: 'Views bind to a model composable; mutations and cache access live in `model`.',
  },
  {
    name: '@/shared/api',
    importNames: [
      'ApiError',
      'FEATURED_FIRST_SORT',
      'alsoInvalidates',
      'createQueryClient',
      'hasErrorCode',
      'httpClient',
      'resetCsrfToken',
      'toPage',
      'unwrapOrNull',
    ],
    message: 'Views only resolve file URLs; requests and API errors belong to `api` and `model`.',
  },
]

/** @type {import('eslint').ESLint.Plugin} */
const conventions = {
  rules: {
    'file-name-case': {
      meta: {
        type: 'problem',
        schema: [],
        messages: {
          component: 'Name component files in PascalCase, such as `EventCard.vue`.',
          module: 'Name non-component files in kebab-case, such as `use-events-admin.ts`.',
        },
      },
      create(context) {
        const name = path.basename(context.filename)
        const component = name.endsWith('.vue')
        const pattern = component
          ? /^[A-Z][A-Za-z0-9]*\.vue$/
          : /^[a-z0-9]+(?:-[a-z0-9]+)*(?:\.[a-z0-9]+)+$/
        return pattern.test(name)
          ? {}
          : {
              Program: (node) =>
                context.report({ node, messageId: component ? 'component' : 'module' }),
            }
      },
    },
    'spec-mirrors-source': {
      meta: {
        type: 'problem',
        schema: [],
        messages: {
          unmirrored: 'Place the spec at the `src/` path of the file it tests.',
        },
      },
      create(context) {
        const tests = path.join(import.meta.dirname, 'tests')
        const segments = path.relative(tests, context.filename).split(path.sep).slice(1)
        const source = path.join(import.meta.dirname, 'src', ...segments).replace(/\.spec\.ts$/, '')
        return ['.ts', '.vue'].some((extension) => existsSync(source + extension))
          ? {}
          : { Program: (node) => context.report({ node, messageId: 'unmirrored' }) }
      },
    },
  },
}

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
    name: 'app/architecture',
    files: ['src/{pages,widgets,features,entities}/**/*.{ts,vue}'],
    rules: {
      'no-restricted-imports': [
        'error',
        {
          paths: [globalI18nImport, imperativeFeedbackImport],
          patterns: [generatedClientPattern],
        },
      ],
    },
  },
  {
    name: 'app/architecture-api-segments',
    files: ['src/{pages,widgets,features,entities}/**/api/**/*.ts'],
    rules: {
      'no-restricted-imports': ['error', { paths: [globalI18nImport, imperativeFeedbackImport] }],
    },
  },
  {
    name: 'app/architecture-views',
    files: ['src/{pages,widgets,features,entities,shared}/**/*.vue'],
    rules: {
      'no-restricted-imports': [
        'error',
        {
          paths: [globalI18nImport, imperativeFeedbackImport, ...viewOnlyImports],
          patterns: [generatedClientPattern],
        },
      ],
    },
  },
  {
    name: 'app/architecture-generated-client',
    files: ['src/app/**/*.{ts,vue}', 'src/shared/**/*.ts'],
    ignores: ['src/shared/api/**'],
    rules: {
      'no-restricted-imports': ['error', { patterns: [generatedClientPattern] }],
    },
  },
  {
    name: 'app/file-names',
    files: ['src/**/*.{ts,vue}'],
    plugins: { conventions },
    rules: {
      'conventions/file-name-case': 'error',
    },
  },
  {
    name: 'app/test-layout',
    files: ['tests/{unit,integration}/**/*.spec.ts'],
    ignores: ['tests/unit/docker/**'],
    plugins: { conventions },
    rules: {
      'conventions/spec-mirrors-source': 'error',
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
          ignores: [
            '/^widgets\\.siteHeader\\.nav\\./u',
            '/^widgets\\.adminShell\\.nav\\./u',
            '/^seo\\.routes\\./u',
            '/^errors\\./u',
            '/^pages\\.about\\.organization\\./u',
            '/^pages\\.account\\.certificates\\.sheet\\./u',
            '/^pages\\.admin\\.dashboard\\.(range\\.preset|series\\.)/u',
            '/^pages\\.admin\\.eventRoster\\.rolePlural/u',
            '/^pages\\.admin\\.diagnostics\\.levels\\./u',
            '/^pages\\.admin\\.audit\\.(actions|actorTypes|entityTypes|outcomes|codes|deletedEntities)\\./u',
            '/^pages\\.admin\\.blockedData\\.(authorities|entities)\\./u',
            '/^pages\\.account\\.personalData\\.(actors|sources)\\./u',
            '/^entities\\.event\\.status\\./u',
            '/^entities\\.user\\.gender\\./u',
            '/^entities\\.user\\.person\\./u',
            '/\\.form\\.problems\\./u',
            '/^validation\\.richTextTooLong$/u',
          ],
        },
      ],
      '@intlify/vue-i18n/valid-message-syntax': 'error',
    },
  },
  eslintConfigPrettier,
)
