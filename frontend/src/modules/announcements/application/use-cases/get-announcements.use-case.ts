import type { Announcement } from '@/modules/announcements/domain/entities/announcement.entity'
import type { AnnouncementRepository } from '@/modules/announcements/domain/repositories/announcement-repository'

export function getAnnouncements(
  repository: AnnouncementRepository,
): Promise<readonly Announcement[]> {
  return repository.getAll()
}
