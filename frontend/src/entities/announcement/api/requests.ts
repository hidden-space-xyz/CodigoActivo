import {
  getApiAnnouncements,
  getApiAnnouncementsAnnouncementId,
  getApiAnnouncementsYears,
} from '@/shared/api/generated/endpoints/announcements/announcements'
import type { AnnouncementResponse } from '@/shared/api/generated/models'
import { FEATURED_FIRST_SORT, toPage, unwrapOrNull } from '@/shared/api'
import type { PagedListPage } from '@/shared/lib'

import type { Announcement, AnnouncementSummary, HomeAnnouncements } from '../model/types'
import { toAnnouncement, toAnnouncementSummary } from './mapper'

export async function getAnnouncementYearsRequest(): Promise<readonly string[]> {
  const { data } = await getApiAnnouncementsYears()
  return (data ?? []).map(String)
}

export async function getAnnouncementsByYearPageRequest(
  year: string,
  page: number,
  pageSize: number,
): Promise<PagedListPage<AnnouncementSummary>> {
  const result = await getApiAnnouncements({
    year: Number(year),
    sort: '-createdAt',
    page,
    pageSize,
  })
  const { items, total } = toPage(result)
  return { items: items.map(toAnnouncementSummary), total }
}

export async function getHomeAnnouncementsRequest(): Promise<HomeAnnouncements> {
  const { data } = await getApiAnnouncements({ sort: FEATURED_FIRST_SORT, pageSize: 4 })
  const [featured = null, ...items] = (data.items ?? []).map(toAnnouncementSummary)
  return { featured, items }
}

export async function getAnnouncementByIdRequest(id: string): Promise<Announcement | null> {
  const response = await unwrapOrNull<AnnouncementResponse>(getApiAnnouncementsAnnouncementId(id))
  return response ? toAnnouncement(response) : null
}
