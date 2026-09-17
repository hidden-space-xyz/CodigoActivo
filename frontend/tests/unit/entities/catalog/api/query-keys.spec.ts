import { describe, expect, it } from 'vitest'

import { catalogQueryKeys } from '@/entities/catalog'

describe('catalogQueryKeys', () => {
  it('nests every catalog key under the shared root', () => {
    expect(catalogQueryKeys.all).toEqual(['catalogs'])
    expect(catalogQueryKeys.userTypes()).toEqual(['catalogs', 'user-types'])
    expect(catalogQueryKeys.userStatusTypes()).toEqual(['catalogs', 'user-status-types'])
    expect(catalogQueryKeys.activityRoleTypes()).toEqual(['catalogs', 'activity-role-types'])
    expect(catalogQueryKeys.assignmentStatusTypes()).toEqual([
      'catalogs',
      'assignment-status-types',
    ])
    expect(catalogQueryKeys.activityModalityTypes()).toEqual([
      'catalogs',
      'activity-modality-types',
    ])
    expect(catalogQueryKeys.resourceTypes()).toEqual(['catalogs', 'resource-types'])
  })

  it('nests the admin table keys under their list keys so one invalidation refreshes both', () => {
    expect(catalogQueryKeys.eventCategoryTypesTable()).toEqual([
      ...catalogQueryKeys.eventCategoryTypes(),
      'table',
    ])
    expect(catalogQueryKeys.termsDocumentsTable()).toEqual([
      ...catalogQueryKeys.termsDocuments(),
      'table',
    ])
  })
})
