export const catalogQueryKeys = {
  all: ['catalogs'] as const,
  userTypes: () => [...catalogQueryKeys.all, 'user-types'] as const,
  userStatusTypes: () => [...catalogQueryKeys.all, 'user-status-types'] as const,
  activityRoleTypes: () => [...catalogQueryKeys.all, 'activity-role-types'] as const,
  assignmentStatusTypes: () => [...catalogQueryKeys.all, 'assignment-status-types'] as const,
  eventCategoryTypes: () => [...catalogQueryKeys.all, 'event-category-types'] as const,
  eventCategoryTypesTable: () => [...catalogQueryKeys.eventCategoryTypes(), 'table'] as const,
  termsDocuments: () => [...catalogQueryKeys.all, 'terms-documents'] as const,
  termsDocumentsTable: () => [...catalogQueryKeys.termsDocuments(), 'table'] as const,
  activityModalityTypes: () => [...catalogQueryKeys.all, 'activity-modality-types'] as const,
  resourceTypes: () => [...catalogQueryKeys.all, 'resource-types'] as const,
}
