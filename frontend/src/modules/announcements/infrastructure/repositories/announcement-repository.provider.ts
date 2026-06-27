import type { AnnouncementRepository } from '@/modules/announcements/domain/repositories/announcement-repository'

import { HttpAnnouncementRepository } from './http-announcement.repository'

export const announcementRepository: AnnouncementRepository = new HttpAnnouncementRepository()
