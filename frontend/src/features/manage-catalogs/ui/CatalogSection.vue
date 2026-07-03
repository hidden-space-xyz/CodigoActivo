<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue'
import { useConfirm } from 'primevue/useconfirm'
import { AppButton as Button, DataState } from '@/shared/ui'
import Column from 'primevue/column'
import DataTable from 'primevue/datatable'
import Dialog from 'primevue/dialog'
import InputText from 'primevue/inputtext'
import Textarea from 'primevue/textarea'

import type { CatalogController, CatalogItem } from '../model/useCatalog'
import { useCrudFeedback } from '@/shared/lib'

const props = defineProps<{ title: string; controller: CatalogController }>()

const feedback = useCrudFeedback()
const confirm = useConfirm()

const dialogVisible = ref(false)
const selected = ref<CatalogItem | null>(null)
const submitted = ref(false)
const form = reactive<{ name: string; description: string }>({ name: '', description: '' })

const saving = computed(
  () => props.controller.create.isPending.value || props.controller.update.isPending.value,
)

watch(dialogVisible, (open) => {
  if (!open) return
  submitted.value = false
  form.name = selected.value?.name ?? ''
  form.description = selected.value?.description ?? ''
})

function openCreate(): void {
  selected.value = null
  dialogVisible.value = true
}

function openEdit(item: CatalogItem): void {
  selected.value = item
  dialogVisible.value = true
}

function save(): void {
  submitted.value = true
  if (!form.name.trim()) return
  const body = { name: form.name.trim(), description: form.description.trim() || null }
  if (selected.value?.id) {
    props.controller.update.mutate(
      { id: selected.value.id, body },
      {
        onSuccess: () => {
          feedback.success('Elemento actualizado.')
          dialogVisible.value = false
        },
        onError: (error) => feedback.error(error),
      },
    )
    return
  }
  props.controller.create.mutate(body, {
    onSuccess: () => {
      feedback.success('Elemento creado.')
      dialogVisible.value = false
    },
    onError: (error) => feedback.error(error),
  })
}

function confirmDelete(item: CatalogItem): void {
  confirm.require({
    header: 'Eliminar elemento',
    message: `¿Seguro que quieres eliminar "${item.name}"?`,
    icon: 'pi pi-exclamation-triangle',
    acceptLabel: 'Eliminar',
    rejectLabel: 'Cancelar',
    acceptClass: 'p-button-danger',
    accept: () => {
      if (!item.id) return
      props.controller.remove.mutate(item.id, {
        onSuccess: () => feedback.success('Elemento eliminado.'),
        onError: (error) => feedback.error(error),
      })
    },
  })
}
</script>

<template>
  <section class="catalog">
    <div class="catalog__head">
      <h2 class="catalog__title">{{ title }}</h2>
      <Button label="Nuevo" icon="pi pi-plus" size="small" @click="openCreate" />
    </div>

    <DataState
      :loading="controller.list.isLoading.value"
      :error="controller.list.isError.value"
      :empty="(controller.list.data.value?.length ?? 0) === 0"
      empty-text="Sin elementos."
    >
      <DataTable :value="controller.list.data.value" data-key="id" striped-rows>
        <Column field="name" header="Nombre" />
        <Column field="description" header="Descripción">
          <template #body="{ data }">{{ data.description || '—' }}</template>
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
    </DataState>

    <Dialog
      v-model:visible="dialogVisible"
      modal
      :header="selected ? 'Editar elemento' : 'Nuevo elemento'"
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
          <label>Descripción</label>
          <Textarea v-model="form.description" :maxlength="500" rows="3" auto-resize fluid />
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
</style>
