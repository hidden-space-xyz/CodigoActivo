import type { Announcement } from '@/modules/announcements/domain/entities/announcement.entity'
import type { AnnouncementRepository } from '@/modules/announcements/domain/repositories/announcement-repository'
import { toAnnouncement } from '@/modules/announcements/infrastructure/mappers/announcement.mapper'
import { getApiAnnouncements } from '@/shared/api/generated/endpoints/announcements/announcements'

export class HttpAnnouncementRepository implements AnnouncementRepository {
  async getAll(): Promise<readonly Announcement[]> {
    const response = await getApiAnnouncements()
    return (response.data ?? []).map(toAnnouncement)
  }
}
