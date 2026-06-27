<script setup lang="ts">
import { computed, ref } from 'vue'
import { useQuery } from '@tanstack/vue-query'
import Button from 'primevue/button'
import Column from 'primevue/column'
import DataTable from 'primevue/datatable'
import DatePicker from 'primevue/datepicker'
import Tag from 'primevue/tag'

import { getApiActivitiesAssigned } from '@/shared/api/generated/endpoints/activities/activities'
import type { GetApiActivitiesAssignedParams } from '@/shared/api/generated/models'
import { queryKeys } from '@/shared/api/query-keys'
import { formatDateTime } from '@/shared/utils/format'
import AdminPageHeader from '@/shared/ui/admin/AdminPageHeader.vue'
import DataState from '@/shared/ui/admin/DataState.vue'

const startDate = ref<Date | null>(null)
const endDate = ref<Date | null>(null)
const appliedParams = ref<GetApiActivitiesAssignedParams>({})

const query = useQuery({
  queryKey: computed(() => [...queryKeys.assignedActivities, appliedParams.value]),
  queryFn: ({ signal }) =>
    getApiActivitiesAssigned(appliedParams.value, { signal }).then((r) => r.data ?? []),
})

function applyFilter(): void {
  const params: GetApiActivitiesAssignedParams = {}
  if (startDate.value) params.startDate = startDate.value.toISOString()
  if (endDate.value) params.endDate = endDate.value.toISOString()
  appliedParams.value = params
}

function clearFilter(): void {
  startDate.value = null
  endDate.value = null
  appliedParams.value = {}
}
</script>

<template>
  <div>
    <AdminPageHeader title="Actividades asignadas" subtitle="Tus actividades por rango de fechas" />

    <div class="filters">
      <DatePicker v-model="startDate" show-time hour-format="24" placeholder="Desde" />
      <DatePicker v-model="endDate" show-time hour-format="24" placeholder="Hasta" />
      <Button label="Filtrar" icon="pi pi-filter" @click="applyFilter" />
      <Button label="Limpiar" text severity="secondary" @click="clearFilter" />
    </div>

    <DataState
      :loading="query.isLoading.value"
      :error="query.isError.value"
      :empty="(query.data.value?.length ?? 0) === 0"
      empty-text="No tienes actividades asignadas en este periodo."
    >
      <DataTable :value="query.data.value" data-key="activityId" striped-rows paginator :rows="10">
        <Column field="title" header="Actividad" />
        <Column header="Horario">
          <template #body="{ data }">
            {{ formatDateTime(data.activityStartsAt) }} – {{ formatDateTime(data.activityEndsAt) }}
          </template>
        </Column>
        <Column header="Rol">
          <template #body="{ data }">{{ data.roleType?.name || '—' }}</template>
        </Column>
        <Column header="Estado">
          <template #body="{ data }">
            <Tag :value="data.status?.name || '—'" severity="info" />
          </template>
        </Column>
        <Column header="Evento">
          <template #body="{ data }">
            <RouterLink
              :to="{ name: 'admin-event-detail', params: { eventId: data.eventId } }"
              class="link"
            >
              Ver evento
            </RouterLink>
          </template>
        </Column>
      </DataTable>
    </DataState>
  </div>
</template>

<style scoped>
.filters {
  display: flex;
  align-items: center;
  gap: 10px;
  margin-bottom: 20px;
  flex-wrap: wrap;
}

.link {
  color: var(--ca-cyan);
  text-decoration: none;
}

.link:hover {
  text-decoration: underline;
}
</style>
