<script setup lang="ts">
import { computed, ref } from 'vue'
import { useConfirm } from 'primevue/useconfirm'
import { AdminPageHeader, AppButton as Button, DataState, ListThumbnail } from '@/shared/ui'
import Column from 'primevue/column'
import DataTable from 'primevue/datatable'
import Tag from 'primevue/tag'

import { deleteThumbnail } from '@/entities/file'
import { EventFormDialog, useEventsAdmin } from '@/features/manage-events'
import type {
  CreateEventRequest,
  EventResponse,
  UpdateEventRequest,
} from '@/shared/api/generated/models'
import { formatDate, formatDateTime, getErrorMessage, useCrudFeedback } from '@/shared/lib'

const { list, create, update, remove, feature } = useEventsAdmin()
const feedback = useCrudFeedback()
const confirm = useConfirm()

function onFeature(event: EventResponse): void {
  if (!event.id || event.featured) return
  feature.mutate(event.id, {
    onSuccess: () => feedback.success('Evento destacado.'),
    onError: (error) => feedback.error(getErrorMessage(error)),
  })
}

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
        <Column header="Imagen" style="width: 110px">
          <template #body="{ data }">
            <ListThumbnail :thumbnail-id="data.thumbnailId" :alt="data.title" style="width: 88px" />
          </template>
        </Column>
        <Column header="Título">
          <template #body="{ data }">
            <span class="title-cell">
              {{ data.title }}
              <Tag v-if="data.featured" value="Destacado" severity="warn" />
            </span>
          </template>
        </Column>
        <Column header="Duración">
          <template #body="{ data }">
            {{ formatDate(data.eventStartsAt) }} – {{ formatDate(data.eventEndsAt) }}
          </template>
        </Column>
        <Column header="Inscripción">
          <template #body="{ data }">
            {{ formatDateTime(data.signupStartsAt) }} – {{ formatDateTime(data.signupEndsAt) }}
          </template>
        </Column>
        <Column header="Acciones" style="width: 230px">
          <template #body="{ data }">
            <div class="row-actions">
              <Button
                :icon="data.featured ? 'pi pi-star-fill' : 'pi pi-star'"
                text
                rounded
                :aria-label="data.featured ? 'Evento destacado' : 'Destacar evento'"
                :disabled="data.featured || feature.isPending.value"
                :class="{ 'is-featured': data.featured }"
                @click="onFeature(data)"
              />
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

.title-cell {
  display: inline-flex;
  align-items: center;
  gap: 8px;
}

.is-featured:deep(.p-button-icon) {
  color: var(--ca-amber);
}
</style>
