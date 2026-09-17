import { describe, expect, it, vi } from 'vitest'

import {
  catalogQueryKeys,
  useActivityModalityTypesList,
  useActivityRoleTypesList,
  useAssignmentStatusTypesList,
  useEventCategoryTypesList,
  useResourceTypesList,
  useTermsDocumentsList,
  useUserStatusTypesList,
  useUserTypesList,
} from '@/entities/catalog'

import { renderComposable } from '../../../../support/fixtures/entities/composable'
import { http, HttpResponse, paged, server } from '../../../../support/server'

const catalogs = [
  ['user types', useUserTypesList, catalogQueryKeys.userTypes(), '/api/users/types', false],
  [
    'user statuses',
    useUserStatusTypesList,
    catalogQueryKeys.userStatusTypes(),
    '/api/users/status-types',
    false,
  ],
  [
    'activity roles',
    useActivityRoleTypesList,
    catalogQueryKeys.activityRoleTypes(),
    '/api/activities/roleType',
    false,
  ],
  [
    'assignment statuses',
    useAssignmentStatusTypesList,
    catalogQueryKeys.assignmentStatusTypes(),
    '/api/activities/assignment-status-types',
    false,
  ],
  [
    'activity modalities',
    useActivityModalityTypesList,
    catalogQueryKeys.activityModalityTypes(),
    '/api/activities/modality-types',
    false,
  ],
  [
    'resource types',
    useResourceTypesList,
    catalogQueryKeys.resourceTypes(),
    '/api/resources/types',
    false,
  ],
  [
    'event categories',
    useEventCategoryTypesList,
    catalogQueryKeys.eventCategoryTypes(),
    '/api/events/categoryType',
    true,
  ],
  [
    'terms documents',
    useTermsDocumentsList,
    catalogQueryKeys.termsDocuments(),
    '/api/events/termsDocument',
    true,
  ],
] as const

describe('catalog queries', () => {
  it.each(catalogs)(
    'loads and caches the %s list',
    async (_name, useList, queryKey, path, isPaged) => {
      const items = [{ id: 'c1', name: 'Uno' }]
      server.use(http.get(path, () => HttpResponse.json(isPaged ? paged(items) : items)))

      const { result, queryClient } = await renderComposable(() => useList())

      await vi.waitFor(() => expect(result.data.value).toEqual(items))
      expect(queryClient.getQueryData(queryKey)).toEqual(items)
    },
  )
})
