import { ElDialog } from 'element-plus'
import { describe, expect, it, vi } from 'vitest'

import { useOrganizationContent } from '@/entities/organization'
import { CONTACT } from '@/shared/config'

import { renderApp, t } from '../../support/render'

describe('about page', () => {
  it('presents the organization values and activities', async () => {
    const { wrapper } = await renderApp('/about')
    const { values, activities } = useOrganizationContent()
    const text = wrapper.get('main').text()

    expect(text).toContain(t('pages.about.what.title'))
    for (const item of [...values, ...activities]) expect(text).toContain(item.title)
    expect(document.title).toContain(t('seo.routes.about.title'))
  })

  it('opens a dialog with contact details to join the project', async () => {
    const { wrapper } = await renderApp('/about')
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
    const dialog = document.body.querySelector('.el-dialog')
    expect(dialog?.textContent).toContain(t('pages.about.cta.dialog.lead'))
    const hrefs = Array.from(dialog?.querySelectorAll('a') ?? []).map((a) => a.getAttribute('href'))
    expect(hrefs).toEqual([`mailto:${CONTACT.email}`, `tel:${CONTACT.phoneHref}`])

    // The dialog reports closing after its leave transition, which test-utils stubs out.
    const dialogComponent = wrapper.findComponent(ElDialog)
    dialogComponent.vm.$emit('update:modelValue', false)

    await vi.waitFor(() => expect(dialogComponent.props('modelValue')).toBe(false))
  })
})
