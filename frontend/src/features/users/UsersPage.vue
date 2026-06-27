<script setup lang="ts">
import { ref } from 'vue'
import { useConfirm } from 'primevue/useconfirm'
import Button from 'primevue/button'
import Column from 'primevue/column'
import DataTable from 'primevue/datatable'
import Dialog from 'primevue/dialog'
import Select from 'primevue/select'
import Tag from 'primevue/tag'

import { useUserTypesList } from '@/features/catalogs/catalogs'
import UserFormDialog from '@/features/users/UserFormDialog.vue'
import { useUsers } from '@/features/users/useUsers'
import type { UpdateUserRequest, UserResponse } from '@/shared/api/generated/models'
import { getErrorMessage } from '@/shared/utils/api-error'
import AdminPageHeader from '@/shared/ui/admin/AdminPageHeader.vue'
import DataState from '@/shared/ui/admin/DataState.vue'
import { useCrudFeedback } from '@/shared/ui/admin/use-crud-feedback'

const { list, update, remove, changeType, fetchOne } = useUsers()
const userTypes = useUserTypesList()
const feedback = useCrudFeedback()
const confirm = useConfirm()

const dialogVisible = ref(false)
const selected = ref<UserResponse | null>(null)

const typeDialogVisible = ref(false)
const typeUser = ref<UserResponse | null>(null)
const selectedRoleId = ref<string | null>(null)

function fullName(user: UserResponse): string {
  return `${user.firstName ?? ''} ${user.lastName ?? ''}`.trim() || '—'
}

function roleNames(user: UserResponse): string[] {
  return (user.roles ?? []).map((role) => role.name ?? '').filter((name) => name.length > 0)
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
      onError: (error) => feedback.error(getErrorMessage(error)),
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
      onError: (error) => feedback.error(getErrorMessage(error)),
    },
  )
}

function confirmDelete(user: UserResponse): void {
  confirm.require({
    header: 'Eliminar usuario',
    message: `¿Seguro que quieres eliminar a "${fullName(user)}"?`,
    icon: 'pi pi-exclamation-triangle',
    acceptLabel: 'Eliminar',
    rejectLabel: 'Cancelar',
    acceptClass: 'p-button-danger',
    accept: () => {
      if (!user.id) return
      remove.mutate(user.id, {
        onSuccess: () => feedback.success('Usuario eliminado.'),
        onError: (error) => feedback.error(getErrorMessage(error)),
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
      <DataTable :value="list.data.value" data-key="id" striped-rows paginator :rows="10">
        <Column header="Nombre">
          <template #body="{ data }">{{ fullName(data) }}</template>
        </Column>
        <Column field="email" header="Correo">
          <template #body="{ data }">{{ data.email || '—' }}</template>
        </Column>
        <Column header="Estado">
          <template #body="{ data }">
            <Tag v-if="data.status?.name" :value="data.status.name" severity="info" />
            <span v-else>—</span>
          </template>
        </Column>
        <Column header="Tipos">
          <template #body="{ data }">
            <span v-if="roleNames(data).length === 0">—</span>
            <Tag v-for="role in roleNames(data)" :key="role" :value="role" class="user-role-tag" />
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
