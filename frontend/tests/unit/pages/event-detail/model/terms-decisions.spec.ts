import { describe, expect, it } from 'vitest'

import type { EventTermsDocumentState, EventTermsPerson } from '@/entities/event'
import {
  asksForSignature,
  draftKey,
  pendingDocuments,
  readTermsDecisions,
  termsSections,
  type TermsDialogSection,
} from '@/pages/event-detail/model/terms-decisions'

function terms(
  id: string,
  options: { required?: boolean; requiresSignature?: boolean; consent?: boolean } = {},
): EventTermsDocumentState {
  return {
    id,
    name: id,
    description: '{}',
    kind: options.consent ? 'Consent' : 'Conditions',
    required: options.required ?? false,
    requiresSignature: options.requiresSignature ?? false,
    displayOrder: 0,
  }
}

function person(
  id: string,
  options: { decidesOwnConsents?: boolean; accepted?: string[]; declined?: string[] } = {},
): EventTermsPerson {
  return {
    id,
    firstName: id,
    lastName: 'Ruiz',
    decidesOwnConsents: options.decidesOwnConsents ?? false,
    decisions: [
      ...(options.accepted ?? []).map((documentId) => ({
        documentId,
        accepted: true,
        decidedAt: '2026-01-01T00:00:00Z',
      })),
      ...(options.declined ?? []).map((documentId) => ({
        documentId,
        accepted: false,
        decidedAt: '2026-01-01T00:00:00Z',
      })),
    ],
  }
}

function section(
  personId: string,
  documents: EventTermsDocumentState[],
  self = false,
): TermsDialogSection {
  return {
    personId,
    name: personId,
    self,
    items: documents.map((document) => ({ document, decider: 'user' })),
  }
}

const SIGNATURE = 'data:image/png;base64,AAAA'

describe('pendingDocuments', () => {
  it('leaves out the documents already accepted for the person and keeps the declined ones', () => {
    const rules = terms('rules', { required: true })
    const images = terms('images', { consent: true })
    const news = terms('news')

    const pending = pendingDocuments(
      [rules, images, news],
      person('leo', { accepted: ['rules'], declined: ['images'] }),
    )

    expect(pending).toEqual([images, news])
    expect(pendingDocuments([rules], undefined)).toEqual([rules])
  })
})

describe('termsSections', () => {
  const rules = terms('rules', { required: true })
  const images = terms('images', { consent: true })

  it('names who decides each document and leaves out people with nothing pending', () => {
    const sections = termsSections(
      [rules, images],
      [
        person('ada', { decidesOwnConsents: true }),
        person('bea', { decidesOwnConsents: true }),
        person('leo'),
        person('max', { accepted: ['rules', 'images'] }),
      ],
      ['ada', 'bea', 'leo', 'max'],
      'ada',
      () => '',
    )

    expect(
      sections.map((entry) => [
        entry.personId,
        entry.self,
        entry.items.map((item) => `${item.document.id}:${item.decider}`),
      ]),
    ).toEqual([
      ['ada', true, ['rules:user', 'images:user']],
      ['bea', false, ['rules:user', 'images:person']],
      ['leo', false, ['rules:user', 'images:user']],
    ])
  })

  it('names a person missing from the terms state with the fallback and lets the user decide', () => {
    const sections = termsSections([images], [], ['new'], 'ada', () => 'Nuevo')

    expect(sections).toEqual([
      {
        personId: 'new',
        name: 'Nuevo',
        self: false,
        items: [{ document: images, decider: 'user' }],
      },
    ])
  })
})

describe('asksForSignature', () => {
  it('is true only for a document that asks for a signature and is checked for that person', () => {
    const signed = terms('signed', { requiresSignature: true })
    const plain = terms('plain')
    const checked = { [draftKey('ada', 'signed')]: true, [draftKey('ada', 'plain')]: true }

    expect(asksForSignature('ada', signed, { checked, signatures: {} })).toBe(true)
    expect(asksForSignature('leo', signed, { checked, signatures: {} })).toBe(false)
    expect(asksForSignature('ada', plain, { checked, signatures: {} })).toBe(false)
  })
})

describe('readTermsDecisions', () => {
  const required = terms('required', { required: true })
  const optional = terms('optional')
  const signed = terms('signed', { requiresSignature: true })

  it('reports a required document left unchecked for anyone', () => {
    const reading = readTermsDecisions(
      [section('ada', [required], true), section('leo', [required])],
      { checked: { [draftKey('ada', 'required')]: true }, signatures: {} },
    )

    expect(reading).toEqual({ missingRequired: true, missingSignature: false, decisions: null })
  })

  it('reports a checked document that asks for a signature without one drawn', () => {
    const reading = readTermsDecisions([section('ada', [signed], true)], {
      checked: { [draftKey('ada', 'signed')]: true },
      signatures: {},
    })

    expect(reading).toEqual({ missingRequired: false, missingSignature: true, decisions: null })
  })

  it('returns the decisions of each person, with a signature only where one was asked for', () => {
    const reading = readTermsDecisions(
      [section('ada', [required, optional], true), section('leo', [required, signed])],
      {
        checked: {
          [draftKey('ada', 'required')]: true,
          [draftKey('leo', 'required')]: true,
          [draftKey('leo', 'signed')]: true,
        },
        signatures: {
          [draftKey('leo', 'signed')]: SIGNATURE,
          [draftKey('ada', 'optional')]: SIGNATURE,
        },
      },
    )

    expect(reading.decisions).toEqual({
      ada: [
        { termsDocumentId: 'required', accepted: true },
        { termsDocumentId: 'optional', accepted: false },
      ],
      leo: [
        { termsDocumentId: 'required', accepted: true },
        { termsDocumentId: 'signed', accepted: true, signature: SIGNATURE },
      ],
    })
  })
})
