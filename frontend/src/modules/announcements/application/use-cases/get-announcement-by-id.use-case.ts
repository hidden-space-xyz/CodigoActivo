import type { Announcement } from '@/modules/announcements/domain/entities/announcement.entity'
import type { AnnouncementRepository } from '@/modules/announcements/domain/repositories/announcement-repository'

export function getAnnouncementById(
  repository: AnnouncementRepository,
  id: string,
): Promise<Announcement | null> {
  return repository.getById(id)
}
