/** @type {import('stylelint').Config} */
export default {
  extends: ['stylelint-config-recommended-vue'],
  ignoreFiles: ['dist/**', 'node_modules/**', 'src/shared/api/generated/**'],
  rules: {
    'selector-class-pattern': [
      '^(?:[a-z][a-z0-9]*(?:-[a-z0-9]+)*(?:__[a-z0-9]+(?:-[a-z0-9]+)*)?(?:--[a-z][A-Za-z0-9-]*)?|el-[A-Za-z0-9-]+(?:__[A-Za-z0-9-]+)?(?:--[A-Za-z0-9-]+)?|ProseMirror(?:-[A-Za-z0-9-]+)?|selectedCell|tableWrapper)$',
      {
        message: 'Expected class selector to use kebab-case or BEM notation',
      },
    ],
  },
}
