import { getApiPartners } from '@/shared/api/generated/endpoints/partners/partners'

import type { Sponsor } from '../model/types'

export async function getSponsorsRequest(): Promise<readonly Sponsor[]> {
  const response = await getApiPartners()
  return (response.data ?? [])
    .filter((partner) => partner.id && partner.name)
    .map((partner) => ({
      id: partner.id ?? '',
      name: partner.name ?? '',
      website: partner.website ?? '',
      thumbnailId: partner.thumbnailId ?? '',
    }))
}
