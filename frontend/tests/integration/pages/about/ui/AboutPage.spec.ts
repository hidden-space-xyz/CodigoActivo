import { flushPromises } from '@vue/test-utils'
import { ElDialog } from 'element-plus'
import { describe, expect, it, vi } from 'vitest'

import { http, HttpResponse, server, TEST_LEGAL_ENTITY } from '../../../../support/server'

import { renderApp, t } from '../../../../support/render'

async function openJoinDialog(wrapper: Awaited<ReturnType<typeof renderApp>>['wrapper']) {
  const join = wrapper
    .get('main')
    .findAll('button')
    .find((button) => button.text() === t('pages.about.cta.join'))

  await join?.trigger('click')

  await vi.waitFor(() =>
    expect(document.body.querySelector('.el-dialog')?.textContent).toContain(
      t('pages.about.cta.dialog.header'),
    ),
  )
  return document.body.querySelector('.el-dialog')
}

function contactLinks(dialog: Element | null): (string | null)[] {
  return Array.from(dialog?.querySelectorAll('a') ?? []).map((a) => a.getAttribute('href'))
}

describe('about page', () => {
  it('opens a dialog with contact details to join the project', async () => {
    const { wrapper } = await renderApp('/about')

    const dialog = await openJoinDialog(wrapper)

    await vi.waitFor(() =>
      expect(contactLinks(dialog)).toEqual([`mailto:${TEST_LEGAL_ENTITY.email}`, 'tel:987000000']),
    )

    const dialogComponent = wrapper.findComponent(ElDialog)
    dialogComponent.vm.$emit('update:modelValue', false)

    await vi.waitFor(() => expect(dialogComponent.props('modelValue')).toBe(false))
  })

  it('offers no contact link the association does not publish', async () => {
    let served = false
    server.use(
      http.get('/api/legal-entity', () => {
        served = true
        return HttpResponse.json({ ...TEST_LEGAL_ENTITY, email: '', phone: null })
      }),
    )
    const { wrapper } = await renderApp('/about')
    await vi.waitFor(() => expect(served).toBe(true))
    await flushPromises()

    const dialog = await openJoinDialog(wrapper)

    expect(contactLinks(dialog)).toEqual([])
  })
})
