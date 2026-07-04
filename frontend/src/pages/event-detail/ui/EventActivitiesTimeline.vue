<script setup lang="ts">
import { computed, reactive, ref } from 'vue'
import { useRouter } from 'vue-router'
import { useToast } from 'primevue/usetoast'
import { AppButton as Button } from '@/shared/ui'
import Checkbox from 'primevue/checkbox'
import Dialog from 'primevue/dialog'
import Select from 'primevue/select'

import { useEventActivities } from '../model/useEventActivities'
import ActivityTimelineCard from './ActivityTimelineCard.vue'
import type { TimelineActivity, TimelineMemberAssignment } from '../model/activity-timeline.types'
import type { ActivityOverlap } from '@/entities/activity'
import { formatDateTime, useCrudFeedback } from '@/shared/lib'

const props = defineProps<{ eventId: string; signupOpen: boolean }>()

const router = useRouter()
const toast = useToast()
const feedback = useCrudFeedback()
const {
  activities,
  assigned,
  household,
  hasHousehold,
  members,
  userId,
  assign,
  assignHousehold,
  unassign,
  verifyOverlaps,
  isAuthenticated,
} = useEventActivities(() => props.eventId)

interface Cluster {
  start: Date
  items: TimelineActivity[]
}

const assignmentByActivity = computed(() => {
  const map = new Map<string, { status: string; roleName: string }>()
  for (const a of assigned.data.value ?? []) {
    if (a.activityId) {
      map.set(a.activityId, { status: a.status, roleName: a.roleName })
    }
  }
  return map
})

const householdByActivity = computed(() => {
  const map = new Map<string, TimelineMemberAssignment[]>()
  for (const a of household.data.value ?? []) {
    if (!a.activityId) continue
    const list = map.get(a.activityId) ?? []
    list.push({
      userId: a.userId,
      name: a.name,
      roleName: a.roleName,
      status: a.status,
    })
    map.set(a.activityId, list)
  }
  return map
})

const items = computed<TimelineActivity[]>(() =>
  (activities.data.value ?? []).map((a) => ({
    id: a.id,
    title: a.title,
    description: a.description,
    location: a.location,
    modality: a.modality,
    start: a.startsAt ? new Date(a.startsAt) : null,
    end: a.endsAt ? new Date(a.endsAt) : null,
    roles: a.roles.map((r) => ({ id: r.id, name: r.name })),
    assignment: assignmentByActivity.value.get(a.id) ?? null,
    household: householdByActivity.value.get(a.id) ?? [],
  })),
)

const scheduled = computed(() =>
  items.value
    .filter((a): a is TimelineActivity & { start: Date } => a.start !== null)
    .sort((x, y) => x.start.getTime() - y.start.getTime()),
)

const unscheduled = computed(() => items.value.filter((a) => a.start === null))

const clusters = computed<Cluster[]>(() => {
  const result: Cluster[] = []
  let current: Cluster | null = null
  let maxEnd = 0
  for (const act of scheduled.value) {
    const start = act.start.getTime()
    const end = (act.end ?? act.start).getTime()
    if (current && start < maxEnd) {
      current.items.push(act)
      maxEnd = Math.max(maxEnd, end)
    } else {
      current = { start: act.start, items: [act] }
      result.push(current)
      maxEnd = end
    }
  }
  return result
})

const busyId = ref<string | null>(null)

const overlapDialog = reactive<{
  visible: boolean
  activity: TimelineActivity | null
  roleId: string
  overlaps: readonly ActivityOverlap[]
}>({ visible: false, activity: null, roleId: '', overlaps: [] })

interface HouseholdRow {
  userId: string
  name: string
  alreadyAssigned: boolean
  assignedRole: string
  include: boolean
  roleId: string
}

const householdDialog = reactive<{
  visible: boolean
  activity: TimelineActivity | null
  rows: HouseholdRow[]
}>({ visible: false, activity: null, rows: [] })

function goLogin(): void {
  void router.push({ name: 'login', query: { redirect: `/events/${props.eventId}` } })
}

async function onSignup(activity: TimelineActivity, roleId: string): Promise<void> {
  if (!props.signupOpen) return
  busyId.value = activity.id
  try {
    const overlap = await verifyOverlaps(activity.id)
    if (overlap?.hasOverlaps) {
      overlapDialog.activity = activity
      overlapDialog.roleId = roleId
      overlapDialog.overlaps = overlap.overlaps ?? []
      overlapDialog.visible = true
      busyId.value = null
      return
    }
    doAssign(activity.id, roleId)
  } catch (error) {
    busyId.value = null
    feedback.error(error)
  }
}

function confirmOverlapSignup(): void {
  if (!overlapDialog.activity) return
  doAssign(overlapDialog.activity.id, overlapDialog.roleId)
  overlapDialog.visible = false
}

function doAssign(activityId: string, roleId: string): void {
  busyId.value = activityId
  assign.mutate(
    { activityId, activityRoleTypeId: roleId },
    {
      onSuccess: () => feedback.success('Te has apuntado a la actividad.', 'Inscripción enviada'),
      onError: (error) => feedback.error(error, 'No se pudo apuntar'),
      onSettled: () => {
        busyId.value = null
      },
    },
  )
}

function openHousehold(activity: TimelineActivity): void {
  const defaultRole = activity.roles.length === 1 ? (activity.roles[0]?.id ?? '') : ''
  householdDialog.activity = activity
  householdDialog.rows = members.value.map((member) => {
    const existing = activity.household.find((h) => h.userId === member.id)
    return {
      userId: member.id,
      name: member.name,
      alreadyAssigned: existing !== undefined,
      assignedRole: existing?.roleName ?? '',
      include: existing === undefined,
      roleId: defaultRole,
    }
  })
  householdDialog.visible = true
}

const householdSelectable = computed(() =>
  householdDialog.rows.filter((row) => !row.alreadyAssigned),
)

function confirmHousehold(): void {
  const activity = householdDialog.activity
  if (!activity) return

  const includedRows = householdDialog.rows.filter(
    (row) => row.include && !row.alreadyAssigned,
  )
  const missingRole = includedRows.some((row) => !row.roleId)
  if (missingRole) {
    toast.add({
      severity: 'warn',
      summary: 'Falta el rol',
      detail: 'Selecciona un rol para cada integrante que quieras apuntar.',
      life: 3500,
    })
    return
  }

  const assignments = includedRows.map((row) => ({ userId: row.userId, roleId: row.roleId }))

  if (assignments.length === 0) {
    householdDialog.visible = false
    return
  }

  busyId.value = activity.id
  assignHousehold.mutate(
    { activityId: activity.id, assignments },
    {
      onSuccess: () => {
        householdDialog.visible = false
        feedback.success('Habéis quedado apuntados a la actividad.', 'Inscripción enviada')
      },
      onError: (error) => feedback.error(error, 'No se pudo apuntar'),
      onSettled: () => {
        busyId.value = null
      },
    },
  )
}

function onUnassignMember(activity: TimelineActivity, memberId: string): void {
  if (!props.signupOpen) return
  busyId.value = activity.id
  unassign.mutate(
    { activityId: activity.id, userId: memberId },
    {
      onSuccess: () => feedback.success('Se ha eliminado la inscripción.', 'Inscripción cancelada'),
      onError: (error) => feedback.error(error),
      onSettled: () => {
        busyId.value = null
      },
    },
  )
}

function onUnassign(activity: TimelineActivity): void {
  if (!userId.value) return
  onUnassignMember(activity, userId.value)
}
</script>

<template>
  <div class="activities">
    <p v-if="activities.isLoading.value" class="activities__state">Cargando actividades…</p>
    <p v-else-if="activities.isError.value" class="activities__state">
      No se pudieron cargar las actividades.
    </p>
    <p v-else-if="items.length === 0" class="activities__state">
      Este evento todavía no tiene actividades.
    </p>

    <template v-else>
      <p v-if="!signupOpen" class="signup-closed">
        <i class="pi pi-info-circle" /> La inscripción no está abierta para este evento.
      </p>

      <ol class="timeline">
        <li v-for="(cluster, index) in clusters" :key="index" class="tl-node">
          <div class="tl-rail">
            <span class="tl-dot" />
          </div>
          <div class="tl-content">
            <div class="tl-time">
              {{ formatDateTime(cluster.start.toISOString()) }}
              <span v-if="cluster.items.length > 1" class="tl-simul">
                · {{ cluster.items.length }} simultáneas
              </span>
            </div>
            <div class="tl-cards" :class="{ 'tl-cards--multi': cluster.items.length > 1 }">
              <ActivityTimelineCard
                v-for="act in cluster.items"
                :key="act.id"
                :activity="act"
                :authenticated="isAuthenticated"
                :signup-open="signupOpen"
                :has-household="hasHousehold"
                :busy="busyId === act.id"
                @signup="onSignup(act, $event)"
                @household="openHousehold(act)"
                @unassign="onUnassign(act)"
                @unassign-member="onUnassignMember(act, $event)"
                @login="goLogin"
              />
            </div>
          </div>
        </li>
      </ol>

      <section v-if="unscheduled.length" class="unscheduled">
        <h3 class="unscheduled__title">Sin horario asignado</h3>
        <div class="tl-cards tl-cards--multi">
          <ActivityTimelineCard
            v-for="act in unscheduled"
            :key="act.id"
            :activity="act"
            :authenticated="isAuthenticated"
            :signup-open="signupOpen"
            :has-household="hasHousehold"
            :busy="busyId === act.id"
            @signup="onSignup(act, $event)"
            @household="openHousehold(act)"
            @unassign="onUnassign(act)"
            @unassign-member="onUnassignMember(act, $event)"
            @login="goLogin"
          />
        </div>
      </section>
    </template>

    <Dialog
      v-model:visible="householdDialog.visible"
      modal
      header="¿A quién quieres apuntar?"
      :style="{ width: '90vw', maxWidth: '560px' }"
    >
      <p class="household__lead">
        Marca a las personas que quieras apuntar a
        <b>{{ householdDialog.activity?.title }}</b> y elige el rol de cada una.
      </p>
      <ul class="household__list">
        <li v-for="row in householdDialog.rows" :key="row.userId" class="household__row">
          <div class="household__member">
            <Checkbox
              v-if="!row.alreadyAssigned"
              v-model="row.include"
              binary
              :input-id="`hh-${row.userId}`"
            />
            <label :for="`hh-${row.userId}`" class="household__name">{{ row.name }}</label>
          </div>
          <span v-if="row.alreadyAssigned" class="household__already">
            Ya inscrito como {{ row.assignedRole || '—' }}
          </span>
          <Select
            v-else
            v-model="row.roleId"
            :options="householdDialog.activity?.roles ?? []"
            option-label="name"
            option-value="id"
            placeholder="Elige un rol"
            :disabled="!row.include"
            class="household__role"
          />
        </li>
      </ul>
      <p v-if="householdSelectable.length === 0" class="household__note">
        Toda tu familia ya está inscrita en esta actividad.
      </p>
      <template #footer>
        <Button
          label="Cancelar"
          text
          severity="secondary"
          @click="householdDialog.visible = false"
        />
        <Button
          label="Apuntar"
          :disabled="householdSelectable.length === 0"
          @click="confirmHousehold"
        />
      </template>
    </Dialog>

    <Dialog
      v-model:visible="overlapDialog.visible"
      modal
      header="Coincidencia de horario"
      :style="{ width: '90vw', maxWidth: '480px' }"
    >
      <p class="overlap__lead">
        Ya estás apuntado a otra actividad que coincide en el tiempo con esta:
      </p>
      <ul class="overlap__list">
        <li v-for="o in overlapDialog.overlaps" :key="o.activityId">
          <strong>{{ o.title }}</strong>
          <span class="overlap__when">
            {{ formatDateTime(o.startsAt) }} – {{ formatDateTime(o.endsAt) }}
          </span>
        </li>
      </ul>
      <p class="overlap__q">¿Quieres apuntarte de todos modos?</p>
      <template #footer>
        <Button label="Cancelar" text severity="secondary" @click="overlapDialog.visible = false" />
        <Button label="Apuntarme igualmente" @click="confirmOverlapSignup" />
      </template>
    </Dialog>
  </div>
</template>

<style scoped>
.activities__state {
  padding: 12px 0 24px;
  color: var(--ca-text-dim);
  font-family: var(--ca-font-mono);
}

.signup-closed {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 20px;
  padding: 12px 16px;
  border: 1px solid var(--ca-amber);
  background: rgba(255, 194, 75, 0.12);
  border-radius: 12px;
  color: var(--ca-text);
  font-size: 14.5px;
}

.signup-closed .pi {
  color: var(--ca-amber);
}

.timeline {
  list-style: none;
  margin: 8px 0 0;
  padding: 0;
}

.tl-node {
  display: grid;
  grid-template-columns: 24px 1fr;
  gap: 16px;
}

.tl-rail {
  display: flex;
  flex-direction: column;
  align-items: center;
}

.tl-dot {
  width: 14px;
  height: 14px;
  margin-top: 4px;
  border-radius: 50%;
  background: var(--ca-cyan);
  box-shadow: 0 0 0 4px rgba(45, 212, 217, 0.16);
}

.tl-rail::after {
  content: '';
  flex: 1;
  width: 2px;
  margin-top: 6px;
  background: var(--ca-border-strong);
}

.tl-node:last-child .tl-rail::after {
  display: none;
}

.tl-content {
  padding-bottom: 26px;
  min-width: 0;
}

.tl-time {
  font-family: var(--ca-font-mono);
  font-size: 13px;
  color: var(--ca-text);
  margin-bottom: 10px;
}

.tl-simul {
  color: var(--ca-amber);
}

.tl-cards {
  display: grid;
  gap: 14px;
}

.tl-cards--multi {
  grid-template-columns: repeat(auto-fit, minmax(260px, 1fr));
}

.unscheduled {
  margin-top: 16px;
}

.unscheduled__title {
  font-family: var(--ca-font-display);
  font-size: 18px;
  font-weight: 600;
  color: var(--ca-text-bright);
  margin-bottom: 14px;
}

.household__lead {
  color: var(--ca-text);
  line-height: 1.55;
  margin-bottom: 16px;
}

.household__list {
  list-style: none;
  margin: 0;
  padding: 0;
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.household__row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 14px;
  background: var(--ca-surface);
  border: 1px solid var(--ca-border-soft);
  border-radius: 12px;
  padding: 12px 14px;
}

.household__member {
  display: flex;
  align-items: center;
  gap: 10px;
  min-width: 0;
}

.household__name {
  font-weight: 600;
  color: var(--ca-text-bright);
  cursor: pointer;
}

.household__already {
  font-size: 13px;
  color: var(--ca-text-muted);
}

.household__role {
  min-width: 170px;
}

.household__note {
  margin-top: 14px;
  font-size: 13.5px;
  color: var(--ca-text-muted);
}

.overlap__lead {
  color: var(--ca-text);
  margin-bottom: 12px;
}

.overlap__list {
  list-style: none;
  margin: 0 0 16px;
  padding: 0;
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.overlap__list li {
  display: flex;
  flex-direction: column;
  gap: 2px;
  background: var(--ca-surface);
  border: 1px solid var(--ca-border-soft);
  border-radius: 10px;
  padding: 10px 12px;
}

.overlap__when {
  font-family: var(--ca-font-mono);
  font-size: 12.5px;
  color: var(--ca-text-muted);
}

.overlap__q {
  color: var(--ca-text);
  font-weight: 600;
}
</style>
