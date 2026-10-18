import { getApiLegalEntity } from '@/shared/api/generated/endpoints/legal/legal'

import type { LegalEntity } from '../model/types'
import { toLegalEntity } from './mapper'

/** Loads the legal identity configured for the deployment. */
export async function getLegalEntityRequest(): Promise<LegalEntity> {
  const response = await getApiLegalEntity()
  return toLegalEntity(response.data)
}
