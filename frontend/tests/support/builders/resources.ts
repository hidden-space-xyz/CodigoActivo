import type {
  ResourceListItemResponse,
  ResourceResponse,
  ResourceTypeResponse,
} from '@/shared/api/generated/models'

import { AUDIT, richText } from './common'

/** Internal resource type: the resource is read on the site. */
export const internalType: ResourceTypeResponse = {
  id: 'type-article',
  name: 'Artículo',
  description: '',
  color: '#123456',
  isExternal: false,
}

/** External resource type: the resource links to another site. */
export const externalType: ResourceTypeResponse = {
  id: 'type-link',
  name: 'Enlace',
  description: '',
  color: '',
  isExternal: true,
}

/** Internal resource list item as returned by `GET /api/resources`. */
export function buildResourceListItem(
  overrides: Partial<ResourceListItemResponse> = {},
): ResourceListItemResponse {
  return {
    id: 'resource-1',
    title: 'Guía de Python',
    subtitle: 'Primeros pasos',
    url: null,
    type: internalType,
    ...AUDIT,
    createdAt: '2026-03-02T10:00:00Z',
    updatedAt: null,
    updatedBy: null,
    thumbnailId: 'thumb-resource',
    ...overrides,
  }
}

/** Full internal resource as returned by `GET /api/resources/:id`. */
export function buildResourceResponse(overrides: Partial<ResourceResponse> = {}): ResourceResponse {
  return {
    ...buildResourceListItem(),
    description: richText('Todo sobre Python.'),
    ...overrides,
  }
}
