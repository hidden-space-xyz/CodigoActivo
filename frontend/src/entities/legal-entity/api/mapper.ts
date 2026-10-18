import type { LegalEntityResponse } from '@/shared/api/generated/models'

import type { ContactPhone, LegalEntity } from '../model/types'

function toContactPhone(phone: string | null | undefined): ContactPhone | null {
  return phone ? { text: phone, dialable: phone.replace(/[^\d+]/g, '') } : null
}

/** Maps the legal identity of the association. */
export function toLegalEntity(response: LegalEntityResponse): LegalEntity {
  return {
    name: response.name,
    taxId: response.taxId,
    address: response.address,
    registry: response.registry,
    email: response.email,
    phone: toContactPhone(response.phone),
  }
}
