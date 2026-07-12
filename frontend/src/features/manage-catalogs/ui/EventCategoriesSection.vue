<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue'
import { AppButton as Button, ColorTag, ColumnSearch } from '@/shared/ui'
import ColorPicker from 'primevue/colorpicker'
import Column from 'primevue/column'
import DataTable from 'primevue/datatable'
import Dialog from 'primevue/dialog'
import InputText from 'primevue/inputtext'

import { useEventCategories } from '../model/categories'
import type { EventCategoryTypeResponse } from '@/shared/api/generated/models'
import { useCrudFeedback, useDeleteConfirm } from '@/shared/lib'

const { table, create, update, remove } = useEventCategories()
const feedback = useCrudFeedback()
const { confirmDelete: requireDelete } = useDeleteConfirm()

const dialogVisible = ref(false)
const selected = ref<EventCategoryTypeResponse | null>(null)
const submitted = ref(false)
const form = reactive<{ name: string; color: string }>({ name: '', color: '6366F1' })

const saving = computed(() => create.isPending.value || update.isPending.value)
const colorHex = computed(() => `#${form.color.replace(/^#/, '')}`)

watch(dialogVisible, (open) => {
  if (!open) return
  submitted.value = false
  form.name = selected.value?.name ?? ''
  form.color = (selected.value?.color ?? '#6366F1').replace(/^#/, '')
})

function openCreate(): void {
  selected.value = null
  dialogVisible.value = true
}

function openEdit(item: EventCategoryTypeResponse): void {
  selected.value = item
  dialogVisible.value = true
}

function save(): void {
  submitted.value = true
  if (!form.name.trim()) return
  const body = { name: form.name.trim(), color: colorHex.value }
  if (selected.value?.id) {
    update.mutate(
      { id: selected.value.id, body },
      {
        onSuccess: () => {
          feedback.success('Categoría actualizada.')
          dialogVisible.value = false
        },
        onError: (error) => feedback.error(error),
      },
    )
    return
  }
  create.mutate(body, {
    onSuccess: () => {
      feedback.success('Categoría creada.')
      dialogVisible.value = false
    },
    onError: (error) => feedback.error(error),
  })
}

function confirmDelete(item: EventCategoryTypeResponse): void {
  requireDelete({
    header: 'Eliminar categoría',
    message: `¿Seguro que quieres eliminar "${item.name}"? Se quitará de los eventos que la usen.`,
    accept: () => {
      if (!item.id) return
      remove.mutate(item.id, {
        onSuccess: () => feedback.success('Categoría eliminada.'),
        onError: (error) => feedback.error(error),
      })
    },
  })
}
</script>

<template>
  <section class="catalog">
    <div class="catalog__head">
      <h2 class="catalog__title">Categorías de eventos</h2>
      <Button label="Nueva" icon="pi pi-plus" size="small" @click="openCreate" />
    </div>

    <DataTable
      lazy
      :value="table.items.value"
      :total-records="table.total.value"
      :loading="table.loading.value"
      data-key="id"
      striped-rows
      paginator
      :rows="table.rows.value"
      :first="table.first.value"
      :rows-per-page-options="[25, 50, 100]"
      :sort-field="table.sortField.value"
      :sort-order="table.sortOrder.value"
      removable-sort
      @page="table.onPage"
      @sort="table.onSort"
    >
      <template #empty>
        <span v-if="table.isError.value">No se pudieron cargar las categorías.</span>
        <span v-else>Sin categorías.</span>
      </template>

      <Column field="name" sortable>
        <template #header>
          <ColumnSearch
            v-model="table.columnFilter('name').value"
            label="Nombre"
            placeholder="Buscar nombre"
            @apply="table.onFilter"
          />
        </template>
      </Column>
      <Column header="Color" style="width: 160px">
        <template #body="{ data }">
          <ColorTag :value="data.name ?? ''" :color="data.color" />
        </template>
      </Column>
      <Column header="Acciones" style="width: 120px">
        <template #body="{ data }">
          <div class="catalog__actions">
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

    <Dialog
      v-model:visible="dialogVisible"
      modal
      :header="selected ? 'Editar categoría' : 'Nueva categoría'"
      :style="{ width: '440px' }"
    >
      <form class="catalog__form" @submit.prevent="save">
        <div class="catalog__field">
          <label>Nombre</label>
          <InputText
            v-model="form.name"
            :maxlength="120"
            :invalid="submitted && !form.name.trim()"
            fluid
          />
        </div>
        <div class="catalog__field">
          <label>Color</label>
          <div class="catalog__color">
            <ColorPicker v-model="form.color" />
            <ColorTag :value="form.name.trim() || 'Ejemplo'" :color="colorHex" />
            <span class="catalog__hex">{{ colorHex }}</span>
          </div>
        </div>
      </form>
      <template #footer>
        <Button
          label="Cancelar"
          text
          severity="secondary"
          :disabled="saving"
          @click="dialogVisible = false"
        />
        <Button label="Guardar" :loading="saving" @click="save" />
      </template>
    </Dialog>
  </section>
</template>

<style scoped>
.catalog {
  background: var(--ca-surface);
  border: 1px solid var(--ca-border-soft);
  border-radius: 14px;
  padding: 18px 20px;
}

.catalog__head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 14px;
}

.catalog__title {
  font-family: var(--ca-font-display);
  font-size: 18px;
  font-weight: 600;
  color: var(--ca-text-bright);
}

.catalog__actions {
  display: flex;
  gap: 2px;
}

.catalog__form {
  display: flex;
  flex-direction: column;
  gap: 14px;
  padding-top: 6px;
}

.catalog__field {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.catalog__field label {
  font-size: 13px;
  font-weight: 600;
  color: var(--ca-text-muted);
}

.catalog__color {
  display: flex;
  align-items: center;
  gap: 12px;
}

.catalog__hex {
  font-family: var(--ca-font-mono);
  font-size: 13px;
  color: var(--ca-text-muted);
}
</style>
