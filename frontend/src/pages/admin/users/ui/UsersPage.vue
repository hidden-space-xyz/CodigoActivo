<script setup lang="ts">
import { computed, ref } from 'vue'
import { AdminPageHeader, AppButton as Button, ColorTag, DataState } from '@/shared/ui'
import Column from 'primevue/column'
import DataTable from 'primevue/datatable'
import Dialog from 'primevue/dialog'
import Select from 'primevue/select'
import Tag from 'primevue/tag'

import { useUserTypesList } from '@/entities/catalog'
import { UserFormDialog, useUsers } from '@/features/manage-users'
import type { UpdateUserRequest, UserResponse } from '@/shared/api/generated/models'
import { ageFrom, formatDate, groupByParent, useCrudFeedback, useDeleteConfirm } from '@/shared/lib'

const { list, update, remove, changeType, fetchOne } = useUsers()
const userTypes = useUserTypesList()
const feedback = useCrudFeedback()
const { confirmDelete: requireDelete } = useDeleteConfirm()

const dialogVisible = ref(false)
const selected = ref<UserResponse | null>(null)

const typeDialogVisible = ref(false)
const typeUser = ref<UserResponse | null>(null)
const selectedRoleId = ref<string | null>(null)

const grouped = computed(() =>
  groupByParent(
    list.data.value ?? [],
    (user) => user.id,
    (user) => user.parentId,
  ),
)

function userDepth(user: UserResponse): number {
  return user.id ? (grouped.value.depthById.get(user.id) ?? 0) : 0
}

function isChild(user: UserResponse): boolean {
  return userDepth(user) > 0
}

function childCount(user: UserResponse): number {
  return user.id ? (grouped.value.childrenByParent.get(user.id)?.length ?? 0) : 0
}

function rowClass(user: UserResponse): string {
  return isChild(user) ? 'user-row--child' : ''
}

function fullName(user: UserResponse): string {
  return `${user.firstName ?? ''} ${user.lastName ?? ''}`.trim() || '—'
}

function birthDateWithAge(user: UserResponse): string {
  const formatted = formatDate(user.birthDate)
  if (formatted === '—') return '—'
  const age = ageFrom(user.birthDate)
  return age === null ? formatted : `${formatted} (${age})`
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
  selectedRoleId.value = null
  typeDialogVisible.value = true
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

    <DataState
      :loading="list.isLoading.value"
      :error="list.isError.value"
      :empty="(list.data.value?.length ?? 0) === 0"
      empty-text="No hay usuarios."
    >
      <DataTable
        :value="grouped.rows"
        :row-class="rowClass"
        data-key="id"
        striped-rows
        paginator
        :rows="10"
      >
        <Column header="Nombre">
          <template #body="{ data }">
            <div
              class="user-name"
              :class="{ 'user-name--child': isChild(data) }"
              :style="{ paddingLeft: userDepth(data) * 22 + 'px' }"
            >
              <i v-if="isChild(data)" class="pi pi-angle-right user-name__child-icon" />
              <span>{{ fullName(data) }}</span>
              <Tag
                v-if="childCount(data) > 0"
                :value="String(childCount(data))"
                icon="pi pi-users"
                severity="secondary"
                class="user-name__count"
              />
            </div>
          </template>
        </Column>
        <Column field="email" header="Correo">
          <template #body="{ data }">{{ data.email || '—' }}</template>
        </Column>
        <Column field="phone" header="Teléfono">
          <template #body="{ data }">{{ data.phone || '—' }}</template>
        </Column>
        <Column header="Nacimiento">
          <template #body="{ data }">{{ birthDateWithAge(data) }}</template>
        </Column>
        <Column header="Estado">
          <template #body="{ data }">
            <ColorTag
              v-if="data.status?.name"
              :value="data.status.name"
              :color="data.status.color"
            />
            <span v-else>—</span>
          </template>
        </Column>
        <Column header="Tipos">
          <template #body="{ data }">
            <span v-if="(data.roles ?? []).length === 0">—</span>
            <ColorTag
              v-for="role in data.roles ?? []"
              :key="role.id"
              :value="role.name ?? ''"
              :color="role.color"
              class="user-role-tag"
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

.user-role-tag {
  margin-right: 4px;
}

.user-name {
  display: flex;
  align-items: center;
  gap: 8px;
}

.user-name--child {
  color: var(--ca-text-muted);
}

.user-name__child-icon {
  font-size: 12px;
  color: var(--ca-text-muted);
}

.user-name__count {
  font-size: 11px;
}

:deep(.user-row--child) > td:first-child {
  border-left: 2px solid var(--ca-border-soft);
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
