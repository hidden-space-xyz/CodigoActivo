import type { EventRepository } from '@/modules/events/domain/repositories/event-repository'

import { HttpEventRepository } from './http-event.repository'

export const eventRepository: EventRepository = new HttpEventRepository()
