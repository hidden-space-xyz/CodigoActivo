import type { Sponsor } from '@/modules/home/domain/entities/sponsor.entity'
import type { SponsorRepository } from '@/modules/home/domain/repositories/sponsor-repository'
import { getApiPartners } from '@/shared/api/generated/endpoints/partners/partners'

export class HttpSponsorRepository implements SponsorRepository {
  async getAll(): Promise<readonly Sponsor[]> {
    const response = await getApiPartners()
    return (response.data ?? [])
      .filter((partner) => partner.id && partner.name)
      .map((partner) => ({ id: partner.id ?? '', name: partner.name ?? '' }))
  }
}
