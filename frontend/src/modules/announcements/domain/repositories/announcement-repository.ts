import type { Announcement } from '@/modules/announcements/domain/entities/announcement.entity'

export interface AnnouncementRepository {
  getAll(): Promise<readonly Announcement[]>
}
