<script setup lang="ts">
import { computed, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { AdminPageHeader, AppButton as Button, DataState, ListThumbnail } from '@/shared/ui'
import Column from 'primevue/column'
import DataTable from 'primevue/datatable'

import {
  ActivityFormDialog,
  AssignVolunteerDialog,
  useActivities,
  useAssignments,
} from '@/features/manage-activities'
import { useActivityModalityTypesList, useActivityRoleTypesList } from '@/entities/catalog'
import { useEvent, useEventSummary } from '@/features/manage-events'
import { useUsers } from '@/features/manage-users'
import type {
  ActivityResponse,
  CreateActivityRequest,
  UpdateActivityRequest,
} from '@/shared/api/generated/models'
import { formatDateTime, useCrudFeedback, useDeleteConfirm } from '@/shared/lib'

const route = useRoute()
const router = useRouter()
const eventId = computed(() => String(route.params.eventId))

const feedback = useCrudFeedback()
const { confirmDelete: requireDelete } = useDeleteConfirm()

const assignDialogVisible = ref(false)

const event = useEvent(eventId)
const summary = useEventSummary(eventId)
const activities = useActivities(eventId)
const assignments = useAssignments(eventId.value)
const roleTypes = useActivityRoleTypesList()
const modalityTypes = useActivityModalityTypesList()
// The users list only feeds the assign dialog — don't fetch it until the dialog opens.
const users = useUsers({ enabled: assignDialogVisible })

const summaryCards = computed(() => {
  const data = summary.data.value
  const cards = [{ label: 'Actividades', value: data?.activitiesCount ?? 0 }]
  for (const role of data?.roleTypeBreakdown ?? []) {
    cards.push({ label: role.roleTypeName ?? '—', value: role.approvedAssignments ?? 0 })
  }
  return cards
})

function roleNames(activity: ActivityResponse): string {
  return (activity.allowedRoleTypes ?? []).map((role) => role.roleTypeName).join(', ') || '—'
}

const activityDialogVisible = ref(false)
const selectedActivity = ref<ActivityResponse | null>(null)
const activitySaving = computed(
  () => activities.create.isPending.value || activities.update.isPending.value,
)

function openCreateActivity(): void {
  selectedActivity.value = null
  activityDialogVisible.value = true
}

function openActivityAssignments(activity: ActivityResponse): void {
  if (!activity.id) return
  void router.push({
    name: 'admin-activity-detail',
    params: { eventId: eventId.value, activityId: activity.id },
  })
}

async function openEditActivity(activity: ActivityResponse): Promise<void> {
  selectedActivity.value = activity
  activityDialogVisible.value = true
  if (!activity.id) return
  try {
    const fresh = await activities.fetchOne(activity.id)
    if (fresh) selectedActivity.value = fresh
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
        onError: (error) => feedback.error(error),
      },
    )
    return
  }
  activities.create.mutate(body as CreateActivityRequest, {
    onSuccess: () => {
      feedback.success('Actividad creada.')
      activityDialogVisible.value = false
    },
    onError: (error) => feedback.error(error),
  })
}

function confirmDeleteActivity(activity: ActivityResponse): void {
  requireDelete({
    header: 'Eliminar actividad',
    message: `¿Seguro que quieres eliminar "${activity.title}"?`,
    accept: () => {
      if (!activity.id) return
      activities.remove.mutate(
        { id: activity.id, thumbnailId: activity.thumbnailId },
        {
          onSuccess: () => feedback.success('Actividad eliminada.'),
          onError: (error) => feedback.error(error),
        },
      )
    },
  })
}

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
      },
      onError: (error) => feedback.error(error),
    },
  )
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
          <Column header="Modalidad">
            <template #body="{ data }">
              <div class="modality-cell">
                <span class="modality-cell__type">{{ data.modalityName || '—' }}</span>
                <span v-if="data.location" class="modality-cell__loc">{{ data.location }}</span>
              </div>
            </template>
          </Column>
          <Column header="Roles">
            <template #body="{ data }">
              {{ roleNames(data) }}
            </template>
          </Column>
          <Column header="Acciones" style="width: 160px">
            <template #body="{ data }">
              <div class="row-actions">
                <Button
                  icon="pi pi-users"
                  text
                  rounded
                  aria-label="Ver asignaciones"
                  @click="openActivityAssignments(data)"
                />
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

    <ActivityFormDialog
      v-model:visible="activityDialogVisible"
      :activity="selectedActivity"
      :role-types="roleTypes.data.value ?? []"
      :modality-types="modalityTypes.data.value ?? []"
      :saving="activitySaving"
      :event-start="event.data.value?.eventStartsAt ?? null"
      :event-end="event.data.value?.eventEndsAt ?? null"
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

.row-actions {
  display: flex;
  gap: 2px;
}

.modality-cell {
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.modality-cell__type {
  font-weight: 600;
}

.modality-cell__loc {
  font-size: 12.5px;
  color: var(--ca-text-muted);
}
</style>
