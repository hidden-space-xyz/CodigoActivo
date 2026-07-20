<script setup lang="ts">
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import {
  AdminPageHeader,
  AppButton as Button,
  ColorTag,
  ColumnFilterDate,
  ColumnFilterSelect,
  ColumnSearch,
} from '@/shared/ui'
import Column from 'primevue/column'
import DataTable from 'primevue/datatable'
import Dialog from 'primevue/dialog'
import Select from 'primevue/select'
import ToggleSwitch from 'primevue/toggleswitch'

import { useUserStatusTypesList, useUserTypesList } from '@/entities/catalog'
import { UserFormDialog, useUsers } from '@/features/manage-users'
import type { UpdateUserRequest, UserResponse } from '@/shared/api/generated/models'
import { ageFrom, formatDate, useCrudFeedback, useDeleteConfirm } from '@/shared/lib'

const { t } = useI18n()

const { table, relationFilter, update, remove, changeType, setAdmin, fetchOne } = useUsers()
const userTypes = useUserTypesList()
const userStatusTypes = useUserStatusTypesList()
const feedback = useCrudFeedback()
const { confirmDelete: requireDelete } = useDeleteConfirm()

const dialogVisible = ref(false)
const selected = ref<UserResponse | null>(null)

const typeDialogVisible = ref(false)
const typeUser = ref<UserResponse | null>(null)
const selectedUserTypeId = ref<string | null>(null)

function fullName(user: UserResponse): string {
  return `${user.firstName ?? ''} ${user.lastName ?? ''}`.trim() || '—'
}

function birthDateWithAge(user: UserResponse): string {
  const formatted = formatDate(user.birthDate)
  if (formatted === '—') return '—'
  const age = ageFrom(user.birthDate)
  return age === null ? formatted : t('pages.admin.users.birthDateWithAge', { formatted, age })
}

function dependentsLabel(count: number): string {
  return t('pages.admin.users.dependentsLabel', { count }, count)
}

const statusOptions = computed(() =>
  (userStatusTypes.data.value ?? []).map((status) => ({
    label: status.name ?? '—',
    value: status.id ?? '',
  })),
)

const typeOptions = computed(() =>
  (userTypes.data.value ?? []).map((type) => ({ label: type.name ?? '—', value: type.id ?? '' })),
)

const adminOptions: { label: string; value: boolean }[] = [
  { label: t('common.yes'), value: true },
  { label: t('common.no'), value: false },
]

function clearColumnFilters(): void {
  for (const key of ['name', 'email', 'phone', 'birthDate', 'status', 'type', 'isAdmin']) {
    table.columnFilter(key).value = null
  }
  table.onFilter()
}

function showTutorOf(user: UserResponse): void {
  if (!user.parentId) return
  clearColumnFilters()
  relationFilter.value = {
    label: t('pages.admin.users.relation.tutorOf', { fullName: fullName(user) }),
    params: { id: user.parentId },
  }
}

function showDependentsOf(user: UserResponse): void {
  if (!user.id) return
  clearColumnFilters()
  relationFilter.value = {
    label: t('pages.admin.users.relation.dependentsOf', { fullName: fullName(user) }),
    params: { parentId: user.id },
  }
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
        feedback.success(t('pages.admin.users.toasts.updated'))
        dialogVisible.value = false
      },
      onError: (error) => feedback.error(error),
    },
  )
}

function openChangeType(user: UserResponse): void {
  typeUser.value = user
  selectedUserTypeId.value = user.type?.id ?? null
  typeDialogVisible.value = true
}

function toggleAdmin(user: UserResponse, value: boolean): void {
  if (!user.id) return
  setAdmin.mutate(
    { id: user.id, isAdmin: value },
    {
      onSuccess: () =>
        feedback.success(
          value
            ? t('pages.admin.users.toasts.adminGranted')
            : t('pages.admin.users.toasts.adminRevoked'),
        ),
      onError: (error) => feedback.error(error),
    },
  )
}

function submitChangeType(): void {
  if (!typeUser.value?.id || !selectedUserTypeId.value) return
  changeType.mutate(
    { id: typeUser.value.id, userTypeId: selectedUserTypeId.value },
    {
      onSuccess: () => {
        feedback.success(t('pages.admin.users.toasts.typeUpdated'))
        typeDialogVisible.value = false
      },
      onError: (error) => feedback.error(error),
    },
  )
}

function confirmDelete(user: UserResponse): void {
  requireDelete({
    header: t('pages.admin.users.delete.header'),
    message: t('pages.admin.users.delete.message', { fullName: fullName(user) }),
    accept: () => {
      if (!user.id) return
      remove.mutate(user.id, {
        onSuccess: () => feedback.success(t('pages.admin.users.toasts.deleted')),
        onError: (error) => feedback.error(error),
      })
    },
  })
}
</script>

<template>
  <div>
    <AdminPageHeader
      :title="$t('pages.admin.users.header.title')"
      :subtitle="$t('pages.admin.users.header.subtitle')"
    />

    <div v-if="relationFilter" class="relation-filter">
      <i class="pi pi-filter relation-filter__icon" />
      <span class="relation-filter__label">{{ relationFilter.label }}</span>
      <Button
        icon="pi pi-times"
        text
        rounded
        size="small"
        :aria-label="$t('pages.admin.users.relation.clear')"
        @click="clearRelationFilter"
      />
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
        <span v-if="table.isError.value">{{ $t('pages.admin.users.empty.error') }}</span>
        <span v-else>{{ $t('pages.admin.users.empty.none') }}</span>
      </template>

      <Column field="firstName" sortable>
        <template #header>
          <ColumnSearch
            v-model="table.columnFilter('name').value"
            :label="$t('common.name')"
            :placeholder="$t('pages.admin.users.search.name')"
            @apply="table.onFilter"
          />
        </template>
        <template #body="{ data }">{{ fullName(data) }}</template>
      </Column>
      <Column field="email" sortable>
        <template #header>
          <ColumnSearch
            v-model="table.columnFilter('email').value"
            :label="$t('common.email')"
            :placeholder="$t('pages.admin.users.search.email')"
            @apply="table.onFilter"
          />
        </template>
        <template #body="{ data }">{{ data.email || '—' }}</template>
      </Column>
      <Column field="phone" sortable>
        <template #header>
          <ColumnSearch
            v-model="table.columnFilter('phone').value"
            :label="$t('common.phone')"
            :placeholder="$t('pages.admin.users.search.phone')"
            @apply="table.onFilter"
          />
        </template>
        <template #body="{ data }">{{ data.phone || '—' }}</template>
      </Column>
      <Column field="birthDate" sortable>
        <template #header>
          <ColumnFilterDate
            v-model="table.columnFilter('birthDate').value"
            :label="$t('pages.admin.users.columns.birth')"
            @apply="table.onFilter"
          />
        </template>
        <template #body="{ data }">{{ birthDateWithAge(data) }}</template>
      </Column>
      <Column field="status" sortable>
        <template #header>
          <ColumnFilterSelect
            v-model="table.columnFilter('status').value"
            :label="$t('common.status')"
            :options="statusOptions"
            @apply="table.onFilter"
          />
        </template>
        <template #body="{ data }">
          <ColorTag v-if="data.status?.name" :value="data.status.name" :color="data.status.color" />
          <span v-else>—</span>
        </template>
      </Column>
      <Column field="type" sortable>
        <template #header>
          <ColumnFilterSelect
            v-model="table.columnFilter('type').value"
            :label="$t('pages.admin.users.columns.type')"
            :options="typeOptions"
            @apply="table.onFilter"
          />
        </template>
        <template #body="{ data }">
          <ColorTag v-if="data.type" :value="data.type.name ?? ''" :color="data.type.color" />
          <span v-else>—</span>
        </template>
      </Column>
      <Column :header="$t('pages.admin.users.columns.family')" sortable sort-field="dependents">
        <template #body="{ data }">
          <div class="family-cell">
            <Button
              v-if="data.parentId"
              :label="data.parentName ?? '—'"
              icon="pi pi-user"
              text
              size="small"
              :tooltip="$t('pages.admin.users.tooltips.showTutor')"
              @click="showTutorOf(data)"
            />
            <Button
              v-if="(data.dependentCount ?? 0) > 0"
              :label="dependentsLabel(data.dependentCount ?? 0)"
              icon="pi pi-users"
              text
              size="small"
              :tooltip="$t('pages.admin.users.tooltips.showDependents')"
              @click="showDependentsOf(data)"
            />
            <span v-if="!data.parentId && (data.dependentCount ?? 0) === 0">—</span>
          </div>
        </template>
      </Column>
      <Column field="isAdmin" sortable style="width: 130px">
        <template #header>
          <ColumnFilterSelect
            v-model="table.columnFilter('isAdmin').value"
            :label="$t('pages.admin.users.columns.admin')"
            :options="adminOptions"
            @apply="table.onFilter"
          />
        </template>
        <template #body="{ data }">
          <ToggleSwitch
            :model-value="!!data.isAdmin"
            :disabled="setAdmin.isPending.value"
            :aria-label="$t('pages.admin.users.aria.admin')"
            @update:model-value="(value: boolean) => toggleAdmin(data, value)"
          />
        </template>
      </Column>
      <Column :header="$t('common.actions')" style="width: 180px">
        <template #body="{ data }">
          <div class="row-actions">
            <Button
              icon="pi pi-pencil"
              text
              rounded
              :aria-label="$t('common.edit')"
              @click="openEdit(data)"
            />
            <Button
              icon="pi pi-sync"
              text
              rounded
              :aria-label="$t('pages.admin.users.aria.changeType')"
              @click="openChangeType(data)"
            />
            <Button
              icon="pi pi-trash"
              text
              rounded
              severity="danger"
              :aria-label="$t('common.delete')"
              @click="confirmDelete(data)"
            />
          </div>
        </template>
      </Column>
    </DataTable>

    <UserFormDialog
      v-model:visible="dialogVisible"
      :user="selected"
      :saving="update.isPending.value"
      @submit="onSubmit"
    />

    <Dialog
      v-model:visible="typeDialogVisible"
      modal
      :header="$t('pages.admin.users.typeDialog.header')"
      :style="{ width: '420px' }"
    >
      <div class="form__field">
        <label>{{ $t('pages.admin.users.typeDialog.typeLabel') }}</label>
        <Select
          v-model="selectedUserTypeId"
          :options="userTypes.data.value ?? []"
          option-label="name"
          option-value="id"
          :placeholder="$t('pages.admin.users.typeDialog.placeholder')"
          fluid
        />
      </div>
      <template #footer>
        <Button
          :label="$t('common.cancel')"
          text
          severity="secondary"
          :disabled="changeType.isPending.value"
          @click="typeDialogVisible = false"
        />
        <Button
          :label="$t('common.apply')"
          :loading="changeType.isPending.value"
          :disabled="!selectedUserTypeId"
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
