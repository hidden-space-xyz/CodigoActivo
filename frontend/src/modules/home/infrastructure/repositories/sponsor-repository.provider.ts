import type { SponsorRepository } from '@/modules/home/domain/repositories/sponsor-repository'

import { HttpSponsorRepository } from './http-sponsor.repository'

export const sponsorRepository: SponsorRepository = new HttpSponsorRepository()
