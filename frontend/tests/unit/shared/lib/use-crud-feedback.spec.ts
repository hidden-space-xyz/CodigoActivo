import { describe, expect, it, vi } from 'vitest'

import { ApiError } from '@/shared/api'
import { ErrorCode } from '@/shared/api/generated/models'
import { useCrudFeedback } from '@/shared/lib'

import { withSetup } from '../../../support/fixtures/shared-app/with-setup'
import { t } from '../../../support/render'

async function notification(): Promise<HTMLElement> {
  let found: HTMLElement | null = null
  await vi.waitFor(() => {
    found = document.body.querySelector<HTMLElement>('.el-notification')
    expect(found).not.toBeNull()
  })
  return found as unknown as HTMLElement
}

describe('useCrudFeedback', () => {
  it('shows a success notification with the default title', async () => {
    const { result } = await withSetup(() => useCrudFeedback())

    result.success('Saved the event')

    const toast = await notification()
    expect(toast.classList.contains('right')).toBe(true)
    expect(toast.querySelector('.el-notification__title')?.textContent).toBe(t('common.done'))
    expect(toast.textContent).toContain('Saved the event')
    expect(toast.querySelector('.el-notification__icon')?.className).toContain('success')
  })

  it('shows a warning notification with a custom title', async () => {
    const { result } = await withSetup(() => useCrudFeedback())

    result.warn('Careful', 'Heads up')

    const toast = await notification()
    expect(toast.querySelector('.el-notification__title')?.textContent).toBe('Heads up')
    expect(toast.textContent).toContain('Careful')
    expect(toast.querySelector('.el-notification__icon')?.className).toContain('warning')
  })

  it('shows the localized API error message with its trace reference', async () => {
    const { result } = await withSetup(() => useCrudFeedback())

    result.error(new ApiError(404, 'raw', 'trace-42', ErrorCode.EventNotFound))

    const toast = await notification()
    expect(toast.querySelector('.el-notification__title')?.textContent).toBe(t('common.error'))
    expect(toast.textContent).toContain(t('errors.EventNotFound'))
    expect(toast.textContent).toContain(t('table.ref', { id: 'trace-42' }))
    expect(toast.textContent).not.toContain('raw')
    expect(toast.querySelector('.el-notification__icon')?.className).toContain('error')
  })

  it('shows only the generic message for errors without a trace id', async () => {
    const { result } = await withSetup(() => useCrudFeedback())

    result.error(new Error('Internal detail'), 'Could not save')

    const toast = await notification()
    expect(toast.querySelector('.el-notification__title')?.textContent).toBe('Could not save')
    expect(toast.textContent).toContain(t('errors.generic'))
    expect(toast.textContent).not.toContain('Internal detail')
    expect(toast.textContent).not.toContain(t('table.ref', { id: '' }).trim())
  })

  it('shows a given message as is', async () => {
    const { result } = await withSetup(() => useCrudFeedback())

    result.error('The event no longer exists')

    const toast = await notification()
    expect(toast.querySelector('.el-notification__title')?.textContent).toBe(t('common.error'))
    expect(toast.textContent).toContain('The event no longer exists')
    expect(toast.textContent).not.toContain(t('errors.generic'))
  })
})
