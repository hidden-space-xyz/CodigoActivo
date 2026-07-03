import {
  deleteApiActivitiesRoleTypeActivityRoleTypeId,
  getApiActivitiesRoleType,
  postApiActivitiesRoleType,
  putApiActivitiesRoleTypeActivityRoleTypeId,
} from '@/shared/api/generated/endpoints/activities/activities'
import { catalogQueryKeys } from '@/entities/catalog'

import { useCatalog } from './useCatalog'

export function useActivityRoleTypes() {
  return useCatalog({
    queryKey: catalogQueryKeys.activityRoleTypes,
    fetchAll: () => getApiActivitiesRoleType().then((r) => r.data ?? []),
    create: (body) => postApiActivitiesRoleType(body),
    update: (id, body) => putApiActivitiesRoleTypeActivityRoleTypeId(id, body),
    remove: (id) => deleteApiActivitiesRoleTypeActivityRoleTypeId(id),
  })
}
