import {
  deleteApiPartnersPartnerId,
  getApiPartners,
  postApiPartners,
  putApiPartnersPartnerId,
} from '@/shared/api/generated/endpoints/partners/partners'
import { toPage } from '@/shared/api'
import { MAX_PAGE_SIZE } from '@/shared/config'
import type { ServerTablePage } from '@/shared/lib/paging'

import type { Partner, PartnerInput, PartnerListParams, Sponsor } from '../model/types'
import { toPartner, toPartnerRequest, toSponsor } from './mapper'

/** Loads up to 100 partners ordered by tier then newest, dropping entries without id or name. */
export async function getSponsorsRequest(): Promise<readonly Sponsor[]> {
  const response = await getApiPartners({ pageSize: MAX_PAGE_SIZE, sort: 'tier,-fromDate' })
  return response.data.items.filter((partner) => partner.id && partner.name).map(toSponsor)
}

/** Fetches one page of the admin partner list. */
export async function getPartnersPageRequest(
  params: PartnerListParams,
): Promise<ServerTablePage<Partner>> {
  return toPage(await getApiPartners(params), toPartner)
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
