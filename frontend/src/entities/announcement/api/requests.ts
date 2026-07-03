import { fetchODataEntity, fetchODataList, odataInt } from '@/shared/api'
import type { AnnouncementResponse } from '@/shared/api/generated/models'

import type { Announcement, HomeAnnouncements } from '../model/types'
import { toAnnouncement } from './mapper'

const RESOURCE = 'Announcements'

export async function getAnnouncementYearsRequest(): Promise<readonly string[]> {
  const { items } = await fetchODataList<AnnouncementResponse>(RESOURCE, {
    orderBy: 'createdAt desc',
    select: 'createdAt',
    top: 100,
  })

  const years = new Set<string>()
  for (const item of items) {
    if (!item.createdAt) continue
    const date = new Date(item.createdAt)
    if (Number.isNaN(date.getTime())) continue
    years.add(String(date.getUTCFullYear()))
  }

  return [...years].sort((a, b) => Number(b) - Number(a))
}

export async function getAnnouncementsByYearRequest(
  year: string,
): Promise<readonly Announcement[]> {
  const { items } = await fetchODataList<AnnouncementResponse>(RESOURCE, {
    filter: `year(createdAt) eq ${odataInt(year)}`,
    orderBy: 'createdAt desc',
    top: 500,
  })
  return items.map(toAnnouncement)
}

async function getFeaturedAnnouncementRequest(): Promise<Announcement | null> {
  const featured = await fetchODataList<AnnouncementResponse>(RESOURCE, {
    filter: 'featured eq true',
    orderBy: 'createdAt desc',
    top: 1,
  })
  if (featured.items[0]) return toAnnouncement(featured.items[0])

  const latest = await fetchODataList<AnnouncementResponse>(RESOURCE, {
    orderBy: 'createdAt desc',
    top: 1,
  })
  return latest.items[0] ? toAnnouncement(latest.items[0]) : null
}

export async function getHomeAnnouncementsRequest(): Promise<HomeAnnouncements> {
  const featured = await getFeaturedAnnouncementRequest()

  const recent = await fetchODataList<AnnouncementResponse>(RESOURCE, {
    orderBy: 'createdAt desc',
    top: 4,
  })

  const items = recent.items
    .map(toAnnouncement)
    .filter((announcement) => announcement.id !== featured?.id)
    .slice(0, 3)

  return { featured, items }
}

export async function getAnnouncementByIdRequest(id: string): Promise<Announcement | null> {
  const response = await fetchODataEntity<AnnouncementResponse>(RESOURCE, id)
  return response ? toAnnouncement(response) : null
}
