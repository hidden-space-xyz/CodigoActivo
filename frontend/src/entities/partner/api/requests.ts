import { getApiPartners } from '@/shared/api/generated/endpoints/partners/partners'
import { toPage } from '@/shared/api'

import type { Sponsor } from '../model/types'

export async function getSponsorsRequest(): Promise<readonly Sponsor[]> {
  const { items } = await getApiPartners({ pageSize: 100, sort: 'tier,-fromDate' }).then(toPage)
  return items
    .filter((partner) => partner.id && partner.name)
    .map((partner) => ({
      id: partner.id ?? '',
      name: partner.name ?? '',
      website: partner.website ?? '',
      thumbnailId: partner.thumbnailId ?? '',
    }))
}
