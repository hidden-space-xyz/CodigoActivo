import type { CreatePartnerRequest, PartnerResponse } from '@/shared/api/generated/models'

import type { Partner, PartnerInput, Sponsor } from '../model/types'

/** Maps a partner of the admin list; a missing website becomes `null`. */
export function toPartner(partner: PartnerResponse): Partner {
  return {
    id: partner.id,
    name: partner.name,
    fromDate: partner.fromDate,
    tier: partner.tier,
    website: partner.website ?? null,
    thumbnailId: partner.thumbnailId,
  }
}

/** Maps a partner shown as a sponsor; a missing website becomes empty text. */
export function toSponsor(partner: PartnerResponse): Sponsor {
  return {
    id: partner.id,
    name: partner.name,
    website: partner.website ?? '',
    thumbnailId: partner.thumbnailId,
  }
}

/** Builds the body that creates or replaces a partner; both endpoints take the same fields. */
export function toPartnerRequest(input: PartnerInput): CreatePartnerRequest {
  return {
    name: input.name,
    fromDate: input.fromDate,
    tier: input.tier,
    website: input.website,
    thumbnailId: input.thumbnailId,
  }
}
