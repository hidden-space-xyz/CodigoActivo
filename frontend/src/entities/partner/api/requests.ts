import { getApiPartners } from '@/shared/api/generated/endpoints/partners/partners'

import type { Sponsor } from '../model/types'

export async function getSponsorsRequest(): Promise<readonly Sponsor[]> {
  const { data } = await getApiPartners({ sort: 'tier,-fromDate', pageSize: 100 })
  return (data.items ?? [])
    .filter((partner) => partner.id && partner.name)
    .map((partner) => ({
      id: partner.id ?? '',
      name: partner.name ?? '',
      website: partner.website ?? '',
      thumbnailId: partner.thumbnailId ?? '',
    }))
}
