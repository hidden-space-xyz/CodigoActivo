import {
  deleteApiAnnouncementsAnnouncementId,
  getApiAnnouncements,
  getApiAnnouncementsAnnouncementId,
  getApiAnnouncementsYears,
  patchApiAnnouncementsAnnouncementIdFeature,
  postApiAnnouncements,
  putApiAnnouncementsAnnouncementId,
} from '@/shared/api/generated/endpoints/announcements/announcements'
import type {
  AnnouncementListItemResponse,
  AnnouncementResponse,
  CreateAnnouncementRequest,
  GetApiAnnouncementsParams,
  UpdateAnnouncementRequest,
} from '@/shared/api/generated/models'
import { FEATURED_FIRST_SORT, toPage, unwrapOrNull } from '@/shared/api'
import type { PagedListPage } from '@/shared/lib'

import type { Announcement, AnnouncementSummary, HomeAnnouncements } from '../model/types'
import { toAnnouncement, toAnnouncementSummary } from './mapper'

/** Lists the years that have announcements, as strings for the year selector. */
export async function getAnnouncementYearsRequest(): Promise<readonly string[]> {
  const { data } = await getApiAnnouncementsYears()
  return (data ?? []).map(String)
}

/** Fetches one page of a year's announcements, newest first; an empty `search` is not sent. */
export async function getAnnouncementsByYearPageRequest(
  year: string,
  search: string,
  page: number,
  pageSize: number,
): Promise<PagedListPage<AnnouncementSummary>> {
  const result = await getApiAnnouncements({
    year: Number(year),
    ...(search ? { search } : {}),
    sort: '-createdAt',
    page,
    pageSize,
  })
  const { items, total } = toPage(result)
  return { items: items.map(toAnnouncementSummary), total }
}

/** Fetches four announcements, featured first, and splits off the first one as the highlight. */
export async function getHomeAnnouncementsRequest(): Promise<HomeAnnouncements> {
  const { data } = await getApiAnnouncements({ sort: FEATURED_FIRST_SORT, pageSize: 4 })
  const [featured = null, ...items] = (data.items ?? []).map(toAnnouncementSummary)
  return { featured, items }
}

/** Loads an announcement for the public detail page; resolves `null` on 404. */
export async function getAnnouncementByIdRequest(id: string): Promise<Announcement | null> {
  const response = await unwrapOrNull<AnnouncementResponse>(getApiAnnouncementsAnnouncementId(id))
  return response ? toAnnouncement(response) : null
}

/** Fetches one page of unmapped announcements for the admin table (server-side filters/sort). */
export function getAnnouncementsAdminPageRequest(
  params: GetApiAnnouncementsParams,
): Promise<{ items: AnnouncementListItemResponse[]; total: number }> {
  return getApiAnnouncements(params).then(toPage)
}

/** Loads the unmapped announcement for the admin edit form; resolves `null` on 404. */
export function getAnnouncementAdminRequest(id: string) {
  return unwrapOrNull<AnnouncementResponse>(getApiAnnouncementsAnnouncementId(id))
}

/** Creates an announcement; the response's `data` holds the created item. */
export function createAnnouncementRequest(body: CreateAnnouncementRequest) {
  return postApiAnnouncements(body)
}

/** Replaces an announcement's content (`PUT`). */
export function updateAnnouncementRequest(id: string, body: UpdateAnnouncementRequest) {
  return putApiAnnouncementsAnnouncementId(id, body)
}

/** Deletes an announcement from the admin panel. */
export function deleteAnnouncementRequest(id: string) {
  return deleteApiAnnouncementsAnnouncementId(id)
}

/**
 * Marks the announcement as the featured one. Despite the name this is not a toggle: the API
 * features this announcement and unfeatures every other one.
 */
export function toggleAnnouncementFeatureRequest(id: string) {
  return patchApiAnnouncementsAnnouncementIdFeature(id)
}
