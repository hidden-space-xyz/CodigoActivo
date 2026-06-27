import type { Announcement } from '@/modules/announcements/domain/entities/announcement.entity'
import type { AnnouncementRepository } from '@/modules/announcements/domain/repositories/announcement-repository'
import { toAnnouncement } from '@/modules/announcements/infrastructure/mappers/announcement.mapper'
import {
  getApiAnnouncements,
  getApiAnnouncementsAnnouncementId,
} from '@/shared/api/generated/endpoints/announcements/announcements'
import { ApiError } from '@/shared/api/http-client'

export class HttpAnnouncementRepository implements AnnouncementRepository {
  async getAll(): Promise<readonly Announcement[]> {
    const response = await getApiAnnouncements()
    return (response.data ?? []).map(toAnnouncement)
  }

  async getById(id: string): Promise<Announcement | null> {
    try {
      const response = await getApiAnnouncementsAnnouncementId(id)
      return toAnnouncement(response.data)
    } catch (error) {
      if (error instanceof ApiError && error.status === 404) return null
      throw error
    }
  }
}
