<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'

import { fullName, genderLabelKey, type User } from '@/entities/user'
import { SendEmailDialog, useSendEmail, useSendEmailDialog } from '@/features/send-email'
import { ageFrom, formatDate, todayIso } from '@/shared/lib/date'
import { type CsvValue, useCsvExport } from '@/shared/lib/download'
import { useCrudFeedback } from '@/shared/lib/feedback'
import { ActionButton } from '@/shared/ui/action-button'
import { AdminPageHeader } from '@/shared/ui/admin-page-header'
import { AppIcon } from '@/shared/ui/app-icon'
import { ColorTag } from '@/shared/ui/color-tag'
import {
  ColumnFilterDate,
  ColumnFilterSelect,
  ColumnSearch,
  toSelectOptions,
} from '@/shared/ui/column-filter'
import { DataTable } from '@/shared/ui/data-table'

import { useUsersAdmin } from '../model/use-users-admin'
import AdminPasswordDialog from './AdminPasswordDialog.vue'
import UserFormDialog from './UserFormDialog.vue'
import UserTypeDialog from './UserTypeDialog.vue'

const { t } = useI18n()
const feedback = useCrudFeedback()
const {
  table,
  types,
  statuses,
  relationFilter,
  showGuardianOf,
  showDependentsOf,
  clearRelationFilter,
  editDialog,
  editError,
  openEdit,
  saveUser,
  saving,
  typeDialog,
  saveType,
  changingType,
  grantDialog,
  grantError,
  toggleAdmin,
  grantAdmin,
  settingAdmin,
  resetDialog,
  resetError,
  openResetTwoFactor,
  resetUserTwoFactor,
  resettingTwoFactor,
  confirmRemove,
} = useUsersAdmin()
const { sendToUsers, usersAudience } = useSendEmail()

const editVisible = editDialog.visible
const editedUser = editDialog.editing
const loadingUser = editDialog.loading
const typeVisible = typeDialog.visible
const typedUser = typeDialog.editing
const grantVisible = grantDialog.visible
const grantedUser = grantDialog.editing
const resetVisible = resetDialog.visible
const resetUser = resetDialog.editing

const statusOptions = computed(() => toSelectOptions(statuses.data.value))
const typeOptions = computed(() => toSelectOptions(types.data.value))
const yesNoOptions: { label: string; value: boolean }[] = [
  { label: t('common.yes'), value: true },
  { label: t('common.no'), value: false },
]

function birthDateWithAge(user: User): string {
  const formatted = formatDate(user.birthDate)
  if (formatted === '—') return '—'
  const age = ageFrom(user.birthDate)
  return age === null ? formatted : t('pages.admin.users.birthDateWithAge', { formatted, age })
}

function dependentsLabel(count: number): string {
  return t('pages.admin.users.dependentsLabel', { count }, count)
}

const exportHeaders = [
  t('common.firstName'),
  t('common.lastName'),
  t('common.email'),
  t('common.phone'),
  t('common.secondaryPhone'),
  t('common.nationalId'),
  t('common.birthDate'),
  t('common.gender'),
  t('common.status'),
  t('pages.admin.users.columns.type'),
  t('pages.admin.users.columns.admin'),
  t('common.promotionalConsent'),
  t('pages.admin.users.export.columns.guardian'),
]

function exportRow(user: User): CsvValue[] {
  return [
    user.firstName,
    user.lastName,
    user.email,
    user.phone,
    user.secondaryPhone,
    user.nationalId,
    formatDate(user.birthDate),
    t(genderLabelKey(user.gender)),
    user.status.name,
    user.type?.name,
    user.isAdmin ? t('common.yes') : t('common.no'),
    user.promotionalConsent ? t('common.yes') : t('common.no'),
    user.parentName,
  ]
}

const { exporting, exportCsv } = useCsvExport<User>({
  fetchRows: table.fetchAll,
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
  recipients: emailRecipients,
  withoutConsent: emailWithoutConsent,
  open: openEmail,
  submit: submitEmail,
} = useSendEmailDialog<User>({
  idOf: (user) => user.id,
  targetOne: (user) => t('pages.admin.users.email.targetOne', { fullName: fullName(user) }),
  targetAll: () => t('pages.admin.users.email.targetFiltered', table.total.value),
  bulkPending: () => sendToUsers.isPending.value,
  sendAll: (payload, handlers) =>
    sendToUsers.mutate({ params: table.filterParams.value, payload }, handlers),
  fetchAudience: (user) => usersAudience(user ? { id: user.id } : table.filterParams.value),
  onError: (error) => feedback.error(error),
})
</script>

<template>
  <div>
    <AdminPageHeader
      :title="$t('pages.admin.users.header.title')"
      :subtitle="$t('pages.admin.users.header.subtitle')"
    >
      <template #actions>
        <ActionButton
          :label="$t('pages.admin.users.export.label')"
          :tooltip="$t('pages.admin.users.export.tooltip')"
          icon="download"
          :loading="exporting"
          :disabled="table.total.value === 0"
          @click="exportCsv"
        />
        <ActionButton
          :label="$t('pages.admin.users.email.bulkLabel')"
          :tooltip="$t('pages.admin.users.email.bulkTooltip')"
          icon="envelope"
          class="ca-action-icon--email"
          :disabled="table.total.value === 0"
          @click="openEmail(null)"
        />
      </template>
    </AdminPageHeader>

    <div v-if="relationFilter" class="relation-filter">
      <span class="relation-filter__icon"><AppIcon name="filter" /></span>
      <span class="relation-filter__label">{{ relationFilter.label }}</span>
      <ActionButton
        icon="times"
        text
        circle
        size="small"
        :aria-label="$t('pages.admin.users.relation.clear')"
        @click="clearRelationFilter"
      />
    </div>

    <DataTable
      :table="table"
      :empty-text="$t('pages.admin.users.empty.none')"
      :error-text="$t('pages.admin.users.empty.error')"
    >
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
        <template #default="{ row }">
          <div class="phone-cell">
            <span>{{ row.phone || '—' }}</span>
            <span v-if="row.secondaryPhone" class="phone-cell__secondary">{{
              row.secondaryPhone
            }}</span>
          </div>
        </template>
      </el-table-column>
      <el-table-column prop="nationalId" sortable="custom" min-width="150">
        <template #header>
          <ColumnSearch
            v-model="table.columnFilter('nationalId').value"
            :label="$t('pages.admin.users.columns.nationalId')"
            :placeholder="$t('pages.admin.users.search.nationalId')"
            @apply="table.onFilter"
          />
        </template>
        <template #default="{ row }">{{ row.nationalId || '—' }}</template>
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
      <el-table-column prop="promotionalConsent" sortable="custom" min-width="200">
        <template #header>
          <ColumnFilterSelect
            v-model="table.columnFilter('promotionalConsent').value"
            :label="$t('pages.admin.users.columns.promotionalConsent')"
            :options="yesNoOptions"
            @apply="table.onFilter"
          />
        </template>
        <template #default="{ row }">{{
          row.promotionalConsent ? $t('common.yes') : $t('common.no')
        }}</template>
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
          <ColorTag v-if="row.status.name" :value="row.status.name" :color="row.status.color" />
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
          <ColorTag v-if="row.type" :value="row.type.name" :color="row.type.color" />
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
            <ActionButton
              v-if="row.parentId"
              :label="row.parentName"
              icon="user"
              text
              size="small"
              :tooltip="$t('pages.admin.users.tooltips.showTutor')"
              @click="showGuardianOf(row)"
            />
            <ActionButton
              v-if="row.dependentCount > 0"
              :label="dependentsLabel(row.dependentCount)"
              icon="users"
              text
              size="small"
              :tooltip="$t('pages.admin.users.tooltips.showDependents')"
              @click="showDependentsOf(row)"
            />
            <span v-if="!row.parentId && row.dependentCount === 0">—</span>
          </div>
        </template>
      </el-table-column>
      <el-table-column prop="isAdmin" sortable="custom" width="130">
        <template #header>
          <ColumnFilterSelect
            v-model="table.columnFilter('isAdmin').value"
            :label="$t('pages.admin.users.columns.admin')"
            :options="yesNoOptions"
            @apply="table.onFilter"
          />
        </template>
        <template #default="{ row }">
          <el-switch
            :model-value="row.isAdmin"
            :disabled="settingAdmin || row.isInitialAdmin"
            :aria-label="$t('pages.admin.users.aria.admin')"
            @update:model-value="
              (value: string | number | boolean) => toggleAdmin(row, value === true)
            "
          />
        </template>
      </el-table-column>
      <el-table-column
        :label="$t('common.actions')"
        width="240"
        align="center"
        :fixed="table.actionsFixed.value"
      >
        <template #default="{ row }">
          <div class="ca-row-actions">
            <ActionButton
              icon="pencil"
              type="success"
              text
              circle
              :aria-label="$t('common.edit')"
              :disabled="loadingUser"
              @click="openEdit(row)"
            />
            <ActionButton
              icon="sync"
              text
              circle
              class="ca-action-icon--change-type"
              :aria-label="$t('pages.admin.users.aria.changeType')"
              @click="typeDialog.openEdit(row)"
            />
            <ActionButton
              v-if="row.email"
              icon="envelope"
              text
              circle
              class="ca-action-icon--email"
              :aria-label="$t('pages.admin.users.aria.sendEmail')"
              @click="openEmail(row)"
            />
            <ActionButton
              v-if="row.email"
              icon="undo"
              text
              circle
              type="warning"
              :aria-label="$t('pages.admin.users.aria.resetTwoFactor')"
              @click="openResetTwoFactor(row)"
            />
            <ActionButton
              icon="trash"
              text
              circle
              type="danger"
              :aria-label="$t('common.delete')"
              :disabled="row.isInitialAdmin"
              @click="confirmRemove(row)"
            />
          </div>
        </template>
      </el-table-column>
    </DataTable>

    <UserFormDialog
      v-model:visible="editVisible"
      :user="editedUser"
      :saving="saving"
      :error="editError"
      @submit="saveUser"
    />

    <AdminPasswordDialog
      v-model:visible="grantVisible"
      :title="$t('pages.admin.users.grantAdmin.header')"
      :message="
        $t('pages.admin.users.grantAdmin.message', { fullName: fullName(grantedUser ?? {}) })
      "
      :confirm-label="$t('pages.admin.users.grantAdmin.confirm')"
      input-id="grant-admin-password"
      width="min(440px, 92vw)"
      :saving="settingAdmin"
      :error="grantError"
      @submit="grantAdmin"
    />

    <AdminPasswordDialog
      v-model:visible="resetVisible"
      :title="$t('pages.admin.users.resetTwoFactor.header')"
      :message="
        resetUser?.usesAuthenticator
          ? $t('pages.admin.users.resetTwoFactor.message', { fullName: fullName(resetUser) })
          : $t('pages.admin.users.resetTwoFactor.messageEmail', {
              fullName: fullName(resetUser ?? {}),
            })
      "
      :confirm-label="$t('pages.admin.users.resetTwoFactor.confirm')"
      input-id="reset-two-factor-password"
      width="min(460px, 92vw)"
      :saving="resettingTwoFactor"
      :error="resetError"
      @submit="resetUserTwoFactor"
    />

    <UserTypeDialog
      v-model:visible="typeVisible"
      :user="typedUser"
      :options="typeOptions"
      :saving="changingType"
      @submit="saveType"
    />

    <SendEmailDialog
      v-model:visible="emailDialogVisible"
      :target="emailTarget"
      :sending="emailSending"
      :recipients="emailRecipients"
      :without-consent="emailWithoutConsent"
      @submit="submitEmail"
    />
  </div>
</template>

<style scoped>
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

.phone-cell {
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.phone-cell__secondary {
  font-size: 12.5px;
  color: var(--ca-text-muted);
}

.family-cell {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 2px;
}
</style>
