import { flushPromises } from '@vue/test-utils'
import { describe, expect, it } from 'vitest'

import EventTermsDialog from '@/pages/event-detail/ui/EventTermsDialog.vue'
import type { EventTermsDocumentState } from '@/entities/event'

import { renderWithProviders, t } from '../../../../support/render'
import { richText } from '../../../../support/builders'
import { click, findButton, findDialog, textOf } from '../../../../support/dom'

const REQUIRED_DOC: EventTermsDocumentState = {
  id: 'terms-required',
  name: 'Normas del campamento',
  description: richText('Respeta a los demás.'),
  required: true,
  displayOrder: 0,
  accepted: null,
  decidedAt: null,
}

const OPTIONAL_DOC: EventTermsDocumentState = {
  id: 'terms-optional',
  name: 'Boletín informativo',
  description: richText('Recibe noticias del evento.'),
  required: false,
  displayOrder: 1,
  accepted: null,
  decidedAt: null,
}

/** Finds the checkbox input for a terms document row, however Element Plus placed the id. */
function termsCheckbox(root: ParentNode, documentId: string): HTMLElement {
  const checkbox = root.querySelector<HTMLElement>(
    `#terms-${documentId} input, input#terms-${documentId}`,
  )
  if (!checkbox) throw new Error(`Terms checkbox not found: ${documentId}`)
  return checkbox
}

function renderDialog(documents: readonly EventTermsDocumentState[]) {
  return renderWithProviders(EventTermsDialog, {
    props: { visible: true, documents },
    attach: true,
  })
}

describe('EventTermsDialog document list', () => {
  it('lists every pending document with its required/optional badge', async () => {
    await renderDialog([REQUIRED_DOC, OPTIONAL_DOC])

    const rows = [...document.body.querySelectorAll('.terms-dialog__row')]
    expect(rows).toHaveLength(2)
    expect(textOf(rows[0]?.querySelector('.terms-dialog__link') ?? null)).toBe(REQUIRED_DOC.name)
    expect(textOf(rows[0]?.querySelector('.terms-dialog__tag') ?? null)).toBe(
      t('pages.eventDetail.terms.required'),
    )
    expect(rows[0]?.querySelector('.terms-dialog__tag')?.className).toContain(
      'terms-dialog__tag--required',
    )

    expect(textOf(rows[1]?.querySelector('.terms-dialog__link') ?? null)).toBe(OPTIONAL_DOC.name)
    expect(textOf(rows[1]?.querySelector('.terms-dialog__tag') ?? null)).toBe(
      t('pages.eventDetail.terms.optional'),
    )
    expect(rows[1]?.querySelector('.terms-dialog__tag')?.className).toContain(
      'terms-dialog__tag--optional',
    )
  })

  it('only shows the documents the parent still considers pending', async () => {
    await renderDialog([OPTIONAL_DOC])

    const rows = [...document.body.querySelectorAll('.terms-dialog__row')]
    expect(rows).toHaveLength(1)
    expect(textOf(rows[0]?.querySelector('.terms-dialog__link') ?? null)).toBe(OPTIONAL_DOC.name)
  })
})

describe('EventTermsDialog confirmation gate', () => {
  it('points out the unchecked required documents only after trying to confirm', async () => {
    const { wrapper } = await renderDialog([REQUIRED_DOC, OPTIONAL_DOC])

    expect(document.body.querySelector('.terms-dialog__warning')).toBeNull()
    await click(findButton(t('pages.eventDetail.terms.confirm')))

    expect(wrapper.emitted('confirm')).toBeUndefined()
    const warning = document.body.querySelector('.terms-dialog__warning')
    expect(textOf(warning)).toBe(t('pages.eventDetail.terms.missingRequired'))
    expect(warning?.getAttribute('role')).toBe('alert')

    await click(termsCheckbox(document.body, REQUIRED_DOC.id))

    expect(document.body.querySelector('.terms-dialog__warning')).toBeNull()
  })

  it('forgets an earlier attempt when it opens again', async () => {
    const { wrapper } = await renderDialog([REQUIRED_DOC])
    await click(findButton(t('pages.eventDetail.terms.confirm')))
    expect(document.body.querySelector('.terms-dialog__warning')).not.toBeNull()

    await wrapper.setProps({ visible: false })
    await wrapper.setProps({ visible: true })
    await flushPromises()

    expect(document.body.querySelector('.terms-dialog__warning')).toBeNull()
  })

  it('does not require checking optional documents', async () => {
    const { wrapper } = await renderDialog([REQUIRED_DOC, OPTIONAL_DOC])
    await click(termsCheckbox(document.body, REQUIRED_DOC.id))

    await click(findButton(t('pages.eventDetail.terms.confirm')))

    expect(wrapper.emitted('confirm')).toHaveLength(1)
    expect(document.body.querySelector('.terms-dialog__warning')).toBeNull()
  })
})

describe('EventTermsDialog document preview', () => {
  it('hides the content until the document name is opened, and can be closed', async () => {
    await renderDialog([REQUIRED_DOC])

    expect(findDialog(REQUIRED_DOC.name)).toBeUndefined()
    expect(document.body.textContent).not.toContain('Respeta a los demás.')

    await click(findButton(REQUIRED_DOC.name))

    const preview = findDialog(REQUIRED_DOC.name)
    expect(preview).toBeDefined()
    expect(textOf(preview?.querySelector('.rich-text') ?? null)).toContain('Respeta a los demás.')

    await click(findButton(t('pages.eventDetail.terms.close'), preview))

    expect(findDialog(REQUIRED_DOC.name)).toBeUndefined()
  })

  it('checks the document when accepted from the preview and unchecks it when rejected', async () => {
    await renderDialog([REQUIRED_DOC])
    const checkbox = termsCheckbox(document.body, REQUIRED_DOC.id) as HTMLInputElement

    await click(findButton(REQUIRED_DOC.name))
    let preview = findDialog(REQUIRED_DOC.name)
    await click(findButton(t('pages.eventDetail.terms.accept'), preview))

    expect(findDialog(REQUIRED_DOC.name)).toBeUndefined()
    expect(checkbox.checked).toBe(true)

    await click(findButton(REQUIRED_DOC.name))
    preview = findDialog(REQUIRED_DOC.name)
    await click(findButton(t('pages.eventDetail.terms.reject'), preview))

    expect(checkbox.checked).toBe(false)
  })
})

describe('EventTermsDialog confirm decisions', () => {
  it('sends one decision per pending document: true for checked, false for unchecked optional ones', async () => {
    const { wrapper } = await renderDialog([REQUIRED_DOC, OPTIONAL_DOC])

    await click(termsCheckbox(document.body, REQUIRED_DOC.id))
    await click(findButton(t('pages.eventDetail.terms.confirm')))

    expect(wrapper.emitted('confirm')).toEqual([
      [
        [
          { termsDocumentId: REQUIRED_DOC.id, accepted: true },
          { termsDocumentId: OPTIONAL_DOC.id, accepted: false },
        ],
      ],
    ])
  })

  it('does not include a decision for a document the caller already resolved', async () => {
    const { wrapper } = await renderDialog([OPTIONAL_DOC])

    await click(findButton(t('pages.eventDetail.terms.confirm')))

    expect(wrapper.emitted('confirm')).toEqual([
      [[{ termsDocumentId: OPTIONAL_DOC.id, accepted: false }]],
    ])
  })
})
