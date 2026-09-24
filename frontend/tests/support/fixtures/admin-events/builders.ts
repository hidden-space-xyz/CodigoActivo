import { defineComponent, h } from 'vue'
import { DOMWrapper } from '@vue/test-utils'

import type {
  ActivityModalityTypeResponse,
  ActivityResponse,
  ActivityRoleTypeResponse,
  AssignmentStatusTypeResponse,
  EventAttendeeAssignmentResponse,
  EventAttendeeResponse,
  EventBadgeResponse,
  EventCategoryTypeResponse,
  EventListItemResponse,
  EventRatingListItemResponse,
  EventResponse,
  EventRosterActivityResponse,
  EventRosterParticipantResponse,
  EventSummaryResponse,
  TermsDocumentResponse,
  UserTypeResponse,
} from '@/shared/api/generated/models'

import { renderWithProviders, type RenderOptions } from '../../render'
import { http, HttpResponse, paged, server } from '../../server'

/** Fixture overrides; undefined values remove a default field. */
type Overrides<T> = { [K in keyof T]?: T[K] | undefined }

function withOverrides<T extends object>(defaults: T, overrides: Overrides<T>): T {
  return { ...defaults, ...overrides }
}

export const EVENT_ID = '11111111-1111-4111-8111-111111111111'
export const THUMBNAIL_ID = '22222222-2222-4222-8222-222222222222'

export function buildEventListItem(
  overrides: Overrides<EventListItemResponse> = {},
): EventListItemResponse {
  return withOverrides<EventListItemResponse>(
    {
      id: EVENT_ID,
      title: 'Hackathon',
      subtitle: 'Code all night',
      eventStartsAt: '2026-10-10',
      eventEndsAt: '2026-10-12',
      earlySignupStartsAt: null,
      signupStartsAt: '2026-09-01T08:00:00Z',
      signupEndsAt: '2026-10-01T20:00:00Z',
      thumbnailId: THUMBNAIL_ID,
      featured: false,
      categories: [{ categoryTypeId: 'cat-1', name: 'Tech', color: '#112233' }],
    },
    overrides,
  )
}

export function buildEvent(overrides: Overrides<EventResponse> = {}): EventResponse {
  return withOverrides<EventResponse>(
    {
      id: EVENT_ID,
      title: 'Hackathon',
      subtitle: 'Code all night',
      description: '{"type":"doc","content":[]}',
      eventStartsAt: '2026-10-10',
      eventEndsAt: '2026-10-12',
      earlySignupStartsAt: null,
      signupStartsAt: '2026-09-01T08:00:00.000Z',
      signupEndsAt: '2026-10-01T20:00:00.000Z',
      thumbnailId: THUMBNAIL_ID,
      featured: false,
      categories: [{ categoryTypeId: 'cat-1', name: 'Tech', color: '#112233' }],
      termsDocuments: [
        { termsDocumentId: 'terms-1', name: 'Terms', required: true, displayOrder: 0 },
      ],
    },
    overrides,
  )
}

export function buildActivity(overrides: Overrides<ActivityResponse> = {}): ActivityResponse {
  return withOverrides<ActivityResponse>(
    {
      id: 'act-1',
      title: 'Robotics',
      description: 'Build robots',
      location: 'Room 1',
      activityStartsAt: '2026-10-10T09:00:00.000Z',
      activityEndsAt: '2026-10-10T11:00:00.000Z',
      eventId: EVENT_ID,
      modalityId: 'mod-1',
      modalityName: 'On site',
      thumbnailId: THUMBNAIL_ID,
      roleCapacities: [{ activityRoleTypeId: 'role-1', desiredCount: 4 }],
    },
    overrides,
  )
}

export function buildAssignment(
  overrides: Overrides<EventAttendeeAssignmentResponse> = {},
): EventAttendeeAssignmentResponse {
  return withOverrides<EventAttendeeAssignmentResponse>(
    {
      activityId: 'act-1',
      activityTitle: 'Robotics',
      roleTypeId: 'role-1',
      roleTypeName: 'Volunteer',
      statusId: 'status-1',
      statusName: 'Requested',
      signedUpAt: '2026-09-02T10:00:00Z',
      hasTimeConflict: false,
    },
    overrides,
  )
}

export function buildAttendee(
  overrides: Overrides<EventAttendeeResponse> = {},
): EventAttendeeResponse {
  return withOverrides<EventAttendeeResponse>(
    {
      userId: 'user-1',
      firstName: 'Ada',
      lastName: 'Lovelace',
      email: 'ada@example.test',
      phone: '600000001',
      birthDate: '1990-01-15',
      gender: 'Female',
      userTypeName: 'Member',
      userTypeColor: '#ff0000',
      assignments: [buildAssignment()],
    },
    overrides,
  )
}

export function buildRating(
  overrides: Overrides<EventRatingListItemResponse> = {},
): EventRatingListItemResponse {
  return withOverrides<EventRatingListItemResponse>(
    {
      id: 'rating-1',
      score: 4,
      mostLiked: 'The people',
      leastLiked: '',
      suggestions: null,
    },
    overrides,
  )
}

export function buildSummary(
  overrides: Overrides<EventSummaryResponse> = {},
): EventSummaryResponse {
  return withOverrides<EventSummaryResponse>(
    {
      eventId: EVENT_ID,
      title: 'Hackathon',
      activitiesCount: 3,
      ratingsCount: 2,
      ratingsAverage: 4.25,
      roleTypeBreakdown: [
        { roleTypeId: 'role-1', roleTypeName: 'Volunteer', approvedAssignments: 7 },
      ],
    },
    overrides,
  )
}

export function buildBadge(overrides: Overrides<EventBadgeResponse> = {}): EventBadgeResponse {
  return withOverrides<EventBadgeResponse>(
    {
      userId: 'user-1',
      firstName: 'Ada',
      lastName: 'Lovelace',
      userTypeName: 'Member',
      userTypeColor: '#123456',
      activities: [{ title: 'Robotics', location: 'Lab 1' }],
    },
    overrides,
  )
}

export function buildRosterParticipant(
  overrides: Overrides<EventRosterParticipantResponse> = {},
): EventRosterParticipantResponse {
  return withOverrides<EventRosterParticipantResponse>(
    {
      userId: 'user-1',
      firstName: 'Ada',
      lastName: 'Lovelace',
      birthDate: '2000-01-01',
      email: 'ada@example.test',
      phone: '600000001',
      roleName: 'Monitora',
    },
    overrides,
  )
}

export function buildRosterActivity(
  overrides: Overrides<EventRosterActivityResponse> = {},
): EventRosterActivityResponse {
  return withOverrides<EventRosterActivityResponse>(
    {
      activityId: 'act-1',
      title: 'Robotics',
      location: 'Room 1',
      activityStartsAt: '2026-10-10T09:00:00Z',
      activityEndsAt: '2026-10-10T11:00:00Z',
      participants: [buildRosterParticipant()],
    },
    overrides,
  )
}

const categoryTypes: EventCategoryTypeResponse[] = [
  { id: 'cat-1', name: 'Tech', color: '#112233' },
  { id: 'cat-2', name: 'Art', color: '#445566' },
]
const termsDocuments: TermsDocumentResponse[] = [{ id: 'terms-1', name: 'Terms' }]
export const modalityTypes: ActivityModalityTypeResponse[] = [
  { id: 'mod-1', name: 'On site' },
  { id: 'mod-2', name: 'Online' },
]
export const roleTypes: ActivityRoleTypeResponse[] = [
  { id: 'role-1', name: 'Volunteer' },
  { id: 'role-2', name: 'Mentor' },
]
const statusTypes: AssignmentStatusTypeResponse[] = [
  { id: 'status-1', name: 'Requested', color: '#aabbcc' },
  { id: 'status-2', name: 'Confirmed', color: '#00ff00' },
]
const userTypes: UserTypeResponse[] = [
  { id: 'type-1', name: 'Member', color: '#ff0000' },
  { id: 'type-2', name: 'Guest', color: null },
]

/** Registers the catalog endpoints used across the admin event screens. */
export function useCatalogHandlers(): void {
  server.use(
    http.get('/api/events/categoryType', () => HttpResponse.json(paged(categoryTypes))),
    http.get('/api/events/termsDocument', () => HttpResponse.json(paged(termsDocuments))),
    http.get('/api/activities/modality-types', () => HttpResponse.json(modalityTypes)),
    http.get('/api/activities/roleType', () => HttpResponse.json(roleTypes)),
    http.get('/api/activities/assignment-status-types', () => HttpResponse.json(statusTypes)),
    http.get('/api/users/types', () => HttpResponse.json(userTypes)),
    http.get('/api/files/:fileId', () => HttpResponse.json({ name: 'cover', extension: '.png' })),
  )
}

/** Mounts a host component that runs `composable` in its setup with every app provider. */
export async function setupComposable<T>(composable: () => T, options: RenderOptions = {}) {
  const box: { value?: T } = {}
  const Host = defineComponent({
    name: 'AdminEventsComposableHost',
    setup() {
      box.value = composable()
      return () => h('div')
    },
  })
  const rendered = await renderWithProviders(Host, options)
  if (!('value' in box)) throw new Error('Composable did not run')
  return { result: box.value, ...rendered }
}

/** Finds an element anywhere in `document.body` (teleported dialogs included). */
export function bodyFind<E extends Element = HTMLElement>(selector: string): DOMWrapper<E> {
  const element = document.body.querySelector<E>(selector)
  if (!element) throw new Error(`Element not found: ${selector}`)
  return new DOMWrapper(element)
}

/** Buttons in `document.body` whose visible text or aria-label matches `label`. */
export function bodyButtons(label: string, root: ParentNode = document.body): HTMLButtonElement[] {
  return [...root.querySelectorAll<HTMLButtonElement>('button')].filter(
    (button) => button.textContent?.trim() === label || button.getAttribute('aria-label') === label,
  )
}

/** Clicks the first button in `document.body` labelled `label`. */
export function clickBodyButton(label: string, root: ParentNode = document.body): void {
  const button = bodyButtons(label, root)[0]
  if (!button) throw new Error(`Button not found: ${label}`)
  button.click()
}

/** Text of the notifications currently shown. */
export function notificationsText(): string {
  return [...document.body.querySelectorAll('.el-notification')]
    .map((node) => node.textContent ?? '')
    .join('\n')
}

/** Picks `file` in the first thumbnail file input inside `root`. */
export function pickFile(file: File, root: ParentNode = document.body): void {
  const input = root.querySelector<HTMLInputElement>('.thumb input[type="file"]')
  if (!input) throw new Error('File input not found')
  Object.defineProperty(input, 'files', { configurable: true, value: [file] })
  input.dispatchEvent(new Event('change'))
}

/** Reads a component prop without the narrow typing of `wrapper.props(name)`. */
export function propOf(wrapper: { props: () => unknown }, name: string): unknown {
  return (wrapper.props() as Record<string, unknown>)[name]
}

/** Stubs `document.fonts.ready`, which jsdom does not implement. Returns a cleanup function. */
export function stubDocumentFonts(): () => void {
  Object.defineProperty(document, 'fonts', {
    configurable: true,
    value: { ready: Promise.resolve() },
  })
  return () => {
    Reflect.deleteProperty(document, 'fonts')
  }
}
