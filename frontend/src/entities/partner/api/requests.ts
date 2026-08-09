import {
  deleteApiPartnersPartnerId,
  getApiPartners,
  postApiPartners,
  putApiPartnersPartnerId,
} from '@/shared/api/generated/endpoints/partners/partners'
import type {
  CreatePartnerRequest,
  GetApiPartnersParams,
  PartnerResponse,
  UpdatePartnerRequest,
} from '@/shared/api/generated/models'
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

export function getPartnersPageRequest(
  params: GetApiPartnersParams,
): Promise<{ items: PartnerResponse[]; total: number }> {
  return getApiPartners(params).then(toPage)
}

export function createPartnerRequest(body: CreatePartnerRequest) {
  return postApiPartners(body).then((r) => r.data)
}

export function updatePartnerRequest(id: string, body: UpdatePartnerRequest) {
  return putApiPartnersPartnerId(id, body).then((r) => r.data)
}

export function deletePartnerRequest(id: string) {
  return deleteApiPartnersPartnerId(id)
}
