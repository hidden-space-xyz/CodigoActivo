import { describe, expect, it } from 'vitest'

import type { EventTermsDocumentState } from '@/entities/event'
import { asksForSignature, readTermsDecisions } from '@/pages/event-detail/model/terms-decisions'

function terms(
  id: string,
  options: { required?: boolean; requiresSignature?: boolean } = {},
): EventTermsDocumentState {
  return {
    id,
    name: id,
    description: '{}',
    required: options.required ?? false,
    requiresSignature: options.requiresSignature ?? false,
    displayOrder: 0,
    accepted: null,
    decidedAt: null,
  }
}

const SIGNATURE = 'data:image/png;base64,AAAA'

describe('asksForSignature', () => {
  it('is true only for a checked document that asks for a signature', () => {
    const signed = terms('signed', { requiresSignature: true })
    const plain = terms('plain')

    expect(asksForSignature(signed, { checked: { signed: true }, signatures: {} })).toBe(true)
    expect(asksForSignature(signed, { checked: { signed: false }, signatures: {} })).toBe(false)
    expect(asksForSignature(plain, { checked: { plain: true }, signatures: {} })).toBe(false)
  })
})

describe('readTermsDecisions', () => {
  const required = terms('required', { required: true })
  const optional = terms('optional')
  const signed = terms('signed', { requiresSignature: true })

  it('reports an unchecked required document', () => {
    const reading = readTermsDecisions([required, optional], {
      checked: { optional: true },
      signatures: {},
    })

    expect(reading).toEqual({ missingRequired: true, missingSignature: false, decisions: null })
  })

  it('reports a checked document that asks for a signature without one', () => {
    const reading = readTermsDecisions([required, signed], {
      checked: { signed: true },
      signatures: { signed: '' },
    })

    expect(reading).toEqual({ missingRequired: true, missingSignature: true, decisions: null })
  })

  it('decides every document and sends signatures only where they are asked for', () => {
    const reading = readTermsDecisions([required, optional, signed], {
      checked: { required: true, signed: true },
      signatures: { required: SIGNATURE, signed: SIGNATURE },
    })

    expect(reading.missingRequired).toBe(false)
    expect(reading.missingSignature).toBe(false)
    expect(reading.decisions).toEqual([
      { termsDocumentId: 'required', accepted: true },
      { termsDocumentId: 'optional', accepted: false },
      { termsDocumentId: 'signed', accepted: true, signature: SIGNATURE },
    ])
  })

  it('needs no signature for an unchecked optional document that asks for one', () => {
    const reading = readTermsDecisions([signed], { checked: {}, signatures: {} })

    expect(reading.decisions).toEqual([{ termsDocumentId: 'signed', accepted: false }])
  })
})
