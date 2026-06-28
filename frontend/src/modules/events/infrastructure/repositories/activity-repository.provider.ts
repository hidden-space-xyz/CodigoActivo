import type { ActivityRepository } from '@/modules/events/domain/repositories/activity-repository'

import { HttpActivityRepository } from './http-activity.repository'

export const activityRepository: ActivityRepository = new HttpActivityRepository()
