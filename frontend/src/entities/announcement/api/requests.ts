import {
  getApiAnnouncements,
  getApiAnnouncementsAnnouncementId,
  getApiAnnouncementsYears,
} from '@/shared/api/generated/endpoints/announcements/announcements'
import type {
  AnnouncementListItemResponse,
  AnnouncementResponse,
} from '@/shared/api/generated/models'
import { FEATURED_FIRST_SORT, fetchAllPages, toPage, unwrapOrNull } from '@/shared/api'

import type { Announcement, AnnouncementSummary, HomeAnnouncements } from '../model/types'
import { toAnnouncement, toAnnouncementSummary } from './mapper'

export async function getAnnouncementYearsRequest(): Promise<readonly string[]> {
  const { data } = await getApiAnnouncementsYears()
  return (data ?? []).map(String)
}

export async function getAnnouncementsByYearRequest(
  year: string,
): Promise<readonly AnnouncementSummary[]> {
  const items = await fetchAllPages<AnnouncementListItemResponse>((page, pageSize) =>
    getApiAnnouncements({ year: Number(year), sort: '-createdAt', page, pageSize }).then(toPage),
  )
  return items.map(toAnnouncementSummary)
}

export async function getHomeAnnouncementsRequest(): Promise<HomeAnnouncements> {
  // Single featured-first read: the first item is the featured announcement (or the latest one
  // when none is featured) and the most recent items backfill the rest of the section.
  const { data } = await getApiAnnouncements({ sort: FEATURED_FIRST_SORT, pageSize: 4 })
  const [featured = null, ...items] = (data.items ?? []).map(toAnnouncementSummary)
  return { featured, items }
}

export async function getAnnouncementByIdRequest(id: string): Promise<Announcement | null> {
  const response = await unwrapOrNull<AnnouncementResponse>(getApiAnnouncementsAnnouncementId(id))
  return response ? toAnnouncement(response) : null
}
