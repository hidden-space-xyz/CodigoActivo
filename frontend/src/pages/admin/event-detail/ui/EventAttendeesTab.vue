<script setup lang="ts">
import { computed, onBeforeUnmount, ref, watch, type Ref } from 'vue'
import { useI18n } from 'vue-i18n'

import { useAssignments } from '@/features/manage-activities'
import { useEventAttendeesTable } from '@/features/manage-events'
import { SendEmailDialog, useSendEmail, useSendEmailDialog } from '@/features/send-email'
import {
  useActivityRoleTypesList,
  useAssignmentStatusTypesList,
  useUserTypesList,
} from '@/entities/catalog'
import { genderLabel, genderOptions } from '@/entities/user'
import type {
  ActivityResponse,
  EventAttendeeAssignmentResponse,
  EventAttendeeResponse,
  PostApiEmailsEventsEventIdAttendeesParams,
} from '@/shared/api/generated/models'
import { AppButton as Button, AppIcon, ColorTag, DataState } from '@/shared/ui'
import type { CsvValue } from '@/shared/lib'
import {
  ageFrom,
  formatDateTime,
  fullName,
  normalizeHexColor,
  toSelectOptions,
  todayIso,
  useCrudFeedback,
  useCsvExport,
} from '@/shared/lib'

const props = defineProps<{
  eventId: string
  active: boolean
  activities: ActivityResponse[]
  activitiesLoading: boolean
  activitiesError: boolean
}>()

const { t } = useI18n()
const feedback = useCrudFeedback()
const attendees = useEventAttendeesTable(
  () => props.eventId,
  () => props.active,
)
const assignments = useAssignments(() => props.eventId)
const { sendToEventAttendees } = useSendEmail()
const statusTypes = useAssignmentStatusTypesList()
const roleTypes = useActivityRoleTypesList()
const userTypes = useUserTypesList()

const searchText = ref('')
let searchTimer: ReturnType<typeof setTimeout> | undefined

watch(searchText, (value) => {
  if (searchTimer) clearTimeout(searchTimer)
  searchTimer = setTimeout(() => {
    attendees.search.value = value
  }, 300)
})

onBeforeUnmount(() => {
  if (searchTimer) clearTimeout(searchTimer)
})

const TYPE_SORT = 'type,firstName'

const sortOptions: { label: string; value: string }[] = [
  { label: t('common.name'), value: 'firstName' },
  { label: t('common.lastName'), value: 'lastName' },
  { label: t('common.birthDate'), value: 'birthDate' },
  { label: t('pages.admin.eventDetail.attendees.type'), value: TYPE_SORT },
]

const sortField = computed({
  get: () => attendees.table.sortField.value ?? 'firstName',
  set: (value: string) => {
    attendees.table.sortField.value = value
    attendees.table.first.value = 0
  },
})

const sortAsc = computed(() => attendees.table.sortOrder.value !== -1)

function toggleSortDirection(): void {
  attendees.table.sortOrder.value = sortAsc.value ? -1 : 1
  attendees.table.first.value = 0
}

const activityOptions = computed(() =>
  props.activities.map((activity) => ({
    label: activity.title ?? '—',
    value: activity.id ?? '',
  })),
)

const typeOptions = computed(() => toSelectOptions(userTypes.data.value))

const roleOptions = computed(() => toSelectOptions(roleTypes.data.value))

const statusOptions = computed(() => toSelectOptions(statusTypes.data.value))

const genders = genderOptions()

function filterModel<T extends string>(source: Ref<T | null>) {
  return computed<T | null>({
    get: () => source.value,
    set: (value) => {
      source.value = value == null || (value as string) === '' ? null : value
    },
  })
}

const userTypeFilter = filterModel(attendees.userTypeId)
const genderFilter = filterModel(attendees.gender)
const activityFilter = filterModel(attendees.activityId)
const roleFilter = filterModel(attendees.roleTypeId)
const statusFilter = filterModel(attendees.statusId)

const hasActiveFilters = computed(
  () =>
    searchText.value.trim() !== '' ||
    attendees.userTypeId.value !== null ||
    attendees.gender.value !== null ||
    attendees.activityId.value !== null ||
    attendees.roleTypeId.value !== null ||
    attendees.statusId.value !== null,
)

const exportHeaders = [
  t('pages.admin.eventDetail.attendees.export.columns.firstName'),
  t('pages.admin.eventDetail.attendees.export.columns.lastName'),
  t('pages.admin.eventDetail.attendees.export.columns.email'),
  t('pages.admin.eventDetail.attendees.export.columns.phone'),
  t('pages.admin.eventDetail.attendees.export.columns.gender'),
  t('pages.admin.eventDetail.attendees.export.columns.guardianFirstName'),
  t('pages.admin.eventDetail.attendees.export.columns.guardianLastName'),
  t('pages.admin.eventDetail.attendees.export.columns.guardianEmail'),
  t('pages.admin.eventDetail.attendees.export.columns.guardianPhone'),
]

function exportRow(attendee: EventAttendeeResponse): CsvValue[] {
  return [
    attendee.firstName,
    attendee.lastName,
    attendee.email,
    attendee.phone,
    attendee.gender ? genderLabel(attendee.gender) : null,
    attendee.guardian?.firstName,
    attendee.guardian?.lastName,
    attendee.guardian?.email,
    attendee.guardian?.phone,
  ]
}

const { exporting, exportCsv } = useCsvExport<EventAttendeeResponse>({
  fetchRows: attendees.fetchAllAttendees,
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
  open: openEmail,
  submit: submitEmail,
} = useSendEmailDialog<EventAttendeeResponse>({
  idOf: (attendee) => attendee.userId,
  targetOne: (attendee) =>
    t('pages.admin.eventDetail.attendees.email.targetOne', { fullName: fullName(attendee) }),
  targetAll: () =>
    t('pages.admin.eventDetail.attendees.email.targetFiltered', attendees.table.total.value),
  bulkPending: () => sendToEventAttendees.isPending.value,
  sendAll: (payload, handlers) =>
    sendToEventAttendees.mutate(
      {
        eventId: props.eventId,
        params: attendees.filterParams() as PostApiEmailsEventsEventIdAttendeesParams,
        payload,
      },
      handlers,
    ),
  onError: (error) => feedback.error(error),
})

const statusColorById = computed(() => {
  const map = new Map<string, string>()
  for (const status of statusTypes.data.value ?? []) {
    if (status.id) map.set(status.id, status.color ?? '')
  }
  return map
})

function statusColor(assignment: EventAttendeeAssignmentResponse): string | null {
  return assignment.statusId ? (statusColorById.value.get(assignment.statusId) ?? null) : null
}

function typeName(attendee: EventAttendeeResponse): string {
  return attendee.userTypeName || '—'
}

function attendeeVars(attendee: EventAttendeeResponse): Record<string, string> | undefined {
  const color = normalizeHexColor(attendee.userTypeColor)
  return color ? { '--user-type': color } : undefined
}

function hasConflicts(attendee: EventAttendeeResponse): boolean {
  return (attendee.assignments ?? []).some((assignment) => assignment.hasTimeConflict)
}

type DialogTarget = {
  attendee: EventAttendeeResponse
  assignment: EventAttendeeAssignmentResponse
}

const statusDialogVisible = ref(false)
const statusTarget = ref<DialogTarget | null>(null)
const selectedStatusId = ref<string | null>(null)

function openChangeStatus(
  attendee: EventAttendeeResponse,
  assignment: EventAttendeeAssignmentResponse,
): void {
  statusTarget.value = { attendee, assignment }
  selectedStatusId.value = assignment.statusId ?? null
  statusDialogVisible.value = true
}

function submitChangeStatus(): void {
  const target = statusTarget.value
  if (!target?.attendee.userId || !target.assignment.activityId || !selectedStatusId.value) return
  assignments.changeStatus.mutate(
    {
      activityId: target.assignment.activityId,
      userId: target.attendee.userId,
      body: { assignmentStatusId: selectedStatusId.value },
    },
    {
      onSuccess: () => {
        feedback.success(t('pages.admin.eventDetail.attendees.toast.statusUpdated'))
        statusDialogVisible.value = false
      },
      onError: (error) => feedback.error(error),
    },
  )
}

const roleDialogVisible = ref(false)
const roleTarget = ref<DialogTarget | null>(null)
const selectedRoleId = ref<string | null>(null)

function openChangeRole(
  attendee: EventAttendeeResponse,
  assignment: EventAttendeeAssignmentResponse,
): void {
  roleTarget.value = { attendee, assignment }
  selectedRoleId.value = assignment.roleTypeId ?? null
  roleDialogVisible.value = true
}

function submitChangeRole(): void {
  const target = roleTarget.value
  if (!target?.attendee.userId || !target.assignment.activityId || !selectedRoleId.value) return
  assignments.changeRole.mutate(
    {
      activityId: target.assignment.activityId,
      userId: target.attendee.userId,
      body: { activityRoleTypeId: selectedRoleId.value },
    },
    {
      onSuccess: () => {
        feedback.success(t('pages.admin.eventDetail.attendees.toast.roleUpdated'))
        roleDialogVisible.value = false
      },
      onError: (error) => feedback.error(error),
    },
  )
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
          v-for="option in genders"
          :key="option.value"
          :label="option.label"
          :value="option.value"
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
      <Button
        :label="$t('pages.admin.eventDetail.attendees.export.label')"
        :tooltip="$t('pages.admin.eventDetail.attendees.export.tooltip')"
        icon="download"
        :loading="exporting"
        :disabled="attendees.table.total.value === 0"
        @click="exportCsv"
      />
      <Button
        :label="$t('pages.admin.eventDetail.attendees.email.bulkLabel')"
        :tooltip="$t('pages.admin.eventDetail.attendees.email.bulkTooltip')"
        icon="envelope"
        :disabled="attendees.table.total.value === 0"
        @click="openEmail(null)"
      />
      <div class="toolbar__sort">
        <el-select
          v-model="sortField"
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
        <Button
          :icon="sortAsc ? 'sort-amount-up-alt' : 'sort-amount-down'"
          text
          circle
          :aria-label="
            sortAsc
              ? $t('pages.admin.eventDetail.attendees.sort.ascending')
              : $t('pages.admin.eventDetail.attendees.sort.descending')
          "
          @click="toggleSortDirection"
        />
      </div>
    </div>

    <DataState
      :loading="
        (attendees.table.loading.value && attendees.table.items.value.length === 0) ||
        activitiesLoading
      "
      :error="attendees.table.isError.value || activitiesError"
      :empty="attendees.table.total.value === 0 && !attendees.table.loading.value"
      :empty-text="
        hasActiveFilters
          ? $t('pages.admin.eventDetail.attendees.empty.noMatches')
          : $t('pages.admin.eventDetail.attendees.empty.none')
      "
    >
      <p class="count">
        {{ $t('pages.admin.eventDetail.attendees.count', attendees.table.total.value) }}
      </p>

      <ul class="attendees">
        <li
          v-for="attendee in attendees.table.items.value"
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
                  $t('pages.admin.eventDetail.attendees.typeTitle', { name: typeName(attendee) })
                "
              >
                {{ typeName(attendee) }}
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
              </template>
              <template v-else>
                <span><AppIcon name="envelope" /> {{ attendee.email || '—' }}</span>
                <span><AppIcon name="phone" /> {{ attendee.phone || '—' }}</span>
              </template>
              <Button
                v-if="attendee.email"
                icon="send"
                text
                circle
                size="small"
                :aria-label="$t('pages.admin.eventDetail.attendees.email.rowLabel')"
                @click="openEmail(attendee)"
              />
            </div>
          </div>

          <ul class="attendee__assignments">
            <li
              v-for="assignment in attendee.assignments ?? []"
              :key="assignment.activityId"
              class="assignment"
            >
              <span class="assignment__title">{{ assignment.activityTitle || '—' }}</span>
              <span class="assignment__role">{{ assignment.roleTypeName || '—' }}</span>
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
                <Button
                  icon="tag"
                  text
                  circle
                  size="small"
                  :aria-label="$t('pages.admin.eventDetail.attendees.changeRole')"
                  @click="openChangeRole(attendee, assignment)"
                />
                <Button
                  icon="sync"
                  text
                  circle
                  size="small"
                  :aria-label="$t('pages.admin.eventDetail.attendees.changeStatus')"
                  @click="openChangeStatus(attendee, assignment)"
                />
              </div>
            </li>
          </ul>
        </li>
      </ul>

      <el-pagination
        v-if="attendees.table.total.value > 25 || attendees.table.first.value > 0"
        v-bind="attendees.table.paginationProps.value"
        class="paginator"
        @update:current-page="attendees.table.onCurrentPageChange"
        @update:page-size="attendees.table.onPageSizeChange"
      />
    </DataState>

    <SendEmailDialog
      v-model:visible="emailDialogVisible"
      :target="emailTarget"
      :sending="emailSending"
      @submit="submitEmail"
    />

    <el-dialog
      v-model="roleDialogVisible"
      :title="$t('pages.admin.eventDetail.attendees.changeRole')"
      width="min(92vw, 400px)"
      append-to-body
    >
      <p class="dialog-context">
        {{
          $t('pages.admin.eventDetail.attendees.dialogContext', {
            name: roleTarget ? fullName(roleTarget.attendee) : '',
            activity: roleTarget?.assignment.activityTitle,
          })
        }}
      </p>
      <div class="form__field">
        <label>{{ $t('pages.admin.eventDetail.attendees.role') }}</label>
        <el-select
          v-model="selectedRoleId"
          :placeholder="$t('pages.admin.eventDetail.attendees.selectRole')"
        >
          <el-option
            v-for="option in roleOptions"
            :key="option.value"
            :label="option.label"
            :value="option.value"
          />
        </el-select>
        <small v-if="roleOptions.length === 0" class="form__warning">
          {{ $t('pages.admin.eventDetail.attendees.rolesLoadError') }}
        </small>
      </div>
      <template #footer>
        <Button
          :label="$t('common.cancel')"
          text
          :disabled="assignments.changeRole.isPending.value"
          @click="roleDialogVisible = false"
        />
        <Button
          :label="$t('common.apply')"
          type="primary"
          :loading="assignments.changeRole.isPending.value"
          :disabled="!selectedRoleId || roleOptions.length === 0"
          @click="submitChangeRole"
        />
      </template>
    </el-dialog>

    <el-dialog
      v-model="statusDialogVisible"
      :title="$t('pages.admin.eventDetail.attendees.changeStatus')"
      width="min(92vw, 400px)"
      append-to-body
    >
      <p class="dialog-context">
        {{
          $t('pages.admin.eventDetail.attendees.dialogContext', {
            name: statusTarget ? fullName(statusTarget.attendee) : '',
            activity: statusTarget?.assignment.activityTitle,
          })
        }}
      </p>
      <div class="form__field">
        <label>{{ $t('common.status') }}</label>
        <el-select
          v-model="selectedStatusId"
          :placeholder="$t('pages.admin.eventDetail.attendees.selectStatus')"
        >
          <el-option
            v-for="option in statusOptions"
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
          :disabled="assignments.changeStatus.isPending.value"
          @click="statusDialogVisible = false"
        />
        <Button
          :label="$t('common.apply')"
          type="primary"
          :loading="assignments.changeStatus.isPending.value"
          :disabled="!selectedStatusId"
          @click="submitChangeStatus"
        />
      </template>
    </el-dialog>
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

.dialog-context {
  font-size: 13.5px;
  color: var(--ca-text-muted);
  margin: 0 0 14px;
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

.form__warning {
  font-size: 12.5px;
  color: var(--ca-danger-ink);
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
