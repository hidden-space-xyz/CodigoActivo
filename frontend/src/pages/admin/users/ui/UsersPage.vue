<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'

import { fullName, STAFF_ROLES, staffRoleLabelKey, type User } from '@/entities/user'
import { SendEmailDialog, useSendEmail, useSendEmailDialog } from '@/features/send-email'
import { ageFrom, formatDate } from '@/shared/lib/date'
import { ActionButton } from '@/shared/ui/action-button'
import { AdminPageHeader } from '@/shared/ui/admin-page-header'
import { ColorTag } from '@/shared/ui/color-tag'
import {
  ColumnFilterDate,
  ColumnFilterSelect,
  ColumnSearch,
  toSelectOptions,
} from '@/shared/ui/column-filter'
import { DataTable } from '@/shared/ui/data-table'
import { RowActions, type RowAction } from '@/shared/ui/row-actions'
import { TablePrimaryCell } from '@/shared/ui/table-primary-cell'

import { useUsersAdmin } from '../model/use-users-admin'
import AdminPasswordDialog from './AdminPasswordDialog.vue'
import StaffRoleDialog from './StaffRoleDialog.vue'
import UserTypeDialog from './UserTypeDialog.vue'

const { t } = useI18n()
const {
  table,
  types,
  statuses,
  scope,
  setScope,
  showsField,
  relationChips,
  showGuardianOf,
  showDependentsOf,
  isOwnAccount,
  typeBlockReason,
  mayChangeStaffRole,
  mayDesignatePrivacyOfficer,
  mayDelete,
  deleteBlockReason,
  typeDialog,
  saveType,
  changingType,
  staffRoleDialog,
  staffRoleError,
  openStaffRole,
  saveStaffRole,
  settingStaffRole,
  officerDialog,
  officerError,
  openPrivacyOfficer,
  designatePrivacyOfficer,
  designatingOfficer,
  resetDialog,
  resetError,
  mayResetTwoFactor,
  openResetTwoFactor,
  resetUserTwoFactor,
  resettingTwoFactor,
  confirmRemove,
} = useUsersAdmin()
const { sendToUsers, usersAudience } = useSendEmail()

const typeVisible = typeDialog.visible
const typedUser = typeDialog.editing
const staffRoleVisible = staffRoleDialog.visible
const staffRoleUser = staffRoleDialog.editing
const officerVisible = officerDialog.visible
const officerUser = officerDialog.editing
const resetVisible = resetDialog.visible
const resetUser = resetDialog.editing

const scopeOptions = computed(() => [
  { label: t('pages.admin.users.scope.independent'), value: 'independent' },
  { label: t('pages.admin.users.scope.dependent'), value: 'dependent' },
  { label: t('pages.admin.users.scope.all'), value: 'all' },
])

const emptyText = computed(() => {
  if (scope.value === 'independent') return t('pages.admin.users.empty.noIndependent')
  if (scope.value === 'dependent') return t('pages.admin.users.empty.noDependent')
  return t('pages.admin.users.empty.none')
})

const statusOptions = computed(() => toSelectOptions(statuses.data.value))
const typeOptions = computed(() => toSelectOptions(types.data.value))
const yesNoOptions: { label: string; value: boolean }[] = [
  { label: t('common.yes'), value: true },
  { label: t('common.no'), value: false },
]

function ageLabel(user: User): string | null {
  const age = ageFrom(user.birthDate)
  return age === null ? null : t('pages.admin.users.age', age)
}

function hasFamily(user: User): boolean {
  return Boolean(user.parentId) || user.dependentCount > 0
}

function dependentsLabel(count: number): string {
  return t('pages.admin.users.dependentsLabel', { count })
}

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
  fetchAudience: () => usersAudience(table.filterParams.value),
})

function blockedBy(reason: string | undefined): Pick<RowAction, 'disabled' | 'reason'> {
  return reason === undefined ? { disabled: false } : { disabled: true, reason }
}

function userActions(user: User): RowAction[] {
  const actions: RowAction[] = [
    {
      key: 'changeType',
      label: t('pages.admin.users.actions.changeType'),
      icon: 'tag',
      ...blockedBy(typeBlockReason(user)),
      run: () => {
        void typeDialog.openEdit(user)
      },
    },
  ]
  if (user.email) {
    actions.push({
      key: 'sendEmail',
      label: t('pages.admin.users.actions.sendEmail'),
      icon: 'envelope',
      run: () => {
        openEmail(user)
      },
    })
  }
  if (mayResetTwoFactor(user)) {
    actions.push({
      key: 'resetTwoFactor',
      label: t('pages.admin.users.actions.resetTwoFactor'),
      icon: 'lock',
      run: () => {
        openResetTwoFactor(user)
      },
    })
  }
  if (mayChangeStaffRole(user)) {
    actions.push({
      key: 'staffRole',
      label: t('pages.admin.users.actions.changeStaffRole'),
      icon: 'key',
      disabled: settingStaffRole.value,
      run: () => {
        openStaffRole(user)
      },
    })
  }
  if (mayDesignatePrivacyOfficer(user)) {
    actions.push({
      key: 'privacyOfficer',
      label: t('pages.admin.users.actions.designatePrivacyOfficer'),
      icon: 'id-card',
      disabled: designatingOfficer.value,
      run: () => {
        openPrivacyOfficer(user)
      },
    })
  }
  if (mayDelete.value) {
    actions.push({
      key: 'delete',
      label: t('common.delete'),
      icon: 'trash',
      danger: true,
      divided: true,
      ...blockedBy(deleteBlockReason(user)),
      run: () => {
        confirmRemove(user)
      },
    })
  }
  return actions
}

function staffRoleLabel(user: User): string {
  if (user.isInitialAdmin) return t('pages.admin.users.staffRole.superAdmin')
  return user.staffRole === 'None' ? '—' : t(staffRoleLabelKey(user.staffRole))
}

const staffRoleOptions = computed(() =>
  STAFF_ROLES.map((role) => ({ label: t(staffRoleLabelKey(role)), value: role })),
)
</script>

<template>
  <div>
    <AdminPageHeader
      :title="$t('pages.admin.users.header.title')"
      :subtitle="$t('pages.admin.users.header.subtitle')"
    />

    <DataTable
      :table="table"
      :empty-text="emptyText"
      :error-text="$t('pages.admin.users.empty.error')"
      :search="{ key: 'name', label: $t('pages.admin.users.search.name') }"
      :extra-filters="relationChips"
    >
      <template #filters>
        <el-segmented
          size="small"
          data-testid="users-scope"
          :aria-label="$t('pages.admin.users.scope.label')"
          :options="scopeOptions"
          :model-value="scope"
          @change="setScope"
        >
          <template #default="{ item }">
            <span :data-testid="`users-scope-${item.value}`">{{ item.label }}</span>
          </template>
        </el-segmented>
        <ColumnFilterSelect
          v-if="showsField('newsConsent')"
          v-model="table.columnFilter('newsConsent').value"
          data-testid="users-filter-news-consent"
          variant="toolbar"
          :label="$t('pages.admin.users.columns.newsConsent')"
          :options="yesNoOptions"
          @apply="table.onFilter"
        />
        <ColumnFilterDate
          v-if="showsField('birthDate')"
          v-model="table.columnFilter('birthDate').value"
          data-testid="users-filter-birth-date"
          variant="toolbar"
          :label="$t('pages.admin.users.columns.birth')"
          @apply="table.onFilter"
        />
      </template>
      <template #actions>
        <ActionButton
          :label="$t('pages.admin.users.email.bulkLabel')"
          data-testid="users-bulk-email"
          :tooltip="$t('pages.admin.users.email.bulkTooltip')"
          icon="envelope"
          size="small"
          :disabled="table.total.value === 0"
          @click="openEmail(null)"
        />
      </template>

      <el-table-column
        prop="firstName"
        :label="$t('pages.admin.users.columns.user')"
        sortable="custom"
        min-width="210"
        :fixed="table.isNarrow.value ? false : 'left'"
      >
        <template #default="{ row }">
          <TablePrimaryCell :title="fullName(row)">
            <template v-if="isOwnAccount(row)" #badges>
              <el-tag size="small" type="info" disable-transitions data-testid="users-you">{{
                $t('pages.admin.users.badges.you')
              }}</el-tag>
            </template>
            <template v-if="ageLabel(row) !== null || hasFamily(row)" #meta>
              <span class="user-meta">
                <span v-if="row.parentId" data-testid="users-guardian-prefix">{{
                  $t('pages.admin.users.guardianPrefix')
                }}</span>
                <button
                  v-if="row.parentId"
                  type="button"
                  class="user-meta__link"
                  :title="$t('pages.admin.users.tooltips.showTutor')"
                  data-testid="users-guardian"
                  @click="showGuardianOf(row)"
                >
                  {{ row.parentName }}
                </button>
                <button
                  v-else-if="row.dependentCount > 0"
                  type="button"
                  class="user-meta__link"
                  :title="$t('pages.admin.users.tooltips.showDependents')"
                  data-testid="users-dependents"
                  @click="showDependentsOf(row)"
                >
                  {{ dependentsLabel(row.dependentCount) }}
                </button>
                <span v-if="ageLabel(row) !== null && hasFamily(row)" aria-hidden="true">·</span>
                <span
                  v-if="ageLabel(row) !== null"
                  class="user-meta__age"
                  :title="formatDate(row.birthDate)"
                  >{{ ageLabel(row) }}</span
                >
              </span>
            </template>
          </TablePrimaryCell>
        </template>
      </el-table-column>
      <el-table-column
        v-if="showsField('email')"
        prop="email"
        :label="$t('common.email')"
        sortable="custom"
        min-width="150"
        show-overflow-tooltip
      >
        <template #header>
          <ColumnSearch
            v-model="table.columnFilter('email').value"
            data-testid="users-filter-email"
            :label="$t('common.email')"
            :placeholder="$t('pages.admin.users.search.email')"
            @apply="table.onFilter"
          />
        </template>
        <template #default="{ row }">{{ row.email || '—' }}</template>
      </el-table-column>
      <el-table-column
        v-if="showsField('phone')"
        prop="phone"
        :label="$t('common.phone')"
        sortable="custom"
        width="128"
      >
        <template #header>
          <ColumnSearch
            v-model="table.columnFilter('phone').value"
            data-testid="users-filter-phone"
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
      <el-table-column
        v-if="showsField('nationalId')"
        prop="nationalId"
        :label="$t('pages.admin.users.columns.nationalId')"
        sortable="custom"
        width="120"
      >
        <template #header>
          <ColumnSearch
            v-model="table.columnFilter('nationalId').value"
            data-testid="users-filter-national-id"
            :label="$t('pages.admin.users.columns.nationalId')"
            :placeholder="$t('pages.admin.users.search.nationalId')"
            @apply="table.onFilter"
          />
        </template>
        <template #default="{ row }">{{ row.nationalId || '—' }}</template>
      </el-table-column>
      <el-table-column
        v-if="showsField('status')"
        prop="status"
        :label="$t('common.status')"
        sortable="custom"
        width="124"
      >
        <template #header>
          <ColumnFilterSelect
            v-model="table.columnFilter('status').value"
            data-testid="users-filter-status"
            :label="$t('common.status')"
            :options="statusOptions"
            @apply="table.onFilter"
          />
        </template>
        <template #default="{ row }">
          <ColorTag
            v-if="row.status.name"
            variant="dot"
            :value="row.status.name"
            :color="row.status.color"
          />
          <span v-else>—</span>
        </template>
      </el-table-column>
      <el-table-column
        prop="type"
        :label="$t('pages.admin.users.columns.type')"
        sortable="custom"
        width="132"
      >
        <template #header>
          <ColumnFilterSelect
            v-model="table.columnFilter('type').value"
            data-testid="users-filter-type"
            :label="$t('pages.admin.users.columns.type')"
            :options="typeOptions"
            @apply="table.onFilter"
          />
        </template>
        <template #default="{ row }">
          <ColorTag v-if="row.type" variant="dot" :value="row.type.name" :color="row.type.color" />
          <span v-else>—</span>
        </template>
      </el-table-column>
      <el-table-column
        v-if="showsField('staffRole')"
        prop="staffRole"
        :label="$t('pages.admin.users.columns.staffRole')"
        sortable="custom"
        width="170"
      >
        <template #header>
          <ColumnFilterSelect
            v-model="table.columnFilter('staffRole').value"
            data-testid="users-filter-staff-role"
            :label="$t('pages.admin.users.columns.staffRole')"
            :options="staffRoleOptions"
            @apply="table.onFilter"
          />
        </template>
        <template #default="{ row }">
          <div class="staff-cell">
            <span data-testid="users-staff-role">{{ staffRoleLabel(row) }}</span>
            <el-tag
              v-if="row.isPrivacyOfficer"
              size="small"
              type="success"
              disable-transitions
              :title="$t('pages.admin.users.badges.privacyOfficerTitle')"
              data-testid="users-privacy-officer"
              >{{ $t('pages.admin.users.badges.privacyOfficer') }}</el-tag
            >
          </div>
        </template>
      </el-table-column>
      <el-table-column :label="$t('common.actions')" width="100" align="right" fixed="right">
        <template #default="{ row }">
          <RowActions
            :actions="userActions(row)"
            :menu-label="$t('table.moreActionsFor', { name: fullName(row) })"
          />
        </template>
      </el-table-column>
    </DataTable>

    <StaffRoleDialog
      v-model:visible="staffRoleVisible"
      :user="staffRoleUser"
      :saving="settingStaffRole"
      :error="staffRoleError"
      @submit="saveStaffRole"
    />

    <AdminPasswordDialog
      v-model:visible="officerVisible"
      data-testid="privacy-officer-dialog"
      :title="$t('pages.admin.users.privacyOfficer.header')"
      :message="
        $t('pages.admin.users.privacyOfficer.message', { fullName: fullName(officerUser ?? {}) })
      "
      :confirm-label="$t('pages.admin.users.privacyOfficer.confirm')"
      input-id="privacy-officer-password"
      width="min(460px, 92vw)"
      :saving="designatingOfficer"
      :error="officerError"
      @submit="designatePrivacyOfficer"
    />

    <AdminPasswordDialog
      v-model:visible="resetVisible"
      data-testid="reset-two-factor-dialog"
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
.user-meta {
  display: flex;
  align-items: baseline;
  gap: 4px;
  min-width: 0;
  max-width: 100%;
}

.user-meta > * {
  flex: none;
}

.user-meta__link {
  flex: 0 1 auto;
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  padding: 0;
  border: 0;
  border-radius: 3px;
  background: none;
  color: var(--ca-orange-ink);
  font: inherit;
  cursor: pointer;
}

.user-meta__link:hover {
  text-decoration: underline;
  text-underline-offset: 2px;
}

.user-meta__link:focus-visible {
  outline: 2px solid transparent;
  background-color: var(--ca-orange-soft);
  text-decoration: underline;
  text-decoration-thickness: 2px;
  text-underline-offset: 2px;
}

.phone-cell {
  display: flex;
  flex-direction: column;
  gap: 2px;
  min-width: 0;
}

.phone-cell > span {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.staff-cell {
  display: flex;
  flex-direction: column;
  align-items: flex-start;
  gap: 4px;
  min-width: 0;
}

.phone-cell__secondary {
  font-size: 12.5px;
  line-height: 18px;
  color: var(--ca-text-muted);
}
</style>
