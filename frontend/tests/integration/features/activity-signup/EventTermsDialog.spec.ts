import { describe, expect, it } from 'vitest'

import EventTermsDialog from '@/features/activity-signup/ui/EventTermsDialog.vue'
import type { EventTermsDocumentState } from '@/entities/event'

import {
  buttonByText,
  clickElement,
  openDialog,
  textOf,
} from '../../../support/fixtures/public-dashboard/dom'
import { renderWithProviders, t } from '../../../support/render'

const REQUIRED_DOC: EventTermsDocumentState = {
  id: 'terms-required',
  name: 'Normas del campamento',
  description: 'Respeta a los demás.',
  required: true,
  displayOrder: 0,
  accepted: null,
  decidedAt: null,
}

const OPTIONAL_DOC: EventTermsDocumentState = {
  id: 'terms-optional',
  name: 'Boletín informativo',
  description: 'Recibe noticias del evento.',
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
      t('features.activitySignup.terms.required'),
    )
    expect(rows[0]?.querySelector('.terms-dialog__tag')?.className).toContain(
      'terms-dialog__tag--required',
    )

    expect(textOf(rows[1]?.querySelector('.terms-dialog__link') ?? null)).toBe(OPTIONAL_DOC.name)
    expect(textOf(rows[1]?.querySelector('.terms-dialog__tag') ?? null)).toBe(
      t('features.activitySignup.terms.optional'),
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
  it('disables confirm while a required document is unchecked and enables it once checked', async () => {
    await renderDialog([REQUIRED_DOC, OPTIONAL_DOC])

    const confirmButton = buttonByText(t('features.activitySignup.terms.confirm'))
    expect(confirmButton.disabled).toBe(true)
    expect(document.body.querySelector('.terms-dialog__warning')).not.toBeNull()

    await clickElement(termsCheckbox(document.body, REQUIRED_DOC.id))

    expect(confirmButton.disabled).toBe(false)
    expect(document.body.querySelector('.terms-dialog__warning')).toBeNull()
  })

  it('does not require checking optional documents', async () => {
    await renderDialog([REQUIRED_DOC, OPTIONAL_DOC])
    await clickElement(termsCheckbox(document.body, REQUIRED_DOC.id))

    const confirmButton = buttonByText(t('features.activitySignup.terms.confirm'))
    expect(confirmButton.disabled).toBe(false)
  })
})

describe('EventTermsDialog document preview', () => {
  it('hides the content until the document name is opened, and can be closed', async () => {
    await renderDialog([REQUIRED_DOC])

    expect(openDialog(REQUIRED_DOC.name)).toBeUndefined()
    expect(document.body.textContent).not.toContain(REQUIRED_DOC.description)

    await clickElement(buttonByText(REQUIRED_DOC.name))

    const preview = openDialog(REQUIRED_DOC.name)
    expect(preview).toBeDefined()
    expect(textOf(preview?.querySelector('.rich-text') ?? null)).toContain(REQUIRED_DOC.description)

    await clickElement(buttonByText(t('features.activitySignup.terms.close'), preview))

    expect(openDialog(REQUIRED_DOC.name)).toBeUndefined()
  })

  it('checks the document when accepted from the preview and unchecks it when rejected', async () => {
    await renderDialog([REQUIRED_DOC])
    const confirmButton = buttonByText(t('features.activitySignup.terms.confirm'))

    await clickElement(buttonByText(REQUIRED_DOC.name))
    let preview = openDialog(REQUIRED_DOC.name)
    await clickElement(buttonByText(t('features.activitySignup.terms.accept'), preview))

    expect(openDialog(REQUIRED_DOC.name)).toBeUndefined()
    expect(confirmButton.disabled).toBe(false)

    await clickElement(buttonByText(REQUIRED_DOC.name))
    preview = openDialog(REQUIRED_DOC.name)
    await clickElement(buttonByText(t('features.activitySignup.terms.reject'), preview))

    expect(confirmButton.disabled).toBe(true)
  })
})

describe('EventTermsDialog confirm decisions', () => {
  it('sends one decision per pending document: true for checked, false for unchecked optional ones', async () => {
    const { wrapper } = await renderDialog([REQUIRED_DOC, OPTIONAL_DOC])

    await clickElement(termsCheckbox(document.body, REQUIRED_DOC.id))
    await clickElement(buttonByText(t('features.activitySignup.terms.confirm')))

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

    await clickElement(buttonByText(t('features.activitySignup.terms.confirm')))

    expect(wrapper.emitted('confirm')).toEqual([
      [[{ termsDocumentId: OPTIONAL_DOC.id, accepted: false }]],
    ])
  })
})
