import { fetchODataEntity, fetchODataList, odataInt } from '@/shared/api'
import type { AnnouncementResponse } from '@/shared/api/generated/models'

import type { Announcement, HomeAnnouncements } from '../model/types'
import { toAnnouncement } from './mapper'

const RESOURCE = 'Announcements'

/** Distinct years (of `createdAt`) that have announcements, as strings, numeric DESC. */
export async function getAnnouncementYearsRequest(): Promise<readonly string[]> {
  const { items } = await fetchODataList<AnnouncementResponse>(RESOURCE, {
    orderBy: 'createdAt desc',
    select: 'createdAt',
    top: 1000,
  })

  const years = new Set<string>()
  for (const item of items) {
    if (!item.createdAt) continue
    const date = new Date(item.createdAt)
    if (Number.isNaN(date.getTime())) continue
    // createdAt is a UTC DateTimeOffset and the list is filtered server-side with `year(createdAt)`
    // (evaluated in UTC), so derive the pill year in UTC too — otherwise a boundary announcement
    // lands under a year whose filter returns nothing in negative-UTC timezones.
    years.add(String(date.getUTCFullYear()))
  }

  return [...years].sort((a, b) => Number(b) - Number(a))
}

/** Announcements created in the given year, newest first. */
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

/** The featured announcement, falling back to the most recent one; null when there are none. */
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

/** Home block: one featured announcement plus up to three other recent ones. */
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

/** A single announcement by id; null on 404. */
export async function getAnnouncementByIdRequest(id: string): Promise<Announcement | null> {
  const response = await fetchODataEntity<AnnouncementResponse>(RESOURCE, id)
  return response ? toAnnouncement(response) : null
}
