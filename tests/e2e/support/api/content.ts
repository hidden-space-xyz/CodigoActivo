import { readFile } from 'node:fs/promises'
import type { ActivityResponse } from '@contract/activityResponse'
import type { CreateActivityRequest } from '@contract/createActivityRequest'
import type { CreateEventRequest } from '@contract/createEventRequest'
import type { EventCategoryTypeResponse } from '@contract/eventCategoryTypeResponse'
import type { EventResponse } from '@contract/eventResponse'
import type { FileResponse } from '@contract/fileResponse'
import type { LegalPolicyKind } from '@contract/legalPolicyKind'
import type { NewsItemResponse } from '@contract/newsItemResponse'
import type { PartnerResponse } from '@contract/partnerResponse'
import type { ResourceResponse } from '@contract/resourceResponse'
import type { TermsDocumentKind } from '@contract/termsDocumentKind'
import type { TermsDocumentResponse } from '@contract/termsDocumentResponse'
import type { UpdateActivityRequest } from '@contract/updateActivityRequest'
import type { UpdateEventRequest } from '@contract/updateEventRequest'

import { DAY_MS, dayFromToday, daysBetween, instantOn } from '../data/time'
import { uniqueTitle } from '../data/unique'
import type { ApiClient } from './api-client'
import { CATALOG, type RoleName, type SignupStatusName } from './catalogs'
import { signUp } from './signups'

const THUMBNAIL = new URL('../assets/thumbnail.png', import.meta.url)

/** When an event takes signups relative to now. */
export type SignupWindowName = 'open' | 'early' | 'upcoming' | 'closed'

/** A terms document linked to an event. */
interface EventTerms {
  readonly document: TermsDocumentResponse
  readonly required: boolean
}

/** What an event looks like; every field has a sensible default. */
export interface EventOptions {
  readonly title?: string
  readonly subtitle?: string
  readonly description?: string
  readonly startsIn?: number
  readonly lastsDays?: number
  readonly signup?: SignupWindowName
  readonly categories?: readonly EventCategoryTypeResponse[]
  readonly terms?: readonly EventTerms[]
}

/** What an activity looks like; every field has a sensible default. */
export interface ActivityOptions {
  readonly title?: string
  readonly description?: string
  readonly location?: string
  readonly dayOfEvent?: number
  readonly startHour?: number
  readonly hours?: number
  readonly startsAt?: Date
  readonly endsAt?: Date
  readonly online?: boolean
  readonly roles?: Partial<Readonly<Record<RoleName, number>>>
}

/** A terms document to create. */
export interface TermsDocumentOptions {
  readonly kind?: TermsDocumentKind
  readonly requiresSignature?: boolean
  readonly name?: string
  readonly text?: string
}

/** A resource to create: internal ones carry text, external ones a link. */
export interface ResourceOptions {
  readonly title?: string
  readonly subtitle?: string
  readonly url?: string
}

/** A partner to create. */
export interface PartnerOptions {
  readonly name?: string
  readonly tier?: number
  readonly website?: string
}

/** A news item to create. */
export interface NewsItemOptions {
  readonly title?: string
  readonly subtitle?: string
  readonly text?: string
}

/** The rich text document the editor saves for a single paragraph of `text`. */
function richText(text: string): string {
  return JSON.stringify({
    type: 'doc',
    content: [{ type: 'paragraph', content: [{ type: 'text', text }] }],
  })
}

function signupWindow(window: SignupWindowName, firstDay: string, lastDay: string) {
  const now = Date.now()
  const iso = (instant: number) => new Date(instant).toISOString()
  if (window === 'closed') {
    const endsAt = Math.min(now - DAY_MS, instantOn(firstDay, 20).getTime() - DAY_MS)
    return { signupStartsAt: iso(endsAt - 10 * DAY_MS), signupEndsAt: iso(endsAt) }
  }
  const endsAt = Math.min(now + 10 * DAY_MS, instantOn(lastDay, 23).getTime())
  if (window === 'open') return { signupStartsAt: iso(now - DAY_MS), signupEndsAt: iso(endsAt) }
  return {
    ...(window === 'early' ? { earlySignupStartsAt: iso(now - DAY_MS) } : {}),
    signupStartsAt: iso(now + Math.min(3 * DAY_MS, (endsAt - now) / 2)),
    signupEndsAt: iso(endsAt),
  }
}

/** Creates an event category with a unique name unless one is given. */
export function createCategory(
  admin: ApiClient,
  name = uniqueTitle('Categoría'),
  color = '#2DD4D9',
): Promise<EventCategoryTypeResponse> {
  return admin.post('/api/events/categoryType', { name, color })
}

function resourceRequest(thumbnailId: string, options: ResourceOptions) {
  const external = options.url !== undefined
  return {
    title: options.title ?? uniqueTitle('Recurso'),
    subtitle: options.subtitle ?? 'Material creado por la suite e2e',
    description: external ? null : richText('Guía paso a paso para el primer programa.'),
    url: options.url ?? null,
    resourceTypeId: external ? CATALOG.resourceTypes.external : CATALOG.resourceTypes.internal,
    thumbnailId,
  }
}

/**
 * Uploads the image a worker's seeders share and returns its id. The API limits uploads per user
 * and deletes a file as soon as nothing references it, so a resource that stays keeps it alive
 * while tests create and delete the content that uses it.
 */
export async function uploadSharedThumbnail(admin: ApiClient): Promise<string> {
  const file = await admin.upload<FileResponse>('/api/files', {
    name: 'thumbnail.png',
    mimeType: 'image/png',
    buffer: await readFile(THUMBNAIL),
  })
  await admin.post('/api/resources', resourceRequest(file.id, {}))
  return file.id
}

/**
 * Creates content through the API as an administrator, the way the admin pages would, so a test
 * can start from the state it needs. Every name is unique, everything uses `thumbnailId` and
 * events get `defaultCategory` unless told otherwise.
 */
export class ContentSeeder {
  constructor(
    private readonly admin: ApiClient,
    private readonly defaultCategory: EventCategoryTypeResponse,
    private readonly thumbnailId: string,
  ) {}

  /** Creates an event category. */
  category(name?: string, color?: string): Promise<EventCategoryTypeResponse> {
    return createCategory(this.admin, name, color)
  }

  /** Creates a terms document, conditions without signature unless told otherwise. */
  termsDocument(options: TermsDocumentOptions = {}): Promise<TermsDocumentResponse> {
    return this.admin.post('/api/events/termsDocument', {
      name: options.name ?? uniqueTitle('Condiciones'),
      description: richText(options.text ?? 'Normas de participación de la suite e2e.'),
      kind: options.kind ?? 'Conditions',
      requiresSignature: options.requiresSignature ?? false,
    })
  }

  /** Creates an event, by default in two weeks, for one day, with signups open now. */
  async event(options: EventOptions = {}): Promise<EventResponse> {
    const startsIn = options.startsIn ?? 14
    const eventStartsAt = dayFromToday(startsIn)
    const eventEndsAt = dayFromToday(startsIn + (options.lastsDays ?? 1) - 1)
    const window = options.signup ?? (eventEndsAt < dayFromToday(0) ? 'closed' : 'open')
    const categories = options.categories ?? [this.defaultCategory]
    const request: CreateEventRequest = {
      title: options.title ?? uniqueTitle('Evento'),
      subtitle: options.subtitle ?? 'Jornada creada por la suite e2e',
      description: richText(options.description ?? 'Una jornada de talleres de programación.'),
      eventStartsAt,
      eventEndsAt,
      ...signupWindow(window, eventStartsAt, eventEndsAt),
      thumbnailId: this.thumbnailId,
      categoryTypeIds: categories.map((category) => category.id),
      termsDocuments: (options.terms ?? []).map((terms) => ({
        termsDocumentId: terms.document.id,
        required: terms.required,
      })),
    }
    return this.admin.post('/api/events', request)
  }

  /**
   * Creates an activity on the event's first day, by default 10:00–12:00 with every role, or from
   * `startsAt` to `endsAt` when given.
   */
  async activity(event: EventResponse, options: ActivityOptions = {}): Promise<ActivityResponse> {
    const day = dayFromToday(
      daysBetween(dayFromToday(0), event.eventStartsAt) + (options.dayOfEvent ?? 0),
    )
    const startHour = options.startHour ?? 10
    const roles = options.roles ?? { participant: 10, volunteer: 3, leader: 1 }
    const request: CreateActivityRequest = {
      title: options.title ?? uniqueTitle('Taller'),
      description: options.description ?? 'Taller práctico de la suite e2e.',
      location: options.location ?? 'León',
      activityModalityTypeId: options.online
        ? CATALOG.modalities.online
        : CATALOG.modalities.onSite,
      activityStartsAt: (options.startsAt ?? instantOn(day, startHour)).toISOString(),
      activityEndsAt: (
        options.endsAt ?? instantOn(day, startHour + (options.hours ?? 2))
      ).toISOString(),
      thumbnailId: this.thumbnailId,
      roleCapacities: Object.entries(roles).map(([role, count]) => ({
        activityRoleTypeId: CATALOG.roles[role as RoleName],
        desiredCount: count,
      })),
    }
    return this.admin.post(`/api/activities/${event.id}`, request)
  }

  /**
   * Signs `userId` (the signed-in user of `api` or one of their dependents) up for an activity that
   * still takes signups, accepting every terms document as the SPA does, and then decides the
   * signup as an administrator. An administrator never signs anyone else up: to reach an activity
   * that has started or an event that has ended, sign up first and then `reschedule` or
   * `moveToPast`.
   */
  async enrol(
    api: ApiClient,
    activity: ActivityResponse,
    userId: string,
    role: RoleName = 'participant',
    status: SignupStatusName = 'confirmed',
  ): Promise<void> {
    await signUp(api, activity, userId, role)
    if (status !== 'requested') await this.decide(activity, userId, status)
  }

  /** Moves an activity to `startsAt`–`endsAt` as an administrator, keeping its signups. */
  reschedule(activity: ActivityResponse, startsAt: Date, endsAt: Date): Promise<ActivityResponse> {
    const request: UpdateActivityRequest = {
      title: activity.title,
      description: activity.description,
      location: activity.location,
      activityModalityTypeId: activity.modalityId,
      activityStartsAt: startsAt.toISOString(),
      activityEndsAt: endsAt.toISOString(),
      thumbnailId: activity.thumbnailId,
      roleCapacities: activity.roleCapacities.map(({ activityRoleTypeId, desiredCount }) => ({
        activityRoleTypeId,
        desiredCount,
      })),
    }
    return this.admin.put(`/api/activities/${activity.id}`, request)
  }

  /**
   * Moves an event and its `activities` as an administrator so that it starts `daysAgo` days ago,
   * with its signup closed, keeping their signups and the time of each activity. The event first
   * spans both its old and its new days, so no activity falls outside it meanwhile.
   */
  async moveToPast(
    event: EventResponse,
    activities: readonly ActivityResponse[],
    daysAgo: number,
  ): Promise<EventResponse> {
    const eventStartsAt = dayFromToday(-daysAgo)
    const eventEndsAt = dayFromToday(daysBetween(event.eventStartsAt, event.eventEndsAt) - daysAgo)
    const shift = daysBetween(eventStartsAt, event.eventStartsAt) * DAY_MS
    const update = (endsAt: string): Promise<EventResponse> => {
      const request: UpdateEventRequest = {
        title: event.title,
        subtitle: event.subtitle,
        description: event.description,
        eventStartsAt,
        eventEndsAt: endsAt,
        ...signupWindow('closed', eventStartsAt, eventEndsAt),
        thumbnailId: event.thumbnailId,
        categoryTypeIds: event.categories.map((category) => category.categoryTypeId),
        termsDocuments: event.termsDocuments.map(({ termsDocumentId, required }) => ({
          termsDocumentId,
          required,
        })),
      }
      return this.admin.put(`/api/events/${event.id}`, request)
    }
    await update(event.eventEndsAt)
    for (const activity of activities) {
      await this.reschedule(
        activity,
        new Date(Date.parse(activity.activityStartsAt) - shift),
        new Date(Date.parse(activity.activityEndsAt) - shift),
      )
    }
    return update(eventEndsAt)
  }

  /** Confirms or denies the signup of `userId`. */
  async decide(
    activity: ActivityResponse,
    userId: string,
    status: SignupStatusName,
  ): Promise<void> {
    await this.admin.patch(`/api/activities/${activity.id}/${userId}/change-status`, {
      assignmentStatusId: CATALOG.signupStatuses[status],
    })
  }

  /** Features the event on the home page. */
  async featureEvent(event: EventResponse): Promise<void> {
    await this.admin.patch(`/api/events/${event.id}/feature`)
  }

  /** Creates a news item. */
  async newsItem(options: NewsItemOptions = {}): Promise<NewsItemResponse> {
    return this.admin.post('/api/news', {
      title: options.title ?? uniqueTitle('Novedad'),
      subtitle: options.subtitle ?? 'Noticia creada por la suite e2e',
      description: richText(options.text ?? 'Abrimos las inscripciones del próximo taller.'),
      thumbnailId: this.thumbnailId,
    })
  }

  /** Features the news item on the home page. */
  async featureNewsItem(item: NewsItemResponse): Promise<void> {
    await this.admin.patch(`/api/news/${item.id}/feature`)
  }

  /** Creates an internal resource, or an external one when `url` is given. */
  resource(options: ResourceOptions = {}): Promise<ResourceResponse> {
    return this.admin.post('/api/resources', resourceRequest(this.thumbnailId, options))
  }

  /** Creates a partner, a sponsor since a year ago. */
  async partner(options: PartnerOptions = {}): Promise<PartnerResponse> {
    return this.admin.post('/api/partners', {
      name: options.name ?? uniqueTitle('Patrocinador'),
      fromDate: dayFromToday(-365),
      tier: options.tier ?? 1,
      website: options.website ?? null,
      thumbnailId: this.thumbnailId,
    })
  }

  /** Publishes a new version of a legal policy with `text`. */
  async publishPolicy(kind: LegalPolicyKind, text: string): Promise<void> {
    await this.admin.put(`/api/legal-policies/${kind}`, { content: richText(text) })
  }
}
