import { ElMessageBox } from 'element-plus'

import { i18n } from '@/shared/i18n'

/** Message box that asks for a line of text, with "apply" and "cancel" buttons. */
export function useTextPrompt() {
  /**
   * Resolves with the text entered, or `null` when the prompt is dismissed. While `validate`
   * returns a message, the prompt stays open and shows it under the input.
   */
  async function promptText(options: {
    header: string
    message: string
    value: string
    validate: (value: string) => string | true
  }): Promise<string | null> {
    try {
      const { value } = await ElMessageBox.prompt(options.message, options.header, {
        inputValue: options.value,
        inputValidator: options.validate,
        confirmButtonText: i18n.global.t('common.apply'),
        cancelButtonText: i18n.global.t('common.cancel'),
      })
      return value
    } catch {
      return null
    }
  }

  return { promptText }
}
