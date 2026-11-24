import { flushPromises } from '@vue/test-utils'
import { describe, expect, it } from 'vitest'

import {
  acceptMessageBox,
  cancelMessageBox,
  inputValue,
  messageBox,
  typeInto,
} from '../../../../support/dom'
import { t } from '../../../../support/render'
import { useTextPrompt } from '@/shared/lib/feedback'

describe('useTextPrompt', () => {
  it('resolves with the accepted text and keeps the prompt open while it is invalid', async () => {
    const { promptText } = useTextPrompt()

    const answer = promptText({
      header: 'Link',
      message: 'Address of the link',
      value: 'https://old.test',
      validate: (value) => value.startsWith('https://') || 'Only https',
    })
    await flushPromises()

    expect(messageBox().textContent).toContain('Link')
    expect(messageBox().textContent).toContain('Address of the link')
    expect(inputValue('input', messageBox())).toBe('https://old.test')
    await typeInto('input', 'ftp://new.test', messageBox())
    await acceptMessageBox()
    expect(messageBox().textContent).toContain('Only https')

    await typeInto('input', 'https://new.test', messageBox())
    await acceptMessageBox()

    await expect(answer).resolves.toBe('https://new.test')
  })

  it('resolves with null when the prompt is cancelled', async () => {
    const { promptText } = useTextPrompt()

    const answer = promptText({
      header: 'Link',
      message: 'Address of the link',
      value: '',
      validate: () => true,
    })
    await flushPromises()

    expect(messageBox().textContent).toContain(t('common.apply'))
    await cancelMessageBox()

    await expect(answer).resolves.toBeNull()
  })
})
