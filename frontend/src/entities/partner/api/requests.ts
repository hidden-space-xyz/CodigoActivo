import { fetchODataList } from '@/shared/api'
import type { PartnerResponse } from '@/shared/api/generated/models'

import type { Sponsor } from '../model/types'

export async function getSponsorsRequest(): Promise<readonly Sponsor[]> {
  const page = await fetchODataList<PartnerResponse>('Partners', {
    orderBy: 'tier asc,fromDate desc',
    top: 1000,
  })
  return page.items
    .filter((partner) => partner.id && partner.name)
    .map((partner) => ({
      id: partner.id ?? '',
      name: partner.name ?? '',
      website: partner.website ?? '',
      thumbnailId: partner.thumbnailId ?? '',
    }))
}
