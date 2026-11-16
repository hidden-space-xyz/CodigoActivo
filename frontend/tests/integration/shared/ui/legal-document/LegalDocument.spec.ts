import { describe, expect, it } from 'vitest'

import { renderWithProviders } from '../../../../support/render'
import { LegalDocument } from '@/shared/ui/legal-document'

describe('LegalDocument', () => {
  it('shows the lead and the last update under the title when given', async () => {
    const { wrapper } = await renderWithProviders(LegalDocument, {
      props: { eyebrow: 'Legal', title: 'Privacy', lead: 'How we use data', updated: 'May 2026' },
      slots: { default: '<p class="clause">Clause</p>' },
    })

    expect(wrapper.get('h1').text()).toBe('Privacy')
    expect(wrapper.get('.legal-document__lead').text()).toBe('How we use data')
    expect(wrapper.get('.legal-document__updated').text()).toBe('May 2026')
    expect(wrapper.get('.legal-document__body .clause').text()).toBe('Clause')
  })

  it('omits the lead and the last update when they are not set', async () => {
    const { wrapper } = await renderWithProviders(LegalDocument, {
      props: { eyebrow: 'Legal', title: 'Privacy' },
    })

    expect(wrapper.find('.legal-document__lead').exists()).toBe(false)
    expect(wrapper.find('.legal-document__updated').exists()).toBe(false)
  })
})
