<script setup lang="ts">
import { computed, ref } from 'vue'
import Dialog from 'primevue/dialog'
import InputText from 'primevue/inputtext'
import Select from 'primevue/select'

import { useAssignments } from '@/features/manage-activities'
import { useEventAttendees } from '@/features/manage-events'
import { useAssignmentStatusTypesList } from '@/entities/catalog'
import type {
  ActivityResponse,
  EventAttendeeAssignmentResponse,
  EventAttendeeResponse,
} from '@/shared/api/generated/models'
import { AppButton as Button, ColorTag, DataState } from '@/shared/ui'
import { ageFrom, formatDateTime, useCrudFeedback } from '@/shared/lib'

const props = defineProps<{
  eventId: string
  active: boolean
  activities: ActivityResponse[]
  activitiesLoading: boolean
  activitiesError: boolean
}>()

const feedback = useCrudFeedback()
const attendees = useEventAttendees(
  () => props.eventId,
  () => props.active,
)
const assignments = useAssignments(() => props.eventId)
const statusTypes = useAssignmentStatusTypesList()

const query = ref('')
const activityFilter = ref<string | null>(null)
const roleFilter = ref<string | null>(null)
const statusFilter = ref<string | null>(null)

type SortField = 'firstName' | 'lastName'
const sortField = ref<SortField>('firstName')
const sortAsc = ref(true)

const sortOptions: { label: string; value: SortField }[] = [
  { label: 'Nombre', value: 'firstName' },
  { label: 'Apellidos', value: 'lastName' },
]

const activityOptions = computed(() =>
  props.activities.map((activity) => ({
    label: activity.title ?? '—',
    value: activity.id ?? '',
  })),
)

const roleOptions = computed(() => {
  const seen = new Map<string, string>()
  for (const attendee of attendees.data.value?.attendees ?? []) {
    for (const assignment of attendee.assignments ?? []) {
      if (assignment.roleTypeId && !seen.has(assignment.roleTypeId)) {
        seen.set(assignment.roleTypeId, assignment.roleTypeName ?? '—')
      }
    }
  }
  return Array.from(seen, ([value, label]) => ({ label, value }))
})

const statusOptions = computed(() =>
  (statusTypes.data.value ?? []).map((status) => ({
    label: status.name ?? '—',
    value: status.id ?? '',
  })),
)

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

function fullName(attendee: EventAttendeeResponse): string {
  return `${attendee.firstName ?? ''} ${attendee.lastName ?? ''}`.trim() || '—'
}

function attendeeStyle(attendee: EventAttendeeResponse): Record<string, string> | undefined {
  const color = attendee.userTypeColor
  if (!color) return undefined
  return {
    borderLeft: `3px solid ${color}`,
    background: `linear-gradient(0deg, ${color}14, ${color}14), var(--ca-surface)`,
  }
}

function hasConflicts(attendee: EventAttendeeResponse): boolean {
  return (attendee.assignments ?? []).some((assignment) => assignment.hasTimeConflict)
}

const hasAssignmentFilter = computed(
  () => activityFilter.value !== null || roleFilter.value !== null || statusFilter.value !== null,
)

function assignmentMatches(assignment: EventAttendeeAssignmentResponse): boolean {
  if (activityFilter.value && assignment.activityId !== activityFilter.value) return false
  if (roleFilter.value && assignment.roleTypeId !== roleFilter.value) return false
  if (statusFilter.value && assignment.statusId !== statusFilter.value) return false
  return true
}

function visibleAssignments(attendee: EventAttendeeResponse): EventAttendeeAssignmentResponse[] {
  const list = attendee.assignments ?? []
  return hasAssignmentFilter.value ? list.filter(assignmentMatches) : [...list]
}

function matches(attendee: EventAttendeeResponse): boolean {
  const text = query.value.trim().toLowerCase()
  if (text) {
    const haystack =
      `${attendee.firstName ?? ''} ${attendee.lastName ?? ''} ${attendee.email ?? ''} ${attendee.phone ?? ''}`.toLowerCase()
    if (!haystack.includes(text)) return false
  }
  if (hasAssignmentFilter.value && !(attendee.assignments ?? []).some(assignmentMatches)) {
    return false
  }
  return true
}

function compare(a: EventAttendeeResponse, b: EventAttendeeResponse): number {
  const field = sortField.value
  const result = (a[field] ?? '').localeCompare(b[field] ?? '', 'es', { sensitivity: 'base' })
  if (result !== 0) return result
  return fullName(a).localeCompare(fullName(b), 'es', { sensitivity: 'base' })
}

const filteredAttendees = computed(() => {
  const direction = sortAsc.value ? 1 : -1
  return (attendees.data.value?.attendees ?? [])
    .filter(matches)
    .toSorted((a, b) => direction * compare(a, b))
})

const totalCount = computed(() => attendees.data.value?.attendees?.length ?? 0)

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
        feedback.success('Estado actualizado.')
        statusDialogVisible.value = false
      },
      onError: (error) => feedback.error(error),
    },
  )
}

const roleDialogVisible = ref(false)
const roleTarget = ref<DialogTarget | null>(null)
const selectedRoleId = ref<string | null>(null)

const roleDialogOptions = computed(() => {
  const activityId = roleTarget.value?.assignment.activityId
  const activity = props.activities.find((a) => a.id === activityId)
  return (activity?.allowedRoleTypes ?? []).map((role) => ({
    label: role.roleTypeName ?? '—',
    value: role.roleTypeId ?? '',
  }))
})

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
        feedback.success('Rol actualizado.')
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
      <InputText
        v-model="query"
        placeholder="Buscar por nombre, correo o teléfono"
        class="toolbar__search"
      />
      <Select
        v-model="activityFilter"
        :options="activityOptions"
        option-label="label"
        option-value="value"
        placeholder="Actividad"
        show-clear
        class="toolbar__filter"
      />
      <Select
        v-model="roleFilter"
        :options="roleOptions"
        option-label="label"
        option-value="value"
        placeholder="Rol"
        show-clear
        class="toolbar__filter"
      />
      <Select
        v-model="statusFilter"
        :options="statusOptions"
        option-label="label"
        option-value="value"
        placeholder="Estado"
        show-clear
        class="toolbar__filter"
      />
      <div class="toolbar__sort">
        <Select
          v-model="sortField"
          :options="sortOptions"
          option-label="label"
          option-value="value"
          aria-label="Ordenar por"
        />
        <Button
          :icon="sortAsc ? 'pi pi-sort-amount-up-alt' : 'pi pi-sort-amount-down'"
          text
          rounded
          :aria-label="sortAsc ? 'Orden ascendente' : 'Orden descendente'"
          @click="sortAsc = !sortAsc"
        />
      </div>
    </div>

    <DataState
      :loading="attendees.isLoading.value || activitiesLoading"
      :error="attendees.isError.value || activitiesError"
      :empty="totalCount === 0"
      empty-text="Todavía no hay usuarios apuntados a este evento."
    >
      <p class="count">
        {{ filteredAttendees.length }} de {{ totalCount }}
        {{ totalCount === 1 ? 'asistente' : 'asistentes' }}
      </p>

      <p v-if="filteredAttendees.length === 0" class="no-match">Sin coincidencias.</p>

      <ul v-else class="attendees">
        <li
          v-for="attendee in filteredAttendees"
          :key="attendee.userId"
          class="attendee"
          :style="attendeeStyle(attendee)"
        >
          <div class="attendee__head">
            <div class="attendee__identity">
              <span class="attendee__name" :title="attendee.userTypeName ?? undefined">
                {{ fullName(attendee) }}
              </span>
              <span v-if="ageFrom(attendee.birthDate) !== null" class="attendee__age">
                {{ ageFrom(attendee.birthDate) }} años
              </span>
              <span
                v-if="hasConflicts(attendee)"
                class="attendee__conflict"
                title="Tiene actividades solapadas en el tiempo"
              >
                <i class="pi pi-exclamation-triangle" aria-hidden="true" /> Solapamiento
              </span>
            </div>
            <div class="attendee__contact">
              <span
                ><i class="pi pi-envelope" aria-hidden="true" /> {{ attendee.email || '—' }}</span
              >
              <span><i class="pi pi-phone" aria-hidden="true" /> {{ attendee.phone || '—' }}</span>
            </div>
          </div>

          <p v-if="attendee.guardian" class="attendee__guardian">
            <i class="pi pi-user" aria-hidden="true" />
            Tutor/a: {{ attendee.guardian.firstName }} {{ attendee.guardian.lastName }} ·
            {{ attendee.guardian.email || '—' }} · {{ attendee.guardian.phone || '—' }}
          </p>

          <ul class="attendee__assignments">
            <li
              v-for="assignment in visibleAssignments(attendee)"
              :key="assignment.activityId"
              class="assignment"
            >
              <span class="assignment__title">{{ assignment.activityTitle || '—' }}</span>
              <span class="assignment__role">{{ assignment.roleTypeName || '—' }}</span>
              <span class="assignment__signed" title="Fecha de inscripción">
                <i class="pi pi-calendar-plus" aria-hidden="true" />
                {{ formatDateTime(assignment.signedUpAt) }}
              </span>
              <span class="assignment__status">
                <ColorTag :value="assignment.statusName || '—'" :color="statusColor(assignment)" />
                <span
                  v-if="assignment.hasTimeConflict"
                  class="assignment__warning"
                  title="Se solapa en el tiempo con otra actividad de este usuario"
                >
                  <i class="pi pi-exclamation-triangle" aria-hidden="true" />
                </span>
              </span>
              <div class="assignment__actions">
                <Button
                  icon="pi pi-tag"
                  text
                  rounded
                  size="small"
                  aria-label="Cambiar rol"
                  @click="openChangeRole(attendee, assignment)"
                />
                <Button
                  icon="pi pi-sync"
                  text
                  rounded
                  size="small"
                  aria-label="Cambiar estado"
                  @click="openChangeStatus(attendee, assignment)"
                />
              </div>
            </li>
          </ul>
        </li>
      </ul>
    </DataState>

    <Dialog
      v-model:visible="roleDialogVisible"
      modal
      header="Cambiar rol"
      :style="{ width: '400px' }"
    >
      <p class="dialog-context">
        {{ roleTarget ? fullName(roleTarget.attendee) : '' }} ·
        {{ roleTarget?.assignment.activityTitle }}
      </p>
      <div class="form__field">
        <label>Rol</label>
        <Select
          v-model="selectedRoleId"
          :options="roleDialogOptions"
          option-label="label"
          option-value="value"
          placeholder="Selecciona un rol"
          fluid
        />
        <small v-if="roleDialogOptions.length === 0" class="form__warning">
          No se han podido cargar los roles de la actividad.
        </small>
      </div>
      <template #footer>
        <Button
          label="Cancelar"
          text
          severity="secondary"
          :disabled="assignments.changeRole.isPending.value"
          @click="roleDialogVisible = false"
        />
        <Button
          label="Aplicar"
          :loading="assignments.changeRole.isPending.value"
          :disabled="!selectedRoleId || roleDialogOptions.length === 0"
          @click="submitChangeRole"
        />
      </template>
    </Dialog>

    <Dialog
      v-model:visible="statusDialogVisible"
      modal
      header="Cambiar estado"
      :style="{ width: '400px' }"
    >
      <p class="dialog-context">
        {{ statusTarget ? fullName(statusTarget.attendee) : '' }} ·
        {{ statusTarget?.assignment.activityTitle }}
      </p>
      <div class="form__field">
        <label>Estado</label>
        <Select
          v-model="selectedStatusId"
          :options="statusOptions"
          option-label="label"
          option-value="value"
          placeholder="Selecciona un estado"
          fluid
        />
      </div>
      <template #footer>
        <Button
          label="Cancelar"
          text
          severity="secondary"
          :disabled="assignments.changeStatus.isPending.value"
          @click="statusDialogVisible = false"
        />
        <Button
          label="Aplicar"
          :loading="assignments.changeStatus.isPending.value"
          :disabled="!selectedStatusId"
          @click="submitChangeStatus"
        />
      </template>
    </Dialog>
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
  min-width: 160px;
}

.toolbar__sort {
  display: flex;
  align-items: center;
  gap: 4px;
  margin-left: auto;
}

.count {
  font-size: 13px;
  color: var(--ca-text-muted);
  margin-bottom: 12px;
}

.no-match {
  color: var(--ca-text-dim);
  font-family: var(--ca-font-mono);
}

.attendees {
  list-style: none;
  margin: 0;
  padding: 0;
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.attendee {
  background: var(--ca-surface);
  border: 1px solid var(--ca-border-soft);
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
}

.attendee__contact i {
  font-size: 12px;
  margin-right: 4px;
}

.attendee__guardian {
  margin: 8px 0 0;
  font-size: 13px;
  color: var(--ca-text-muted);
}

.attendee__guardian i {
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

@media (max-width: 760px) {
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
