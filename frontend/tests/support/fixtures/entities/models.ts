import type { AnnouncementSummary } from '@/entities/announcement/model/types'
import type { PastEvent, UpcomingEvent } from '@/entities/event/model/types'
import type { LearningResourceSummary } from '@/entities/resource/model/types'

/** Upcoming event card model with open signup, two categories and a thumbnail. */
export function buildUpcomingEvent(overrides: Partial<UpcomingEvent> = {}): UpcomingEvent {
  return {
    id: 'event-1',
    title: 'Día Código Activo',
    slogan: 'Programa tu futuro',
    date: '3 oct 2026',
    status: { kind: 'signupOpen', label: 'Inscripción abierta' },
    thumbnailId: 'thumb-1',
    categories: [
      { id: 'cat-1', name: 'Robótica', color: '#ff6b5e' },
      { id: 'cat-2', name: 'IA', color: '' },
    ],
    ...overrides,
  }
}

/** Finished event card model. */
export function buildPastEvent(overrides: Partial<PastEvent> = {}): PastEvent {
  return {
    id: 'event-0',
    title: 'Edición 2025',
    eventName: 'Crea y comparte',
    date: '10 may 2025',
    status: { kind: 'finished', label: 'Finalizado' },
    thumbnailId: 'thumb-0',
    categories: [{ id: 'cat-1', name: 'Robótica', color: '#ff6b5e' }],
    ...overrides,
  }
}

/** Announcement card model. */
export function buildAnnouncementSummary(
  overrides: Partial<AnnouncementSummary> = {},
): AnnouncementSummary {
  return {
    id: 'announcement-1',
    title: 'Abrimos inscripciones',
    subtitle: 'Plazas limitadas',
    date: '15 mar 2026',
    thumbnailId: 'thumb-a',
    featured: false,
    ...overrides,
  }
}

/** Learning resource card model without an external url. */
export function buildResourceSummary(
  overrides: Partial<LearningResourceSummary> = {},
): LearningResourceSummary {
  return {
    id: 'resource-1',
    title: 'Guía de Scratch',
    subtitle: 'Primeros pasos',
    date: '1 feb 2026',
    url: null,
    thumbnailId: 'thumb-r',
    ...overrides,
  }
}
