import {
  deleteApiActivitiesRoleTypeActivityRoleTypeId,
  postApiActivitiesRoleType,
  putApiActivitiesRoleTypeActivityRoleTypeId,
} from '@/shared/api/generated/endpoints/activities/activities'
import type { ActivityRoleTypeResponse } from '@/shared/api/generated/models'
import { fetchODataList } from '@/shared/api'
import { catalogQueryKeys } from '@/entities/catalog'

import { useCatalog } from './useCatalog'

export function useActivityRoleTypes() {
  return useCatalog({
    queryKey: catalogQueryKeys.activityRoleTypes,
    fetchAll: () =>
      fetchODataList<ActivityRoleTypeResponse>('ActivityRoleTypes', {
        orderBy: 'name asc',
        top: 100,
      }).then((r) => r.items),
    create: (body) => postApiActivitiesRoleType(body),
    update: (id, body) => putApiActivitiesRoleTypeActivityRoleTypeId(id, body),
    remove: (id) => deleteApiActivitiesRoleTypeActivityRoleTypeId(id),
  })
}
