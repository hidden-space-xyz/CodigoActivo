import type { CreatePartnerRequest, PartnerResponse } from '@/shared/api/generated/models'
import { toWebUrl } from '@/shared/lib/navigation'

import type { Partner, PartnerInput, Sponsor } from '../model/types'

/** Maps a partner of the admin list; a missing or non-http(s) website becomes `null`. */
export function toPartner(partner: PartnerResponse): Partner {
  return {
    id: partner.id,
    name: partner.name,
    fromDate: partner.fromDate,
    tier: partner.tier,
    website: toWebUrl(partner.website),
    thumbnailId: partner.thumbnailId,
  }
}

/** Maps a partner shown as a sponsor; a missing or non-http(s) website becomes empty text. */
export function toSponsor(partner: PartnerResponse): Sponsor {
  return {
    id: partner.id,
    name: partner.name,
    website: toWebUrl(partner.website) ?? '',
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
