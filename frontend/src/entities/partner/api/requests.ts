import {
  deleteApiPartnersPartnerId,
  getApiPartners,
  postApiPartners,
  putApiPartnersPartnerId,
} from '@/shared/api/generated/endpoints/partners/partners'
import { toPage } from '@/shared/api'
import type { ServerTablePage } from '@/shared/lib/paging'

import type { Partner, PartnerInput, PartnerListParams, Sponsor } from '../model/types'
import { toPartner, toPartnerRequest, toSponsor } from './mapper'

/** Loads up to 100 partners ordered by tier then newest, dropping entries without id or name. */
export async function getSponsorsRequest(): Promise<readonly Sponsor[]> {
  const { items } = toPage(await getApiPartners({ pageSize: 100, sort: 'tier,-fromDate' }))
  return items.filter((partner) => partner.id && partner.name).map(toSponsor)
}

/** Fetches one page of the admin partner list. */
export async function getPartnersPageRequest(
  params: PartnerListParams,
): Promise<ServerTablePage<Partner>> {
  const { items, total } = toPage(await getApiPartners(params))
  return { items: items.map(toPartner), total }
}

/** Creates a partner. */
export async function createPartnerRequest(input: PartnerInput): Promise<void> {
  await postApiPartners(toPartnerRequest(input))
}

/** Replaces a partner. */
export async function updatePartnerRequest(id: string, input: PartnerInput): Promise<void> {
  await putApiPartnersPartnerId(id, toPartnerRequest(input))
}

/** Deletes a partner. */
export async function deletePartnerRequest(id: string): Promise<void> {
  await deleteApiPartnersPartnerId(id)
}
