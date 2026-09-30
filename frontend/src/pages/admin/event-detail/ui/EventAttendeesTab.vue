<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'

import { fullName, GENDERS, genderLabelKey } from '@/entities/user'
import { SendEmailDialog, useSendEmail, useSendEmailDialog } from '@/features/send-email'
import { normalizeHexColor } from '@/shared/lib/color'
import { ageFrom, formatDateTime, todayIso } from '@/shared/lib/date'
import { type CsvValue, useCsvExport } from '@/shared/lib/download'
import { useCrudFeedback } from '@/shared/lib/feedback'
import { ActionButton } from '@/shared/ui/action-button'
import { AppIcon } from '@/shared/ui/app-icon'
import { ColorTag } from '@/shared/ui/color-tag'
import { toSelectOptions } from '@/shared/ui/column-filter'
import { DataState } from '@/shared/ui/data-state'

import type { EventAttendee } from '../model/types'
import { useAssignmentChanges } from '../model/use-assignment-changes'
import { useEventAttendees } from '../model/use-event-attendees'
import AssignmentChangeDialog from './AssignmentChangeDialog.vue'

const props = defineProps<{
  /** Event whose attendees and signups are listed and managed. */
  eventId: string
  /** Whether this tab is selected; the attendees list only loads while it is. */
  active: boolean
}>()

const { t } = useI18n()
const feedback = useCrudFeedback()
const {
  table,
  searchText,
  userTypeFilter,
  genderFilter,
  activityFilter,
  roleFilter,
  statusFilter,
  filters,
  hasActiveFilters,
  sort,
  ascending,
  toggleSortDirection,
  activities,
  roles,
  statuses,
  userTypes,
  statusColor,
} = useEventAttendees(
  () => props.eventId,
  () => props.active,
)
const {
  roleDialog,
  statusDialog,
  changingRole,
  changingStatus,
  openRole,
  openStatus,
  applyRole,
  applyStatus,
} = useAssignmentChanges(() => props.eventId)
const { sendToEventAttendees, usersAudience, eventAttendeesAudience } = useSendEmail()

const sortOptions: { label: string; value: string }[] = [
  { label: t('common.name'), value: 'firstName' },
  { label: t('common.lastName'), value: 'lastName' },
  { label: t('common.birthDate'), value: 'birthDate' },
  { label: t('pages.admin.eventDetail.attendees.type'), value: 'type,firstName' },
]

const activityOptions = computed(() =>
  (activities.data.value ?? []).map((activity) => ({ label: activity.title, value: activity.id })),
)
const typeOptions = computed(() => toSelectOptions(userTypes.data.value))
const roleOptions = computed(() => toSelectOptions(roles.data.value))
const statusOptions = computed(() => toSelectOptions(statuses.data.value))

const exportHeaders = [
  t('pages.admin.eventDetail.attendees.export.columns.firstName'),
  t('pages.admin.eventDetail.attendees.export.columns.lastName'),
  t('pages.admin.eventDetail.attendees.export.columns.email'),
  t('pages.admin.eventDetail.attendees.export.columns.phone'),
  t('pages.admin.eventDetail.attendees.export.columns.secondaryPhone'),
  t('pages.admin.eventDetail.attendees.export.columns.gender'),
  t('pages.admin.eventDetail.attendees.export.columns.guardianFirstName'),
  t('pages.admin.eventDetail.attendees.export.columns.guardianLastName'),
  t('pages.admin.eventDetail.attendees.export.columns.guardianEmail'),
  t('pages.admin.eventDetail.attendees.export.columns.guardianPhone'),
  t('pages.admin.eventDetail.attendees.export.columns.guardianSecondaryPhone'),
]

function exportRow(attendee: EventAttendee): CsvValue[] {
  return [
    attendee.firstName,
    attendee.lastName,
    attendee.email,
    attendee.phone,
    attendee.secondaryPhone,
    t(genderLabelKey(attendee.gender)),
    attendee.guardian?.firstName,
    attendee.guardian?.lastName,
    attendee.guardian?.email,
    attendee.guardian?.phone,
    attendee.guardian?.secondaryPhone,
  ]
}

const { exporting, exportCsv } = useCsvExport<EventAttendee>({
  fetchRows: table.fetchAll,
  headers: exportHeaders,
  toRow: exportRow,
  filename: () => t('pages.admin.eventDetail.attendees.export.filename', { date: todayIso() }),
  onExported: (rows) =>
    feedback.success(t('pages.admin.eventDetail.attendees.export.toast.exported', rows.length)),
  onError: (error) => feedback.error(error),
})

const {
  visible: emailDialogVisible,
  target: emailTarget,
  sending: emailSending,
  withoutConsent: emailWithoutConsent,
  open: openEmail,
  submit: submitEmail,
} = useSendEmailDialog<EventAttendee>({
  idOf: (attendee) => attendee.userId,
  targetOne: (attendee) =>
    t('pages.admin.eventDetail.attendees.email.targetOne', { fullName: fullName(attendee) }),
  targetAll: () => t('pages.admin.eventDetail.attendees.email.targetFiltered', table.total.value),
  bulkPending: () => sendToEventAttendees.isPending.value,
  sendAll: (payload, handlers) =>
    sendToEventAttendees.mutate({ eventId: props.eventId, params: filters(), payload }, handlers),
  fetchAudience: (attendee) =>
    attendee
      ? usersAudience({ id: attendee.userId })
      : eventAttendeesAudience(props.eventId, filters()),
  onError: (error) => feedback.error(error),
})

function attendeeVars(attendee: EventAttendee): Record<string, string> | undefined {
  const color = normalizeHexColor(attendee.userTypeColor)
  return color ? { '--user-type': color } : undefined
}

function hasConflicts(attendee: EventAttendee): boolean {
  return attendee.assignments.some((assignment) => assignment.hasTimeConflict)
}
</script>

<template>
  <div>
    <div class="toolbar">
      <el-input
        v-model="searchText"
        :placeholder="$t('pages.admin.eventDetail.attendees.search.placeholder')"
        class="toolbar__search"
      />
      <el-select
        v-model="userTypeFilter"
        :placeholder="$t('pages.admin.eventDetail.attendees.type')"
        clearable
        class="toolbar__filter"
      >
        <el-option
          v-for="option in typeOptions"
          :key="option.value"
          :label="option.label"
          :value="option.value"
        />
      </el-select>
      <el-select
        v-model="genderFilter"
        :placeholder="$t('common.gender')"
        clearable
        class="toolbar__filter"
      >
        <el-option
          v-for="gender in GENDERS"
          :key="gender"
          :label="$t(genderLabelKey(gender))"
          :value="gender"
        />
      </el-select>
      <el-select
        v-model="activityFilter"
        :placeholder="$t('pages.admin.eventDetail.attendees.filters.activity')"
        clearable
        class="toolbar__filter"
      >
        <el-option
          v-for="option in activityOptions"
          :key="option.value"
          :label="option.label"
          :value="option.value"
        />
      </el-select>
      <el-select
        v-model="roleFilter"
        :placeholder="$t('pages.admin.eventDetail.attendees.role')"
        clearable
        class="toolbar__filter"
      >
        <el-option
          v-for="option in roleOptions"
          :key="option.value"
          :label="option.label"
          :value="option.value"
        />
      </el-select>
      <el-select
        v-model="statusFilter"
        :placeholder="$t('common.status')"
        clearable
        class="toolbar__filter"
      >
        <el-option
          v-for="option in statusOptions"
          :key="option.value"
          :label="option.label"
          :value="option.value"
        />
      </el-select>
      <ActionButton
        :label="$t('pages.admin.eventDetail.attendees.export.label')"
        :tooltip="$t('pages.admin.eventDetail.attendees.export.tooltip')"
        icon="download"
        :loading="exporting"
        :disabled="table.total.value === 0"
        @click="exportCsv"
      />
      <ActionButton
        :label="$t('pages.admin.eventDetail.attendees.email.bulkLabel')"
        :tooltip="$t('pages.admin.eventDetail.attendees.email.bulkTooltip')"
        icon="envelope"
        class="ca-action-icon--email"
        :disabled="table.total.value === 0"
        @click="openEmail(null)"
      />
      <div class="toolbar__sort">
        <el-select
          v-model="sort"
          :aria-label="$t('pages.admin.eventDetail.attendees.sort.ariaSortBy')"
          class="toolbar__sort-select"
        >
          <el-option
            v-for="option in sortOptions"
            :key="option.value"
            :label="option.label"
            :value="option.value"
          />
        </el-select>
        <ActionButton
          :icon="ascending ? 'sort-amount-up-alt' : 'sort-amount-down'"
          text
          circle
          :aria-label="
            ascending
              ? $t('pages.admin.eventDetail.attendees.sort.ascending')
              : $t('pages.admin.eventDetail.attendees.sort.descending')
          "
          @click="toggleSortDirection"
        />
      </div>
    </div>

    <DataState
      :loading="
        (table.loading.value && table.items.value.length === 0) || activities.isLoading.value
      "
      :error="table.isError.value || activities.isError.value"
      :empty="table.total.value === 0 && !table.loading.value"
      :empty-text="
        hasActiveFilters
          ? $t('pages.admin.eventDetail.attendees.empty.noMatches')
          : $t('pages.admin.eventDetail.attendees.empty.none')
      "
    >
      <p class="count">
        {{ $t('pages.admin.eventDetail.attendees.count', table.total.value) }}
      </p>

      <ul class="attendees">
        <li
          v-for="attendee in table.items.value"
          :key="attendee.userId"
          class="attendee"
          :style="attendeeVars(attendee)"
        >
          <div class="attendee__head">
            <div class="attendee__identity">
              <span class="attendee__name">{{ fullName(attendee) }}</span>
              <span v-if="ageFrom(attendee.birthDate) !== null" class="attendee__age">
                {{
                  $t('pages.admin.eventDetail.attendees.age', { age: ageFrom(attendee.birthDate) })
                }}
              </span>
              <span
                class="attendee__type"
                :title="
                  $t('pages.admin.eventDetail.attendees.typeTitle', {
                    name: attendee.userTypeName,
                  })
                "
              >
                {{ attendee.userTypeName }}
              </span>
              <span
                v-if="hasConflicts(attendee)"
                class="attendee__conflict"
                :title="$t('pages.admin.eventDetail.attendees.conflict.attendeeTitle')"
              >
                <AppIcon name="exclamation-triangle" />
                {{ $t('pages.admin.eventDetail.attendees.conflict.badge') }}
              </span>
            </div>
            <div class="attendee__contact">
              <template v-if="attendee.guardian">
                <span>
                  <AppIcon name="user" />
                  {{
                    $t('pages.admin.eventDetail.attendees.guardian', {
                      firstName: attendee.guardian.firstName,
                      lastName: attendee.guardian.lastName,
                    })
                  }}
                </span>
                <span>
                  <AppIcon name="envelope" />
                  {{ attendee.guardian.email || '—' }}
                </span>
                <span>
                  <AppIcon name="phone" />
                  {{ attendee.guardian.phone || '—' }}
                </span>
                <span v-if="attendee.guardian.secondaryPhone">
                  <AppIcon name="phone" />
                  {{ attendee.guardian.secondaryPhone }}
                </span>
              </template>
              <template v-else>
                <span><AppIcon name="envelope" /> {{ attendee.email || '—' }}</span>
                <span><AppIcon name="phone" /> {{ attendee.phone || '—' }}</span>
                <span v-if="attendee.secondaryPhone">
                  <AppIcon name="phone" /> {{ attendee.secondaryPhone }}
                </span>
              </template>
              <ActionButton
                v-if="attendee.email"
                icon="envelope"
                text
                circle
                size="small"
                class="ca-action-icon--email"
                :aria-label="$t('pages.admin.eventDetail.attendees.email.rowLabel')"
                @click="openEmail(attendee)"
              />
            </div>
          </div>

          <ul class="attendee__assignments">
            <li
              v-for="assignment in attendee.assignments"
              :key="assignment.activityId"
              class="assignment"
            >
              <span class="assignment__title">{{ assignment.activityTitle }}</span>
              <span class="assignment__role">{{ assignment.roleName || '—' }}</span>
              <span
                class="assignment__signed"
                :title="$t('pages.admin.eventDetail.attendees.signedUpTitle')"
              >
                <AppIcon name="calendar-plus" />
                {{ formatDateTime(assignment.signedUpAt) }}
              </span>
              <span class="assignment__status">
                <ColorTag :value="assignment.statusName || '—'" :color="statusColor(assignment)" />
                <span
                  v-if="assignment.hasTimeConflict"
                  class="assignment__warning"
                  :title="$t('pages.admin.eventDetail.attendees.conflict.assignmentTitle')"
                >
                  <AppIcon name="exclamation-triangle" />
                </span>
              </span>
              <div class="assignment__actions">
                <ActionButton
                  icon="tag"
                  text
                  circle
                  size="small"
                  class="ca-action-icon--assignment"
                  :aria-label="$t('pages.admin.eventDetail.attendees.changeRole')"
                  @click="openRole(attendee, assignment)"
                />
                <ActionButton
                  icon="sync"
                  text
                  circle
                  size="small"
                  class="ca-action-icon--assignment"
                  :aria-label="$t('pages.admin.eventDetail.attendees.changeStatus')"
                  @click="openStatus(attendee, assignment)"
                />
              </div>
            </li>
          </ul>
        </li>
      </ul>

      <el-pagination
        v-if="table.total.value > 25 || table.first.value > 0"
        v-bind="table.paginationProps.value"
        class="paginator"
        @update:current-page="table.onCurrentPageChange"
        @update:page-size="table.onPageSizeChange"
      />
    </DataState>

    <SendEmailDialog
      v-model:visible="emailDialogVisible"
      :target="emailTarget"
      :sending="emailSending"
      :without-consent="emailWithoutConsent"
      @submit="submitEmail"
    />

    <AssignmentChangeDialog
      v-model:visible="roleDialog.visible"
      v-model:selected="roleDialog.selectedId"
      :title="$t('pages.admin.eventDetail.attendees.changeRole')"
      :attendee-name="roleDialog.target ? fullName(roleDialog.target.attendee) : ''"
      :activity-title="roleDialog.target?.assignment.activityTitle ?? ''"
      :label="$t('pages.admin.eventDetail.attendees.role')"
      input-id="attendee-role"
      :placeholder="$t('pages.admin.eventDetail.attendees.selectRole')"
      :options="roleOptions"
      :unavailable-text="$t('pages.admin.eventDetail.attendees.rolesLoadError')"
      :applying="changingRole"
      @apply="applyRole"
    />

    <AssignmentChangeDialog
      v-model:visible="statusDialog.visible"
      v-model:selected="statusDialog.selectedId"
      :title="$t('pages.admin.eventDetail.attendees.changeStatus')"
      :attendee-name="statusDialog.target ? fullName(statusDialog.target.attendee) : ''"
      :activity-title="statusDialog.target?.assignment.activityTitle ?? ''"
      :label="$t('common.status')"
      input-id="attendee-status"
      :placeholder="$t('pages.admin.eventDetail.attendees.selectStatus')"
      :options="statusOptions"
      :applying="changingStatus"
      @apply="applyStatus"
    />
  </div>
</template>

<style scoped>
.toolbar {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 10px;
  margin-bottom: 16px;
}

.toolbar__search {
  flex: 1 1 260px;
  min-width: 220px;
}

.toolbar__filter {
  flex: 0 1 160px;
  min-width: 160px;
}

.toolbar__sort {
  display: flex;
  align-items: center;
  gap: 4px;
  margin-left: auto;
}

.toolbar__sort-select {
  width: 180px;
}

.count {
  font-size: 13px;
  color: var(--ca-text-muted);
  margin-bottom: 12px;
}

.attendees {
  list-style: none;
  margin: 0;
  padding: 0;
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.paginator {
  margin-top: 16px;
  justify-content: flex-end;
}

.attendee {
  background: var(--ca-surface);
  border: 1px solid var(--ca-border-soft);
  border-left: 3px solid var(--user-type, var(--ca-border-strong));
  border-radius: 12px;
  padding: 12px 16px;
}

.attendee__conflict {
  font-size: 12.5px;
  font-weight: 600;
  color: var(--ca-warning-ink);
  background: var(--ca-warning-soft);
  border-radius: 6px;
  padding: 2px 8px;
}

.attendee__conflict i {
  font-size: 11px;
  margin-right: 3px;
}

.attendee__head {
  display: flex;
  align-items: baseline;
  justify-content: space-between;
  gap: 16px;
  flex-wrap: wrap;
}

.attendee__identity {
  display: flex;
  align-items: baseline;
  gap: 10px;
  row-gap: 6px;
  flex-wrap: wrap;
}

.attendee__type {
  display: inline-block;
  font-size: 12.5px;
  font-weight: 600;
  color: var(--ca-text);
  background: color-mix(in srgb, var(--user-type, var(--ca-border-strong)) 16%, var(--ca-surface));
  border-radius: 6px;
  padding: 2px 8px 2px 6px;
  white-space: nowrap;
}

.attendee__type::before {
  content: '';
  display: inline-block;
  width: 8px;
  height: 8px;
  margin-right: 5px;
  border-radius: 50%;
  background: var(--user-type, var(--ca-border-strong));
  vertical-align: middle;
}

.attendee__name {
  font-weight: 600;
  font-size: 15.5px;
  color: var(--ca-text-bright);
}

.attendee__age {
  font-size: 13px;
  color: var(--ca-text-muted);
}

.attendee__contact {
  display: flex;
  align-items: center;
  gap: 18px;
  flex-wrap: wrap;
  font-size: 13.5px;
  color: var(--ca-text-muted);
  overflow-wrap: anywhere;
  min-width: 0;
}

.attendee__contact i {
  font-size: 12px;
  margin-right: 4px;
}

.attendee__assignments {
  list-style: none;
  margin: 8px 0 0;
  padding: 0;
  display: flex;
  flex-direction: column;
}

.assignment {
  display: grid;
  grid-template-columns: minmax(150px, 2fr) minmax(100px, 1fr) minmax(150px, auto) auto auto;
  align-items: center;
  gap: 10px;
  padding: 1px 0 1px 12px;
  border-top: 1px solid var(--ca-border-soft);
}

.assignment__title {
  color: var(--ca-text);
  font-size: 14px;
}

.assignment__role {
  font-size: 13px;
  color: var(--ca-text-muted);
}

.assignment__signed {
  font-size: 12.5px;
  color: var(--ca-text-dim);
  white-space: nowrap;
}

.assignment__signed i {
  font-size: 11px;
  margin-right: 3px;
}

.assignment__status {
  display: flex;
  align-items: center;
  gap: 6px;
}

.assignment__warning {
  color: var(--ca-warning-ink);
  font-size: 14px;
  line-height: 1;
}

.assignment__actions {
  display: flex;
  gap: 2px;
  justify-self: end;
}

@media (max-width: 768px) {
  .assignment {
    grid-template-columns: 1fr auto;
    grid-template-rows: auto auto auto;
  }

  .assignment__role,
  .assignment__signed,
  .assignment__status {
    grid-column: 1;
  }

  .assignment__actions {
    grid-row: 1 / span 3;
    grid-column: 2;
  }
}
</style>
