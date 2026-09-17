import type {
  AnnouncementResponse,
  EventCategoryTypeResponse,
  PartnerResponse,
  ResourceResponse,
  ResourceTypeResponse,
  TermsDocumentResponse,
  UserStatusTypeResponse,
  UserTypeResponse,
} from '@/shared/api/generated/models'

/** Shallow copy of `value` without `key`, e.g. a row the API returned without an id. */
export function without<T extends object, K extends keyof T>(value: T, key: K): Omit<T, K> {
  return Object.fromEntries(Object.entries(value).filter(([name]) => name !== key)) as Omit<T, K>
}

/** Rich-text document with a single paragraph of `text`, as stored by the editor. */
export function richText(text: string): string {
  return JSON.stringify({
    type: 'doc',
    content: [{ type: 'paragraph', content: [{ type: 'text', text }] }],
  })
}

/** Admin partner row. */
export function buildPartner(overrides: PartnerResponse = {}): PartnerResponse {
  return {
    id: 'partner-1',
    name: 'Acme',
    tier: 1,
    website: 'https://acme.test',
    fromDate: '2024-03-15',
    thumbnailId: 'thumb-partner-1',
    createdAt: '2024-03-15T10:00:00Z',
    ...overrides,
  }
}

/** Internal (non-external) resource type. */
export const internalType: ResourceTypeResponse = {
  id: 'type-article',
  name: 'Artículo',
  color: '#123456',
  isExternal: false,
}

/** External (link) resource type. */
export const externalType: ResourceTypeResponse = {
  id: 'type-link',
  name: 'Enlace',
  color: null,
  isExternal: true,
}

/** Admin resource detail. */
export function buildResource(overrides: ResourceResponse = {}): ResourceResponse {
  return {
    id: 'resource-1',
    title: 'Vue guide',
    subtitle: 'Getting started',
    description: richText('Hello'),
    url: null,
    type: internalType,
    createdAt: '2025-02-01T09:30:00Z',
    thumbnailId: 'thumb-resource-1',
    ...overrides,
  }
}

/** Admin announcement detail. */
export function buildAnnouncement(overrides: AnnouncementResponse = {}): AnnouncementResponse {
  return {
    id: 'announcement-1',
    title: 'Hackathon',
    subtitle: 'Join us',
    description: richText('Details'),
    createdAt: '2025-04-01T08:00:00Z',
    thumbnailId: 'thumb-announcement-1',
    featured: false,
    ...overrides,
  }
}

/** Event category catalog entry. */
export function buildEventCategory(
  overrides: EventCategoryTypeResponse = {},
): EventCategoryTypeResponse {
  return { id: 'category-1', name: 'Workshop', color: '#FF0000', ...overrides }
}

/** Terms document catalog entry. */
export function buildTermsDocument(overrides: TermsDocumentResponse = {}): TermsDocumentResponse {
  return { id: 'terms-1', name: 'Privacy', description: richText('Terms body'), ...overrides }
}

/** User types catalog. */
export const userTypes: UserTypeResponse[] = [
  { id: 'type-participant', name: 'Participant', color: '#00AA00' },
  { id: 'type-member', name: 'Member', color: null },
]

/** User status catalog. */
export const userStatusTypes: UserStatusTypeResponse[] = [
  { id: 'status-active', name: 'Active', color: '#00FF00' },
  { id: 'status-blocked', name: 'Blocked', color: '#FF0000' },
]
