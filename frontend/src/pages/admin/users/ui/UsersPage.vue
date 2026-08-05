<script setup lang="ts">
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import {
  AdminPageHeader,
  AppButton as Button,
  AppIcon,
  ColorTag,
  ColumnFilterDate,
  ColumnFilterSelect,
  ColumnSearch,
} from '@/shared/ui'

import { useUserStatusTypesList, useUserTypesList } from '@/entities/catalog'
import { UserFormDialog, useUsers } from '@/features/manage-users'
import { SendEmailDialog, useSendEmail, useSendEmailDialog } from '@/features/send-email'
import { genderLabel } from '@/entities/user'
import type {
  PostApiEmailsUsersParams,
  UpdateUserRequest,
  UserResponse,
} from '@/shared/api/generated/models'
import type { CsvValue } from '@/shared/lib'
import {
  ageFrom,
  formatDate,
  fullName,
  toSelectOptions,
  todayIso,
  useCrudFeedback,
  useCsvExport,
  useDeleteConfirm,
} from '@/shared/lib'

const { t } = useI18n()

const { table, relationFilter, update, remove, changeType, setAdmin, fetchOne, fetchAllUsers } =
  useUsers()
const userTypes = useUserTypesList()
const userStatusTypes = useUserStatusTypesList()
const feedback = useCrudFeedback()
const { confirmDelete: requireDelete } = useDeleteConfirm()
const { sendToUsers } = useSendEmail()

const dialogVisible = ref(false)
const selected = ref<UserResponse | null>(null)

const typeDialogVisible = ref(false)
const typeUser = ref<UserResponse | null>(null)
const selectedUserTypeId = ref<string | null>(null)

function birthDateWithAge(user: UserResponse): string {
  const formatted = formatDate(user.birthDate)
  if (formatted === '—') return '—'
  const age = ageFrom(user.birthDate)
  return age === null ? formatted : t('pages.admin.users.birthDateWithAge', { formatted, age })
}

function dependentsLabel(count: number): string {
  return t('pages.admin.users.dependentsLabel', { count }, count)
}

const statusOptions = computed(() => toSelectOptions(userStatusTypes.data.value))

const typeOptions = computed(() => toSelectOptions(userTypes.data.value))

const adminOptions: { label: string; value: boolean }[] = [
  { label: t('common.yes'), value: true },
  { label: t('common.no'), value: false },
]

function showTutorOf(user: UserResponse): void {
  if (!user.parentId) return
  table.clearFilters()
  relationFilter.value = {
    label: t('pages.admin.users.relation.tutorOf', { fullName: fullName(user) }),
    params: { id: user.parentId },
  }
}

function showDependentsOf(user: UserResponse): void {
  if (!user.id) return
  table.clearFilters()
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

const exportHeaders = [
  t('common.firstName'),
  t('common.lastName'),
  t('common.email'),
  t('common.phone'),
  t('common.birthDate'),
  t('common.gender'),
  t('common.status'),
  t('pages.admin.users.columns.type'),
  t('pages.admin.users.columns.admin'),
  t('pages.admin.users.export.columns.guardian'),
]

function exportRow(user: UserResponse): CsvValue[] {
  return [
    user.firstName,
    user.lastName,
    user.email,
    user.phone,
    formatDate(user.birthDate),
    user.gender ? genderLabel(user.gender) : null,
    user.status?.name,
    user.type?.name,
    user.isAdmin ? t('common.yes') : t('common.no'),
    user.parentName,
  ]
}

const { exporting, exportCsv } = useCsvExport<UserResponse>({
  fetchRows: fetchAllUsers,
  headers: exportHeaders,
  toRow: exportRow,
  filename: () => t('pages.admin.users.export.filename', { date: todayIso() }),
  onExported: (rows) => feedback.success(t('pages.admin.users.export.toast.exported', rows.length)),
  onError: (error) => feedback.error(error),
})

const {
  visible: emailDialogVisible,
  target: emailTarget,
  sending: emailSending,
  open: openEmail,
  submit: submitEmail,
} = useSendEmailDialog<UserResponse>({
  idOf: (user) => user.id,
  targetOne: (user) => t('pages.admin.users.email.targetOne', { fullName: fullName(user) }),
  targetAll: () => t('pages.admin.users.email.targetFiltered', table.total.value),
  bulkPending: () => sendToUsers.isPending.value,
  sendAll: (payload, handlers) =>
    sendToUsers.mutate(
      { params: table.filterParams.value as PostApiEmailsUsersParams, payload },
      handlers,
    ),
  onError: (error) => feedback.error(error),
})

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
    >
      <template #actions>
        <Button
          :label="$t('pages.admin.users.export.label')"
          :tooltip="$t('pages.admin.users.export.tooltip')"
          icon="download"
          :loading="exporting"
          :disabled="table.total.value === 0"
          @click="exportCsv"
        />
        <Button
          :label="$t('pages.admin.users.email.bulkLabel')"
          :tooltip="$t('pages.admin.users.email.bulkTooltip')"
          icon="envelope"
          :disabled="table.total.value === 0"
          @click="openEmail(null)"
        />
      </template>
    </AdminPageHeader>

    <div v-if="relationFilter" class="relation-filter">
      <span class="relation-filter__icon"><AppIcon name="filter" /></span>
      <span class="relation-filter__label">{{ relationFilter.label }}</span>
      <Button
        icon="times"
        text
        circle
        size="small"
        :aria-label="$t('pages.admin.users.relation.clear')"
        @click="clearRelationFilter"
      />
    </div>

    <el-table
      v-bind="table.tableProps.value"
      v-loading="table.loading.value"
      @sort-change="table.onSortChange"
    >
      <template #empty>
        <span v-if="table.isError.value">{{ $t('pages.admin.users.empty.error') }}</span>
        <span v-else>{{ $t('pages.admin.users.empty.none') }}</span>
      </template>

      <el-table-column prop="firstName" sortable="custom" min-width="170">
        <template #header>
          <ColumnSearch
            v-model="table.columnFilter('name').value"
            :label="$t('common.name')"
            :placeholder="$t('pages.admin.users.search.name')"
            @apply="table.onFilter"
          />
        </template>
        <template #default="{ row }">{{ fullName(row) }}</template>
      </el-table-column>
      <el-table-column prop="email" sortable="custom" min-width="290">
        <template #header>
          <ColumnSearch
            v-model="table.columnFilter('email').value"
            :label="$t('common.email')"
            :placeholder="$t('pages.admin.users.search.email')"
            @apply="table.onFilter"
          />
        </template>
        <template #default="{ row }">{{ row.email || '—' }}</template>
      </el-table-column>
      <el-table-column prop="phone" sortable="custom" min-width="165">
        <template #header>
          <ColumnSearch
            v-model="table.columnFilter('phone').value"
            :label="$t('common.phone')"
            :placeholder="$t('pages.admin.users.search.phone')"
            @apply="table.onFilter"
          />
        </template>
        <template #default="{ row }">{{ row.phone || '—' }}</template>
      </el-table-column>
      <el-table-column prop="birthDate" sortable="custom" min-width="185">
        <template #header>
          <ColumnFilterDate
            v-model="table.columnFilter('birthDate').value"
            :label="$t('pages.admin.users.columns.birth')"
            @apply="table.onFilter"
          />
        </template>
        <template #default="{ row }">{{ birthDateWithAge(row) }}</template>
      </el-table-column>
      <el-table-column prop="status" sortable="custom" min-width="145">
        <template #header>
          <ColumnFilterSelect
            v-model="table.columnFilter('status').value"
            :label="$t('common.status')"
            :options="statusOptions"
            @apply="table.onFilter"
          />
        </template>
        <template #default="{ row }">
          <ColorTag v-if="row.status?.name" :value="row.status.name" :color="row.status.color" />
          <span v-else>—</span>
        </template>
      </el-table-column>
      <el-table-column prop="type" sortable="custom" min-width="150">
        <template #header>
          <ColumnFilterSelect
            v-model="table.columnFilter('type').value"
            :label="$t('pages.admin.users.columns.type')"
            :options="typeOptions"
            @apply="table.onFilter"
          />
        </template>
        <template #default="{ row }">
          <ColorTag v-if="row.type" :value="row.type.name ?? ''" :color="row.type.color" />
          <span v-else>—</span>
        </template>
      </el-table-column>
      <el-table-column
        prop="dependents"
        :label="$t('pages.admin.users.columns.family')"
        sortable="custom"
        min-width="175"
      >
        <template #default="{ row }">
          <div class="family-cell">
            <Button
              v-if="row.parentId"
              :label="row.parentName ?? '—'"
              icon="user"
              text
              size="small"
              :tooltip="$t('pages.admin.users.tooltips.showTutor')"
              @click="showTutorOf(row)"
            />
            <Button
              v-if="(row.dependentCount ?? 0) > 0"
              :label="dependentsLabel(row.dependentCount ?? 0)"
              icon="users"
              text
              size="small"
              :tooltip="$t('pages.admin.users.tooltips.showDependents')"
              @click="showDependentsOf(row)"
            />
            <span v-if="!row.parentId && (row.dependentCount ?? 0) === 0">—</span>
          </div>
        </template>
      </el-table-column>
      <el-table-column prop="isAdmin" sortable="custom" width="130">
        <template #header>
          <ColumnFilterSelect
            v-model="table.columnFilter('isAdmin').value"
            :label="$t('pages.admin.users.columns.admin')"
            :options="adminOptions"
            @apply="table.onFilter"
          />
        </template>
        <template #default="{ row }">
          <el-switch
            :model-value="!!row.isAdmin"
            :disabled="setAdmin.isPending.value"
            :aria-label="$t('pages.admin.users.aria.admin')"
            @update:model-value="
              (value: string | number | boolean) => toggleAdmin(row, value === true)
            "
          />
        </template>
      </el-table-column>
      <el-table-column :label="$t('common.actions')" width="220" fixed="right">
        <template #default="{ row }">
          <div class="row-actions">
            <Button
              icon="pencil"
              text
              circle
              :aria-label="$t('common.edit')"
              @click="openEdit(row)"
            />
            <Button
              icon="sync"
              text
              circle
              :aria-label="$t('pages.admin.users.aria.changeType')"
              @click="openChangeType(row)"
            />
            <Button
              v-if="row.email"
              icon="envelope"
              text
              circle
              :aria-label="$t('pages.admin.users.aria.sendEmail')"
              @click="openEmail(row)"
            />
            <Button
              icon="trash"
              text
              circle
              type="danger"
              :aria-label="$t('common.delete')"
              @click="confirmDelete(row)"
            />
          </div>
        </template>
      </el-table-column>
    </el-table>

    <el-pagination
      v-bind="table.paginationProps.value"
      class="table-pagination"
      @current-change="table.onCurrentPageChange"
      @size-change="table.onPageSizeChange"
    />

    <UserFormDialog
      v-model:visible="dialogVisible"
      :user="selected"
      :saving="update.isPending.value"
      @submit="onSubmit"
    />

    <SendEmailDialog
      v-model:visible="emailDialogVisible"
      :target="emailTarget"
      :sending="emailSending"
      @submit="submitEmail"
    />

    <el-dialog
      v-model="typeDialogVisible"
      :title="$t('pages.admin.users.typeDialog.header')"
      width="420px"
    >
      <div class="form__field">
        <label>{{ $t('pages.admin.users.typeDialog.typeLabel') }}</label>
        <el-select
          v-model="selectedUserTypeId"
          :placeholder="$t('pages.admin.users.typeDialog.placeholder')"
          class="form__select"
        >
          <el-option
            v-for="option in typeOptions"
            :key="option.value"
            :label="option.label"
            :value="option.value"
          />
        </el-select>
      </div>
      <template #footer>
        <Button
          :label="$t('common.cancel')"
          text
          :disabled="changeType.isPending.value"
          @click="typeDialogVisible = false"
        />
        <Button
          :label="$t('common.apply')"
          type="primary"
          :loading="changeType.isPending.value"
          :disabled="!selectedUserTypeId"
          @click="submitChangeType"
        />
      </template>
    </el-dialog>
  </div>
</template>

<style scoped>
.row-actions {
  display: flex;
  gap: 2px;
}

.table-pagination {
  margin-top: 14px;
  justify-content: flex-end;
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
  display: inline-flex;
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

.form__select {
  width: 100%;
}
</style>
