import type { Sponsor } from '@/modules/home/domain/entities/sponsor.entity'

export interface SponsorRepository {
  getAll(): Promise<readonly Sponsor[]>
}
