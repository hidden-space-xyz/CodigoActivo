<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import {
  AdminPageHeader,
  AppButton as Button,
  ColorTag,
  ColumnFilterSelect,
  ColumnSearch,
  DataState,
} from '@/shared/ui'
import Column from 'primevue/column'
import DataTable from 'primevue/datatable'
import type { DataTablePageEvent } from 'primevue/datatable'
import Dialog from 'primevue/dialog'
import Select from 'primevue/select'
import ToggleSwitch from 'primevue/toggleswitch'

import { useUserTypesList } from '@/entities/catalog'
import { UserFormDialog, useUsers } from '@/features/manage-users'
import type { UpdateUserRequest, UserResponse } from '@/shared/api/generated/models'
import { ageFrom, formatDate, useCrudFeedback, useDeleteConfirm } from '@/shared/lib'

const { list, update, remove, changeType, setAdmin, fetchOne } = useUsers()
const userTypes = useUserTypesList()
const feedback = useCrudFeedback()
const { confirmDelete: requireDelete } = useDeleteConfirm()

const dialogVisible = ref(false)
const selected = ref<UserResponse | null>(null)

const typeDialogVisible = ref(false)
const typeUser = ref<UserResponse | null>(null)
const selectedRoleId = ref<string | null>(null)

function fullName(user: UserResponse): string {
  return `${user.firstName ?? ''} ${user.lastName ?? ''}`.trim() || '—'
}

function birthDateWithAge(user: UserResponse): string {
  const formatted = formatDate(user.birthDate)
  if (formatted === '—') return '—'
  const age = ageFrom(user.birthDate)
  return age === null ? formatted : `${formatted} (${age})`
}

const users = computed(() => list.data.value ?? [])

const nameById = computed(() => {
  const map = new Map<string, string>()
  for (const user of users.value) {
    if (user.id) map.set(user.id, fullName(user))
  }
  return map
})

const dependentCountById = computed(() => {
  const map = new Map<string, number>()
  for (const user of users.value) {
    if (user.parentId) map.set(user.parentId, (map.get(user.parentId) ?? 0) + 1)
  }
  return map
})

function tutorName(parentId: string | null | undefined): string {
  if (!parentId) return '—'
  return nameById.value.get(parentId) ?? '—'
}

function dependentCount(user: UserResponse): number {
  return user.id ? (dependentCountById.value.get(user.id) ?? 0) : 0
}

function dependentsLabel(count: number): string {
  return count === 1 ? '1 dependiente' : `${count} dependientes`
}

const nameQuery = ref<string | number | null>(null)
const emailQuery = ref<string | number | null>(null)
const phoneQuery = ref<string | number | null>(null)
const statusId = ref<string | boolean | null>(null)
const typeId = ref<string | boolean | null>(null)
const adminFilter = ref<string | boolean | null>(null)

interface RelationFilter {
  kind: 'tutor' | 'dependents'
  userId: string
}

const relationFilter = ref<RelationFilter | null>(null)

const relationUser = computed(() => {
  const relation = relationFilter.value
  if (!relation) return null
  return users.value.find((user) => user.id === relation.userId) ?? null
})

const relationFilterLabel = computed(() => {
  const relation = relationFilter.value
  const origin = relationUser.value
  if (!relation || !origin) return ''
  return relation.kind === 'tutor'
    ? `Tutor de ${fullName(origin)}`
    : `Dependientes de ${fullName(origin)}`
})

const statusOptions = computed(() => {
  const seen = new Map<string, string>()
  for (const user of users.value) {
    const status = user.status
    if (status?.id && !seen.has(status.id)) seen.set(status.id, status.name ?? '—')
  }
  return Array.from(seen, ([value, label]) => ({ label, value }))
})

const typeOptions = computed(() =>
  (userTypes.data.value ?? []).map((type) => ({ label: type.name ?? '—', value: type.id ?? '' })),
)

const adminOptions: { label: string; value: boolean }[] = [
  { label: 'Sí', value: true },
  { label: 'No', value: false },
]

function textMatch(haystack: string, query: string | number | null): boolean {
  if (query == null || query === '') return true
  return haystack.toLowerCase().includes(String(query).toLowerCase())
}

function matchesRelation(user: UserResponse): boolean {
  const relation = relationFilter.value
  if (!relation) return true
  if (relation.kind === 'tutor') {
    const parentId = relationUser.value?.parentId
    return parentId != null && user.id === parentId
  }
  return user.parentId != null && user.parentId === relation.userId
}

function matches(user: UserResponse): boolean {
  return (
    matchesRelation(user) &&
    textMatch(fullName(user), nameQuery.value) &&
    textMatch(user.email ?? '', emailQuery.value) &&
    textMatch(user.phone ?? '', phoneQuery.value) &&
    (statusId.value == null || user.status?.id === statusId.value) &&
    (typeId.value == null || user.type?.id === typeId.value) &&
    (adminFilter.value == null || !!user.isAdmin === adminFilter.value)
  )
}

const rows = computed(() => users.value.filter(matches))

const first = ref(0)

function onPage(event: DataTablePageEvent): void {
  first.value = event.first
}

watch([nameQuery, emailQuery, phoneQuery, statusId, typeId, adminFilter, relationFilter], () => {
  first.value = 0
})

watch(relationUser, (current) => {
  if (relationFilter.value && !current) relationFilter.value = null
})

function clearColumnFilters(): void {
  nameQuery.value = null
  emailQuery.value = null
  phoneQuery.value = null
  statusId.value = null
  typeId.value = null
  adminFilter.value = null
}

function showTutorOf(user: UserResponse): void {
  if (!user.id) return
  clearColumnFilters()
  relationFilter.value = { kind: 'tutor', userId: user.id }
}

function showDependentsOf(user: UserResponse): void {
  if (!user.id) return
  clearColumnFilters()
  relationFilter.value = { kind: 'dependents', userId: user.id }
}

function clearRelationFilter(): void {
  relationFilter.value = null
}

async function openEdit(user: UserResponse): Promise<void> {
  selected.value = user
  dialogVisible.value = true
  if (!user.id) return
  try {
    selected.value = await fetchOne(user.id)
  } catch {}
}

function onSubmit(body: UpdateUserRequest): void {
  if (!selected.value?.id) return
  update.mutate(
    { id: selected.value.id, body },
    {
      onSuccess: () => {
        feedback.success('Usuario actualizado.')
        dialogVisible.value = false
      },
      onError: (error) => feedback.error(error),
    },
  )
}

function openChangeType(user: UserResponse): void {
  typeUser.value = user
  selectedRoleId.value = user.type?.id ?? null
  typeDialogVisible.value = true
}

function toggleAdmin(user: UserResponse, value: boolean): void {
  if (!user.id) return
  setAdmin.mutate(
    { id: user.id, isAdmin: value },
    {
      onSuccess: () =>
        feedback.success(value ? 'Administrador concedido.' : 'Administrador retirado.'),
      onError: (error) => feedback.error(error),
    },
  )
}

function submitChangeType(): void {
  if (!typeUser.value?.id || !selectedRoleId.value) return
  changeType.mutate(
    { id: typeUser.value.id, roleId: selectedRoleId.value },
    {
      onSuccess: () => {
        feedback.success('Tipo de usuario actualizado.')
        typeDialogVisible.value = false
      },
      onError: (error) => feedback.error(error),
    },
  )
}

function confirmDelete(user: UserResponse): void {
  requireDelete({
    header: 'Eliminar usuario',
    message: `¿Seguro que quieres eliminar a "${fullName(user)}"?`,
    accept: () => {
      if (!user.id) return
      remove.mutate(user.id, {
        onSuccess: () => feedback.success('Usuario eliminado.'),
        onError: (error) => feedback.error(error),
      })
    },
  })
}
</script>

<template>
  <div>
    <AdminPageHeader title="Usuarios" subtitle="Personas registradas en la plataforma" />

    <div v-if="relationFilter" class="relation-filter">
      <i class="pi pi-filter relation-filter__icon" />
      <span class="relation-filter__label">{{ relationFilterLabel }}</span>
      <Button
        icon="pi pi-times"
        text
        rounded
        size="small"
        aria-label="Quitar filtro"
        @click="clearRelationFilter"
      />
    </div>

    <DataState
      :loading="list.isLoading.value"
      :error="list.isError.value"
      :empty="(list.data.value?.length ?? 0) === 0"
      empty-text="No hay usuarios."
    >
      <DataTable
        :value="rows"
        data-key="id"
        striped-rows
        paginator
        :rows="10"
        :first="first"
        removable-sort
        @page="onPage"
      >
        <template #empty>Sin coincidencias.</template>

        <Column field="firstName" sortable>
          <template #header>
            <ColumnSearch v-model="nameQuery" label="Nombre" placeholder="Buscar nombre" />
          </template>
          <template #body="{ data }">{{ fullName(data) }}</template>
        </Column>
        <Column field="email" sortable>
          <template #header>
            <ColumnSearch v-model="emailQuery" label="Correo" placeholder="Buscar correo" />
          </template>
          <template #body="{ data }">{{ data.email || '—' }}</template>
        </Column>
        <Column field="phone" sortable>
          <template #header>
            <ColumnSearch v-model="phoneQuery" label="Teléfono" placeholder="Buscar teléfono" />
          </template>
          <template #body="{ data }">{{ data.phone || '—' }}</template>
        </Column>
        <Column field="birthDate" header="Nacimiento" sortable>
          <template #body="{ data }">{{ birthDateWithAge(data) }}</template>
        </Column>
        <Column field="status.name" sortable>
          <template #header>
            <ColumnFilterSelect v-model="statusId" label="Estado" :options="statusOptions" />
          </template>
          <template #body="{ data }">
            <ColorTag
              v-if="data.status?.name"
              :value="data.status.name"
              :color="data.status.color"
            />
            <span v-else>—</span>
          </template>
        </Column>
        <Column field="type.name" sortable>
          <template #header>
            <ColumnFilterSelect v-model="typeId" label="Tipo" :options="typeOptions" />
          </template>
          <template #body="{ data }">
            <ColorTag v-if="data.type" :value="data.type.name ?? ''" :color="data.type.color" />
            <span v-else>—</span>
          </template>
        </Column>
        <Column header="Familia">
          <template #body="{ data }">
            <div class="family-cell">
              <Button
                v-if="data.parentId"
                :label="tutorName(data.parentId)"
                icon="pi pi-user"
                text
                size="small"
                tooltip="Mostrar solo a su tutor"
                @click="showTutorOf(data)"
              />
              <Button
                v-if="dependentCount(data) > 0"
                :label="dependentsLabel(dependentCount(data))"
                icon="pi pi-users"
                text
                size="small"
                tooltip="Mostrar sus dependientes"
                @click="showDependentsOf(data)"
              />
              <span v-if="!data.parentId && dependentCount(data) === 0">—</span>
            </div>
          </template>
        </Column>
        <Column field="isAdmin" sortable style="width: 130px">
          <template #header>
            <ColumnFilterSelect v-model="adminFilter" label="Admin" :options="adminOptions" />
          </template>
          <template #body="{ data }">
            <ToggleSwitch
              :model-value="!!data.isAdmin"
              :disabled="setAdmin.isPending.value"
              aria-label="Administrador"
              @update:model-value="(value: boolean) => toggleAdmin(data, value)"
            />
          </template>
        </Column>
        <Column header="Acciones" style="width: 180px">
          <template #body="{ data }">
            <div class="row-actions">
              <Button
                icon="pi pi-pencil"
                text
                rounded
                aria-label="Editar"
                @click="openEdit(data)"
              />
              <Button
                icon="pi pi-sync"
                text
                rounded
                aria-label="Cambiar tipo"
                @click="openChangeType(data)"
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

    <UserFormDialog
      v-model:visible="dialogVisible"
      :user="selected"
      :saving="update.isPending.value"
      @submit="onSubmit"
    />

    <Dialog
      v-model:visible="typeDialogVisible"
      modal
      header="Cambiar tipo de usuario"
      :style="{ width: '420px' }"
    >
      <div class="form__field">
        <label>Tipo de usuario</label>
        <Select
          v-model="selectedRoleId"
          :options="userTypes.data.value ?? []"
          option-label="name"
          option-value="id"
          placeholder="Selecciona un tipo"
          fluid
        />
      </div>
      <template #footer>
        <Button
          label="Cancelar"
          text
          severity="secondary"
          :disabled="changeType.isPending.value"
          @click="typeDialogVisible = false"
        />
        <Button
          label="Aplicar"
          :loading="changeType.isPending.value"
          :disabled="!selectedRoleId"
          @click="submitChangeType"
        />
      </template>
    </Dialog>
  </div>
</template>

<style scoped>
.row-actions {
  display: flex;
  gap: 2px;
}

.relation-filter {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  margin-bottom: 14px;
  padding: 4px 6px 4px 14px;
  border: 1px solid var(--ca-border-soft);
  border-radius: 999px;
  background: var(--ca-surface);
}

.relation-filter__icon {
  font-size: 12px;
  color: var(--ca-text-muted);
}

.relation-filter__label {
  font-size: 13px;
  font-weight: 600;
}

.family-cell {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
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
