import { describe, expect, it } from 'vitest'

import { LegalPolicyDocument, type LegalPolicy } from '@/entities/legal-policy'
import { formatDate } from '@/shared/lib/date'

import { renderWithProviders, t } from '../../../../support/render'

const POLICY: LegalPolicy = {
  kind: 'Cookies',
  version: 3,
  content: JSON.stringify({
    type: 'doc',
    content: [
      { type: 'heading', attrs: { level: 2 }, content: [{ type: 'text', text: 'Qué guardamos' }] },
      { type: 'paragraph', content: [{ type: 'text', text: 'Solo lo necesario.' }] },
    ],
  }),
  publishedAt: '2026-10-20T08:30:00Z',
}

function render(props: Record<string, unknown>, slots: Record<string, unknown> = {}) {
  return renderWithProviders(LegalPolicyDocument, {
    props: { eyebrow: '// cookies', title: 'Política', loading: false, error: false, ...props },
    slots,
  })
}

describe('LegalPolicyDocument', () => {
  it('shows the text in force after the slot, with its date and version', async () => {
    const { wrapper } = await render(
      { policy: POLICY },
      { default: '<section class="before">Responsable</section>' },
    )

    expect(wrapper.get('h1').text()).toBe('Política')
    expect(wrapper.get('.legal-document__updated').text()).toBe(
      t('entities.legalPolicy.updated', { date: formatDate(POLICY.publishedAt), version: 3 }),
    )
    expect(wrapper.get('.legal-policy h2').text()).toBe('Qué guardamos')
    expect(wrapper.get('.legal-policy p').text()).toBe('Solo lo necesario.')
    expect(wrapper.html().indexOf('before')).toBeLessThan(wrapper.html().indexOf('legal-policy'))
  })

  it('tells visitors a policy that was never published is not available', async () => {
    const { wrapper } = await render({ policy: null })

    expect(wrapper.text()).toContain(t('entities.legalPolicy.unpublished'))
    expect(wrapper.find('.legal-document__updated').exists()).toBe(false)
  })

  it('shows the loading and error states instead of the text', async () => {
    const loading = await render({ policy: undefined, loading: true })
    expect(loading.wrapper.text()).toContain(t('common.loading'))

    const failed = await render({ policy: undefined, error: true })
    expect(failed.wrapper.text()).toContain(t('dataState.error'))
    expect(failed.wrapper.find('.legal-policy').exists()).toBe(false)
  })
})
