import type { LegalEntityResponse } from '@/shared/api/generated/models'

import type { LegalEntity } from '../model/types'

/** Maps the legal identity of the association. */
export function toLegalEntity(response: LegalEntityResponse): LegalEntity {
  return {
    name: response.name,
    taxId: response.taxId,
    address: response.address,
    registry: response.registry,
  }
}
