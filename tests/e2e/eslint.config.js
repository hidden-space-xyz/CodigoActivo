import path from 'node:path'
import { defineConfig } from 'eslint/config'
import eslintConfigPrettier from 'eslint-config-prettier'
import jsdoc from 'eslint-plugin-jsdoc'
import playwright from 'eslint-plugin-playwright'
import tseslint from 'typescript-eslint'

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

/** @type {import('eslint').ESLint.Plugin} */
const conventions = {
  rules: {
    'file-name-case': {
      meta: {
        type: 'problem',
        schema: [],
        messages: {
          module: 'Name files in kebab-case, such as `event-detail-page.ts`.',
        },
      },
      create(context) {
        const name = path.basename(context.filename)
        return /^[a-z0-9]+(?:-[a-z0-9]+)*(?:\.[a-z0-9]+)+$/.test(name)
          ? {}
          : { Program: (node) => context.report({ node, messageId: 'module' }) }
      },
    },
  },
}

export default defineConfig(
  {
    name: 'e2e/ignores',
    ignores: ['node_modules/**', 'results/**'],
  },
  {
    name: 'e2e/files',
    files: ['**/*.{js,ts}'],
    extends: [tseslint.configs.recommendedTypeChecked],
    languageOptions: {
      parserOptions: {
        projectService: true,
        tsconfigRootDir: import.meta.dirname,
      },
    },
    plugins: { conventions },
    rules: {
      'conventions/file-name-case': 'error',
      '@typescript-eslint/consistent-type-imports': 'error',
      '@typescript-eslint/no-import-type-side-effects': 'error',
      '@typescript-eslint/switch-exhaustiveness-check': 'error',
    },
  },
  {
    ...playwright.configs['flat/recommended'],
    name: 'e2e/playwright',
    files: ['support/**/*.ts', 'specs/**/*.ts'],
    rules: {
      ...playwright.configs['flat/recommended'].rules,
      'playwright/expect-expect': ['error', { assertFunctionPatterns: ['^expect[A-Z]'] }],
      'playwright/no-commented-out-tests': 'error',
      'playwright/no-duplicate-hooks': 'error',
      'playwright/no-get-by-title': 'error',
      'playwright/prefer-comparison-matcher': 'error',
      'playwright/prefer-equality-matcher': 'error',
      'playwright/prefer-hooks-in-order': 'error',
      'playwright/prefer-hooks-on-top': 'error',
      'playwright/prefer-locator': 'error',
      'playwright/prefer-strict-equal': 'error',
      'playwright/prefer-to-be': 'error',
      'playwright/prefer-to-contain': 'error',
      'playwright/prefer-to-have-count': 'error',
      'playwright/prefer-to-have-length': 'error',
      'playwright/require-to-throw-message': 'error',
    },
  },
  {
    name: 'e2e/specs',
    files: ['specs/**/*.ts'],
    rules: {
      'playwright/no-raw-locators': 'error',
      'playwright/require-top-level-describe': 'error',
    },
  },
  {
    name: 'e2e/documentation',
    files: ['support/**/*.ts'],
    plugins: { jsdoc },
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
  eslintConfigPrettier,
)
