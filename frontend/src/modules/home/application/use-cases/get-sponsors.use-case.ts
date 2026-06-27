import type { Sponsor } from '@/modules/home/domain/entities/sponsor.entity'
import type { SponsorRepository } from '@/modules/home/domain/repositories/sponsor-repository'

export function getSponsors(repository: SponsorRepository): Promise<readonly Sponsor[]> {
  return repository.getAll()
}
