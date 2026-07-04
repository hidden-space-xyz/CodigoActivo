import { getApiPartners } from '@/shared/api/generated/endpoints/partners/partners'
import type { PartnerResponse } from '@/shared/api/generated/models'
import { fetchAllPages, toPage } from '@/shared/api'

import type { Sponsor } from '../model/types'

export async function getSponsorsRequest(): Promise<readonly Sponsor[]> {
  const items = await fetchAllPages<PartnerResponse>((page, pageSize) =>
    getApiPartners({ sort: 'tier,-fromDate', page, pageSize }).then(toPage),
  )
  return items
    .filter((partner) => partner.id && partner.name)
    .map((partner) => ({
      id: partner.id ?? '',
      name: partner.name ?? '',
      website: partner.website ?? '',
      thumbnailId: partner.thumbnailId ?? '',
    }))
}
