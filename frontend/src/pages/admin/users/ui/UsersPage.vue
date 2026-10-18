<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'

import { fullName, type User } from '@/entities/user'
import { SendEmailDialog, useSendEmail, useSendEmailDialog } from '@/features/send-email'
import { ageFrom, formatDate } from '@/shared/lib/date'
import { useCrudFeedback } from '@/shared/lib/feedback'
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
import UserTypeDialog from './UserTypeDialog.vue'

const { t } = useI18n()
const feedback = useCrudFeedback()
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
  adminBlockReason,
  deleteBlockReason,
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

const typeVisible = typeDialog.visible
const typedUser = typeDialog.editing
const grantVisible = grantDialog.visible
const grantedUser = grantDialog.editing
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
  fetchAudience: (user) => usersAudience(user ? { id: user.id } : table.filterParams.value),
  onError: (error) => feedback.error(error),
})

function blockedBy(
  reason: string | undefined,
  busy = false,
): Pick<RowAction, 'disabled' | 'reason'> {
  return reason === undefined ? { disabled: busy } : { disabled: true, reason }
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
    actions.push(
      {
        key: 'sendEmail',
        label: t('pages.admin.users.actions.sendEmail'),
        icon: 'envelope',
        run: () => {
          openEmail(user)
        },
      },
      {
        key: 'resetTwoFactor',
        label: t('pages.admin.users.actions.resetTwoFactor'),
        icon: 'lock',
        run: () => {
          openResetTwoFactor(user)
        },
      },
    )
  }
  if (!user.parentId) {
    actions.push({
      key: 'admin',
      label: user.isAdmin
        ? t('pages.admin.users.actions.revokeAdmin')
        : t('pages.admin.users.actions.grantAdmin'),
      icon: 'key',
      ...blockedBy(adminBlockReason(user), settingAdmin.value),
      run: () => {
        toggleAdmin(user, !user.isAdmin)
      },
    })
  }
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
  return actions
}
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
          :aria-label="$t('pages.admin.users.scope.label')"
          :options="scopeOptions"
          :model-value="scope"
          @change="setScope"
        />
        <ColumnFilterSelect
          v-if="showsField('isAdmin')"
          v-model="table.columnFilter('isAdmin').value"
          variant="toolbar"
          :label="$t('pages.admin.users.columns.admin')"
          :options="yesNoOptions"
          @apply="table.onFilter"
        />
        <ColumnFilterSelect
          v-if="showsField('promotionalConsent')"
          v-model="table.columnFilter('promotionalConsent').value"
          variant="toolbar"
          :label="$t('pages.admin.users.columns.promotionalConsent')"
          :options="yesNoOptions"
          @apply="table.onFilter"
        />
        <ColumnFilterDate
          v-if="showsField('birthDate')"
          v-model="table.columnFilter('birthDate').value"
          variant="toolbar"
          :label="$t('pages.admin.users.columns.birth')"
          @apply="table.onFilter"
        />
      </template>
      <template #actions>
        <ActionButton
          :label="$t('pages.admin.users.email.bulkLabel')"
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
            <template v-if="row.isAdmin || isOwnAccount(row)" #badges>
              <el-tag v-if="row.isAdmin" size="small" disable-transitions>{{
                $t('pages.admin.users.columns.admin')
              }}</el-tag>
              <el-tag v-if="isOwnAccount(row)" size="small" type="info" disable-transitions>{{
                $t('pages.admin.users.badges.you')
              }}</el-tag>
            </template>
            <template v-if="ageLabel(row) !== null || hasFamily(row)" #meta>
              <span class="user-meta">
                <span v-if="row.parentId">{{ $t('pages.admin.users.guardianPrefix') }}</span>
                <button
                  v-if="row.parentId"
                  type="button"
                  class="user-meta__link"
                  :title="$t('pages.admin.users.tooltips.showTutor')"
                  @click="showGuardianOf(row)"
                >
                  {{ row.parentName }}
                </button>
                <button
                  v-else-if="row.dependentCount > 0"
                  type="button"
                  class="user-meta__link"
                  :title="$t('pages.admin.users.tooltips.showDependents')"
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
      <el-table-column :label="$t('common.actions')" width="100" align="right" fixed="right">
        <template #default="{ row }">
          <RowActions
            :actions="userActions(row)"
            :menu-label="$t('table.moreActionsFor', { name: fullName(row) })"
          />
        </template>
      </el-table-column>
    </DataTable>

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

.phone-cell__secondary {
  font-size: 12.5px;
  line-height: 18px;
  color: var(--ca-text-muted);
}
</style>
