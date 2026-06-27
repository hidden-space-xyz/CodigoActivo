<script setup lang="ts">
import { computed, ref } from 'vue'
import { useRoute } from 'vue-router'
import { useConfirm } from 'primevue/useconfirm'
import Button from 'primevue/button'
import Column from 'primevue/column'
import DataTable from 'primevue/datatable'
import Dialog from 'primevue/dialog'
import Select from 'primevue/select'
import Tag from 'primevue/tag'

import ActivityFormDialog from '@/features/activities/ActivityFormDialog.vue'
import AssignVolunteerDialog from '@/features/activities/AssignVolunteerDialog.vue'
import { useActivities } from '@/features/activities/useActivities'
import { useAssignments } from '@/features/activities/useAssignments'
import { deleteThumbnail } from '@/features/files/useThumbnail'
import { useActivityRoleTypes, useAssignmentStatusTypesList } from '@/features/catalogs/catalogs'
import { useEvent } from '@/features/events/useEventsAdmin'
import { useEventAssignments, useEventSummary } from '@/features/events/useEventReports'
import { useUsers } from '@/features/users/useUsers'
import type {
  ActivityResponse,
  AssignmentReportItemResponse,
  CreateActivityRequest,
  UpdateActivityRequest,
} from '@/shared/api/generated/models'
import { getErrorMessage } from '@/shared/utils/api-error'
import { formatDateTime } from '@/shared/utils/format'
import ListThumbnail from '@/shared/ui/components/ListThumbnail.vue'
import AdminPageHeader from '@/shared/ui/admin/AdminPageHeader.vue'
import DataState from '@/shared/ui/admin/DataState.vue'
import { useCrudFeedback } from '@/shared/ui/admin/use-crud-feedback'

const route = useRoute()
const eventId = computed(() => String(route.params.eventId))

const feedback = useCrudFeedback()
const confirm = useConfirm()

const event = useEvent(eventId)
const summary = useEventSummary(eventId)
const assignmentsReport = useEventAssignments(eventId)
const activities = useActivities(eventId)
const assignments = useAssignments(eventId.value)
const roleTypes = useActivityRoleTypes()
const statusTypes = useAssignmentStatusTypesList()
const users = useUsers()

const userNameById = computed(() => {
  const map = new Map<string, string>()
  for (const user of users.list.data.value ?? []) {
    if (user.id) map.set(user.id, `${user.firstName ?? ''} ${user.lastName ?? ''}`.trim())
  }
  return map
})

const summaryCards = computed(() => {
  const data = summary.data.value
  return [
    { label: 'Actividades', value: data?.activitiesCount ?? 0 },
    { label: 'Asignaciones', value: data?.totalAssignments ?? 0 },
    { label: 'Solicitadas', value: data?.requestedAssignments ?? 0 },
    { label: 'Confirmadas', value: data?.confirmedAssignments ?? 0 },
    { label: 'Rechazadas', value: data?.deniedAssignments ?? 0 },
    { label: 'Voluntarios', value: data?.distinctVolunteers ?? 0 },
  ]
})

const activityDialogVisible = ref(false)
const selectedActivity = ref<ActivityResponse | null>(null)
const activitySaving = computed(
  () => activities.create.isPending.value || activities.update.isPending.value,
)

function openCreateActivity(): void {
  selectedActivity.value = null
  activityDialogVisible.value = true
}

async function openEditActivity(activity: ActivityResponse): Promise<void> {
  selectedActivity.value = activity
  activityDialogVisible.value = true
  if (!activity.id) return
  try {
    selectedActivity.value = await activities.fetchOne(eventId.value, activity.id)
  } catch {}
}

function onActivitySubmit(body: CreateActivityRequest | UpdateActivityRequest): void {
  if (selectedActivity.value?.id) {
    activities.update.mutate(
      { id: selectedActivity.value.id, body: body as UpdateActivityRequest },
      {
        onSuccess: () => {
          feedback.success('Actividad actualizada.')
          activityDialogVisible.value = false
        },
        onError: (error) => feedback.error(getErrorMessage(error)),
      },
    )
    return
  }
  activities.create.mutate(body as CreateActivityRequest, {
    onSuccess: () => {
      feedback.success('Actividad creada.')
      activityDialogVisible.value = false
      void summary.refetch()
    },
    onError: (error) => feedback.error(getErrorMessage(error)),
  })
}

function confirmDeleteActivity(activity: ActivityResponse): void {
  confirm.require({
    header: 'Eliminar actividad',
    message: `¿Seguro que quieres eliminar "${activity.title}"?`,
    icon: 'pi pi-exclamation-triangle',
    acceptLabel: 'Eliminar',
    rejectLabel: 'Cancelar',
    acceptClass: 'p-button-danger',
    accept: () => {
      if (!activity.id) return
      activities.remove.mutate(activity.id, {
        onSuccess: () => {
          feedback.success('Actividad eliminada.')
          void deleteThumbnail(activity.thumbnailId)
          void summary.refetch()
        },
        onError: (error) => feedback.error(getErrorMessage(error)),
      })
    },
  })
}

const assignDialogVisible = ref(false)

function onAssignSubmit(payload: {
  activityId: string
  userId: string
  activityRoleTypeId: string
}): void {
  assignments.assign.mutate(
    {
      activityId: payload.activityId,
      userId: payload.userId,
      body: { activityRoleTypeId: payload.activityRoleTypeId },
    },
    {
      onSuccess: () => {
        feedback.success('Voluntario asignado.')
        assignDialogVisible.value = false
        void summary.refetch()
      },
      onError: (error) => feedback.error(getErrorMessage(error)),
    },
  )
}

const statusDialogVisible = ref(false)
const statusTarget = ref<AssignmentReportItemResponse | null>(null)
const selectedStatusId = ref<string | null>(null)

function openChangeStatus(item: AssignmentReportItemResponse): void {
  statusTarget.value = item
  selectedStatusId.value = item.statusId ?? null
  statusDialogVisible.value = true
}

function submitChangeStatus(): void {
  if (!statusTarget.value?.activityId || !statusTarget.value.userId || !selectedStatusId.value)
    return
  assignments.changeStatus.mutate(
    {
      activityId: statusTarget.value.activityId,
      userId: statusTarget.value.userId,
      body: { assignmentStatusId: selectedStatusId.value },
    },
    {
      onSuccess: () => {
        feedback.success('Estado actualizado.')
        statusDialogVisible.value = false
        void summary.refetch()
      },
      onError: (error) => feedback.error(getErrorMessage(error)),
    },
  )
}

function confirmUnassign(item: AssignmentReportItemResponse): void {
  confirm.require({
    header: 'Quitar asignación',
    message: '¿Seguro que quieres quitar esta asignación?',
    icon: 'pi pi-exclamation-triangle',
    acceptLabel: 'Quitar',
    rejectLabel: 'Cancelar',
    acceptClass: 'p-button-danger',
    accept: () => {
      if (!item.activityId || !item.userId) return
      assignments.unassign.mutate(
        { activityId: item.activityId, userId: item.userId },
        {
          onSuccess: () => {
            feedback.success('Asignación eliminada.')
            void summary.refetch()
          },
          onError: (error) => feedback.error(getErrorMessage(error)),
        },
      )
    },
  })
}
</script>

<template>
  <div>
    <RouterLink :to="{ name: 'admin-events' }" class="back">← Volver a eventos</RouterLink>

    <AdminPageHeader
      :title="event.data.value?.title ?? 'Evento'"
      :subtitle="event.data.value?.subtitle ?? ''"
    >
      <template #actions>
        <Button
          label="Asignar voluntario"
          icon="pi pi-user-plus"
          @click="assignDialogVisible = true"
        />
        <Button
          label="Nueva actividad"
          icon="pi pi-plus"
          severity="secondary"
          @click="openCreateActivity"
        />
      </template>
    </AdminPageHeader>

    <div class="summary">
      <div v-for="card in summaryCards" :key="card.label" class="summary__card">
        <div class="summary__value">{{ card.value }}</div>
        <div class="summary__label">{{ card.label }}</div>
      </div>
    </div>

    <section class="block">
      <h2 class="block__title">Actividades</h2>
      <DataState
        :loading="activities.list.isLoading.value"
        :error="activities.list.isError.value"
        :empty="(activities.list.data.value?.length ?? 0) === 0"
        empty-text="Este evento aún no tiene actividades."
      >
        <DataTable :value="activities.list.data.value" data-key="id" striped-rows>
          <Column header="Imagen" style="width: 110px">
            <template #body="{ data }">
              <ListThumbnail
                :thumbnail-id="data.thumbnailId"
                :alt="data.title"
                style="width: 88px"
              />
            </template>
          </Column>
          <Column field="title" header="Título" />
          <Column header="Horario">
            <template #body="{ data }">
              {{ formatDateTime(data.activityStartsAt) }} –
              {{ formatDateTime(data.activityEndsAt) }}
            </template>
          </Column>
          <Column header="Roles">
            <template #body="{ data }">
              <Tag
                v-for="role in data.allowedRoleTypes ?? []"
                :key="role.roleTypeId"
                :value="role.roleTypeName"
                class="role-tag"
              />
              <span v-if="(data.allowedRoleTypes ?? []).length === 0">—</span>
            </template>
          </Column>
          <Column header="Acciones" style="width: 120px">
            <template #body="{ data }">
              <div class="row-actions">
                <Button
                  icon="pi pi-pencil"
                  text
                  rounded
                  aria-label="Editar"
                  @click="openEditActivity(data)"
                />
                <Button
                  icon="pi pi-trash"
                  text
                  rounded
                  severity="danger"
                  aria-label="Eliminar"
                  @click="confirmDeleteActivity(data)"
                />
              </div>
            </template>
          </Column>
        </DataTable>
      </DataState>
    </section>

    <section class="block">
      <h2 class="block__title">Asignaciones</h2>
      <DataState
        :loading="assignmentsReport.isLoading.value"
        :error="assignmentsReport.isError.value"
        :empty="(assignmentsReport.data.value?.items?.length ?? 0) === 0"
        empty-text="Todavía no hay voluntarios asignados."
      >
        <DataTable :value="assignmentsReport.data.value?.items ?? []" striped-rows>
          <Column field="activityTitle" header="Actividad" />
          <Column header="Voluntario">
            <template #body="{ data }">{{ userNameById.get(data.userId) || data.userId }}</template>
          </Column>
          <Column field="roleTypeName" header="Rol" />
          <Column header="Estado">
            <template #body="{ data }">
              <Tag :value="data.statusName || '—'" severity="info" />
            </template>
          </Column>
          <Column header="Acciones" style="width: 130px">
            <template #body="{ data }">
              <div class="row-actions">
                <Button
                  icon="pi pi-sync"
                  text
                  rounded
                  aria-label="Cambiar estado"
                  @click="openChangeStatus(data)"
                />
                <Button
                  icon="pi pi-user-minus"
                  text
                  rounded
                  severity="danger"
                  aria-label="Quitar"
                  @click="confirmUnassign(data)"
                />
              </div>
            </template>
          </Column>
        </DataTable>
      </DataState>
    </section>

    <ActivityFormDialog
      v-model:visible="activityDialogVisible"
      :activity="selectedActivity"
      :role-types="roleTypes.list.data.value ?? []"
      :saving="activitySaving"
      @submit="onActivitySubmit"
    />

    <AssignVolunteerDialog
      v-model:visible="assignDialogVisible"
      :activities="activities.list.data.value ?? []"
      :users="users.list.data.value ?? []"
      :saving="assignments.assign.isPending.value"
      :verify-overlaps="assignments.verifyOverlaps"
      @submit="onAssignSubmit"
    />

    <Dialog
      v-model:visible="statusDialogVisible"
      modal
      header="Cambiar estado"
      :style="{ width: '400px' }"
    >
      <div class="form__field">
        <label>Estado</label>
        <Select
          v-model="selectedStatusId"
          :options="statusTypes.data.value ?? []"
          option-label="name"
          option-value="id"
          placeholder="Selecciona un estado"
          fluid
        />
      </div>
      <template #footer>
        <Button
          label="Cancelar"
          text
          severity="secondary"
          :disabled="assignments.changeStatus.isPending.value"
          @click="statusDialogVisible = false"
        />
        <Button
          label="Aplicar"
          :loading="assignments.changeStatus.isPending.value"
          :disabled="!selectedStatusId"
          @click="submitChangeStatus"
        />
      </template>
    </Dialog>
  </div>
</template>

<style scoped>
.back {
  display: inline-block;
  margin-bottom: 14px;
  color: var(--ca-text-muted);
  text-decoration: none;
  font-size: 14px;
}

.back:hover {
  color: var(--ca-text);
}

.summary {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(150px, 1fr));
  gap: 12px;
  margin-bottom: 26px;
}

.summary__card {
  background: var(--ca-surface);
  border: 1px solid var(--ca-border-soft);
  border-radius: 12px;
  padding: 16px;
}

.summary__value {
  font-family: var(--ca-font-display);
  font-weight: 700;
  font-size: 26px;
  color: var(--ca-text-bright);
}

.summary__label {
  font-size: 13px;
  color: var(--ca-text-muted);
}

.block {
  margin-bottom: 30px;
}

.block__title {
  font-family: var(--ca-font-display);
  font-size: 20px;
  font-weight: 600;
  color: var(--ca-text-bright);
  margin-bottom: 14px;
}

.role-tag {
  margin-right: 4px;
}

.row-actions {
  display: flex;
  gap: 2px;
}

.form__field {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.form__field label {
  font-size: 13px;
  font-weight: 600;
  color: var(--ca-text-muted);
}
</style>
