import {
  getApiAnnouncements,
  getApiAnnouncementsAnnouncementId,
  getApiAnnouncementsYears,
} from '@/shared/api/generated/endpoints/announcements/announcements'
import type { AnnouncementResponse } from '@/shared/api/generated/models'
import { fetchAllPages, toPage, unwrapOrNull } from '@/shared/api'

import type { Announcement, HomeAnnouncements } from '../model/types'
import { toAnnouncement } from './mapper'

export async function getAnnouncementYearsRequest(): Promise<readonly string[]> {
  const { data } = await getApiAnnouncementsYears()
  return (data ?? []).map(String)
}

export async function getAnnouncementsByYearRequest(
  year: string,
): Promise<readonly Announcement[]> {
  const items = await fetchAllPages<AnnouncementResponse>((page, pageSize) =>
    getApiAnnouncements({ year: Number(year), sort: '-createdAt', page, pageSize }).then(toPage),
  )
  return items.map(toAnnouncement)
}

async function getFeaturedAnnouncementRequest(): Promise<Announcement | null> {
  const featured = await getApiAnnouncements({ featured: true, sort: '-createdAt', pageSize: 1 })
  const first = featured.data.items?.[0]
  if (first) return toAnnouncement(first)

  const latest = await getApiAnnouncements({ sort: '-createdAt', pageSize: 1 })
  const latestFirst = latest.data.items?.[0]
  return latestFirst ? toAnnouncement(latestFirst) : null
}

export async function getHomeAnnouncementsRequest(): Promise<HomeAnnouncements> {
  const [featured, recent] = await Promise.all([
    getFeaturedAnnouncementRequest(),
    getApiAnnouncements({ sort: '-createdAt', pageSize: 4 }),
  ])

  const items = (recent.data.items ?? [])
    .map(toAnnouncement)
    .filter((announcement) => announcement.id !== featured?.id)
    .slice(0, 3)

  return { featured, items }
}

export async function getAnnouncementByIdRequest(id: string): Promise<Announcement | null> {
  const response = await unwrapOrNull<AnnouncementResponse>(getApiAnnouncementsAnnouncementId(id))
  return response ? toAnnouncement(response) : null
}
