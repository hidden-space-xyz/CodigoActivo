import { computed, onBeforeUnmount, ref, watch, type Ref } from 'vue'
import { useQuery } from '@tanstack/vue-query'

import { activityQueries } from '@/entities/activity'
import { userQueries, type Gender } from '@/entities/user'
import { useServerTable } from '@/shared/lib/paging'

import { attendeeList } from '../api/queries'
import type { AttendeeAssignment, AttendeeFilters } from './types'

const SEARCH_DELAY_MS = 300

function filterModel<T extends string>(source: Ref<T | null>) {
  return computed<T | null>({
    get: () => source.value,
    set: (value) => {
      source.value = value === null || value === '' ? null : value
    },
  })
}

/**
 * Attendees tab of an event's admin page: the attendees list with a debounced search, user type,
 * gender, activity, role and status filters and a sort picker, plus the catalogs its filters and
 * tags read. The list only loads while the tab is `active`. Call it in `setup`.
 *
 * @param eventId - Getter so the list follows route changes.
 * @param active - Getter telling whether the tab is selected.
 */
export function useEventAttendees(eventId: () => string, active: () => boolean) {
  const searchText = ref('')
  const search = ref('')
  const userTypeId = ref<string | null>(null)
  const gender = ref<Gender | null>(null)
  const activityId = ref<string | null>(null)
  const roleTypeId = ref<string | null>(null)
  const statusId = ref<string | null>(null)

  let searchTimer: ReturnType<typeof setTimeout> | undefined
  watch(searchText, (value) => {
    if (searchTimer) clearTimeout(searchTimer)
    searchTimer = setTimeout(() => {
      search.value = value
    }, SEARCH_DELAY_MS)
  })
  onBeforeUnmount(() => {
    if (searchTimer) clearTimeout(searchTimer)
  })

  function filters(): AttendeeFilters {
    return {
      ...(search.value.trim() ? { search: search.value.trim() } : {}),
      ...(userTypeId.value ? { userTypeId: userTypeId.value } : {}),
      ...(gender.value ? { gender: gender.value } : {}),
      ...(activityId.value ? { activityId: activityId.value } : {}),
      ...(roleTypeId.value ? { roleTypeId: roleTypeId.value } : {}),
      ...(statusId.value ? { statusId: statusId.value } : {}),
    }
  }

  const table = useServerTable({
    ...attendeeList,
    defaultSort: { field: 'firstName', order: 1 },
    extraParams: () => ({ eventId: eventId(), ...filters() }),
    enabled: active,
  })

  const sort = computed<string>({
    get: () => table.sortField.value ?? 'firstName',
    set: (value) => {
      table.sortField.value = value
      table.first.value = 0
    },
  })
  const ascending = computed(() => table.sortOrder.value !== -1)

  function toggleSortDirection(): void {
    table.sortOrder.value = ascending.value ? -1 : 1
    table.first.value = 0
  }

  const hasActiveFilters = computed(
    () =>
      searchText.value.trim() !== '' ||
      [userTypeId, gender, activityId, roleTypeId, statusId].some(
        (filter) => filter.value !== null,
      ),
  )

  const activities = useQuery(() => activityQueries.ofEvent(eventId()))
  const roles = useQuery(activityQueries.roles())
  const statuses = useQuery(activityQueries.statuses())
  const userTypes = useQuery(userQueries.types())

  const statusColors = computed(
    () => new Map((statuses.data.value ?? []).map((status) => [status.id, status.color])),
  )

  function statusColor(assignment: AttendeeAssignment): string | null {
    return statusColors.value.get(assignment.statusId) ?? null
  }

  return {
    table,
    searchText,
    userTypeFilter: filterModel(userTypeId),
    genderFilter: filterModel(gender),
    activityFilter: filterModel(activityId),
    roleFilter: filterModel(roleTypeId),
    statusFilter: filterModel(statusId),
    filters,
    hasActiveFilters,
    sort,
    ascending,
    toggleSortDirection,
    activities,
    roles,
    statuses,
    userTypes,
    statusColor,
  }
}
