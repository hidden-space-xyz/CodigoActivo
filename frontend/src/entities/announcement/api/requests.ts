import {
  getApiAnnouncements,
  getApiAnnouncementsAnnouncementId,
} from '@/shared/api/generated/endpoints/announcements/announcements'
import { ApiError } from '@/shared/api'

import type { Announcement } from '../model/types'
import { toAnnouncement } from './mapper'

export async function getAnnouncementsRequest(): Promise<readonly Announcement[]> {
  const response = await getApiAnnouncements()
  return (response.data ?? [])
    .slice()
    .sort((a, b) => (b.createdAt ?? '').localeCompare(a.createdAt ?? ''))
    .map(toAnnouncement)
}

export async function getAnnouncementByIdRequest(id: string): Promise<Announcement | null> {
  try {
    const response = await getApiAnnouncementsAnnouncementId(id)
    return toAnnouncement(response.data)
  } catch (error) {
    if (error instanceof ApiError && error.status === 404) return null
    throw error
  }
}
