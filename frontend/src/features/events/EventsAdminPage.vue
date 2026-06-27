<script setup lang="ts">
import { computed, ref } from 'vue'
import { useConfirm } from 'primevue/useconfirm'
import Button from 'primevue/button'
import Column from 'primevue/column'
import DataTable from 'primevue/datatable'

import { deleteThumbnail } from '@/features/files/useThumbnail'
import EventFormDialog from '@/features/events/EventFormDialog.vue'
import { useEventsAdmin } from '@/features/events/useEventsAdmin'
import type {
  CreateEventRequest,
  EventResponse,
  UpdateEventRequest,
} from '@/shared/api/generated/models'
import { getErrorMessage } from '@/shared/utils/api-error'
import { formatDateTime } from '@/shared/utils/format'
import AdminPageHeader from '@/shared/ui/admin/AdminPageHeader.vue'
import DataState from '@/shared/ui/admin/DataState.vue'
import { useCrudFeedback } from '@/shared/ui/admin/use-crud-feedback'

const { list, create, update, remove } = useEventsAdmin()
const feedback = useCrudFeedback()
const confirm = useConfirm()

const dialogVisible = ref(false)
const selected = ref<EventResponse | null>(null)
const saving = computed(() => create.isPending.value || update.isPending.value)

function openCreate(): void {
  selected.value = null
  dialogVisible.value = true
}

function openEdit(event: EventResponse): void {
  selected.value = event
  dialogVisible.value = true
}

function onSubmit(body: CreateEventRequest | UpdateEventRequest): void {
  if (selected.value?.id) {
    update.mutate(
      { id: selected.value.id, body: body as UpdateEventRequest },
      {
        onSuccess: () => {
          feedback.success('Evento actualizado.')
          dialogVisible.value = false
        },
        onError: (error) => feedback.error(getErrorMessage(error)),
      },
    )
    return
  }
  create.mutate(body as CreateEventRequest, {
    onSuccess: () => {
      feedback.success('Evento creado.')
      dialogVisible.value = false
    },
    onError: (error) => feedback.error(getErrorMessage(error)),
  })
}

function confirmDelete(event: EventResponse): void {
  confirm.require({
    header: 'Eliminar evento',
    message: `¿Seguro que quieres eliminar "${event.title}"? Se perderán también sus actividades.`,
    icon: 'pi pi-exclamation-triangle',
    acceptLabel: 'Eliminar',
    rejectLabel: 'Cancelar',
    acceptClass: 'p-button-danger',
    accept: () => {
      if (!event.id) return
      remove.mutate(event.id, {
        onSuccess: () => {
          feedback.success('Evento eliminado.')
          void deleteThumbnail(event.thumbnailId)
        },
        onError: (error) => feedback.error(getErrorMessage(error)),
      })
    },
  })
}
</script>

<template>
  <div>
    <AdminPageHeader title="Eventos" subtitle="Gestión de eventos y sus actividades">
      <template #actions>
        <Button label="Nuevo evento" icon="pi pi-plus" @click="openCreate" />
      </template>
    </AdminPageHeader>

    <DataState
      :loading="list.isLoading.value"
      :error="list.isError.value"
      :empty="(list.data.value?.length ?? 0) === 0"
      empty-text="Aún no hay eventos."
    >
      <DataTable :value="list.data.value" data-key="id" striped-rows paginator :rows="10">
        <Column field="title" header="Título" />
        <Column header="Inicio">
          <template #body="{ data }">{{ formatDateTime(data.eventStartsAt) }}</template>
        </Column>
        <Column header="Inscripción">
          <template #body="{ data }">
            {{ formatDateTime(data.signupStartsAt) }} – {{ formatDateTime(data.signupEndsAt) }}
          </template>
        </Column>
        <Column header="Acciones" style="width: 200px">
          <template #body="{ data }">
            <div class="row-actions">
              <RouterLink :to="{ name: 'admin-event-detail', params: { eventId: data.id } }">
                <Button icon="pi pi-cog" text rounded aria-label="Gestionar" />
              </RouterLink>
              <Button
                icon="pi pi-pencil"
                text
                rounded
                aria-label="Editar"
                @click="openEdit(data)"
              />
              <Button
                icon="pi pi-trash"
                text
                rounded
                severity="danger"
                aria-label="Eliminar"
                @click="confirmDelete(data)"
              />
            </div>
          </template>
        </Column>
      </DataTable>
    </DataState>

    <EventFormDialog
      v-model:visible="dialogVisible"
      :event="selected"
      :saving="saving"
      @submit="onSubmit"
    />
  </div>
</template>

<style scoped>
.row-actions {
  display: flex;
  align-items: center;
  gap: 2px;
}
</style>
