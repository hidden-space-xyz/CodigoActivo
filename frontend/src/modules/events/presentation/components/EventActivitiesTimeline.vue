<script setup lang="ts">
import { computed, reactive, ref } from 'vue'
import { useRouter } from 'vue-router'
import { useToast } from 'primevue/usetoast'
import Button from 'primevue/button'
import Dialog from 'primevue/dialog'

import { useEventActivities } from '@/modules/events/presentation/composables/useEventActivities'
import ActivityTimelineCard from '@/modules/events/presentation/components/ActivityTimelineCard.vue'
import type { TimelineActivity } from '@/modules/events/presentation/components/activity-timeline.types'
import type { OverlappingActivityResponse } from '@/shared/api/generated/models'
import { getErrorMessage } from '@/shared/utils/api-error'
import { formatDateTime } from '@/shared/utils/format'

const props = defineProps<{ eventId: string; signupOpen: boolean }>()

const router = useRouter()
const toast = useToast()
const { activities, assigned, assign, unassign, verifyOverlaps, isAuthenticated } =
  useEventActivities(() => props.eventId)

interface Cluster {
  start: Date
  items: TimelineActivity[]
}

const assignmentByActivity = computed(() => {
  const map = new Map<string, { status: string; roleName: string }>()
  for (const a of assigned.data.value ?? []) {
    if (a.activityId) {
      map.set(a.activityId, { status: a.status?.name ?? '—', roleName: a.roleType?.name ?? '' })
    }
  }
  return map
})

const items = computed<TimelineActivity[]>(() =>
  (activities.data.value ?? []).map((a) => ({
    id: a.id ?? '',
    title: a.title ?? 'Actividad',
    start: a.activityStartsAt ? new Date(a.activityStartsAt) : null,
    end: a.activityEndsAt ? new Date(a.activityEndsAt) : null,
    roles: (a.allowedRoleTypes ?? [])
      .filter((r) => r.roleTypeId)
      .map((r) => ({ id: r.roleTypeId as string, name: r.roleTypeName ?? 'Rol' })),
    assignment: assignmentByActivity.value.get(a.id ?? '') ?? null,
  })),
)

const scheduled = computed(() =>
  items.value
    .filter((a): a is TimelineActivity & { start: Date } => a.start !== null)
    .sort((x, y) => x.start.getTime() - y.start.getTime()),
)

const unscheduled = computed(() => items.value.filter((a) => a.start === null))

// Group activities that overlap in time into the same timeline node so that
// simultaneous activities are shown side by side.
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
  overlaps: OverlappingActivityResponse[]
}>({ visible: false, activity: null, roleId: '', overlaps: [] })

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
    toast.add({ severity: 'error', summary: 'Error', detail: getErrorMessage(error), life: 4000 })
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
      onSuccess: () =>
        toast.add({
          severity: 'success',
          summary: 'Inscripción enviada',
          detail: 'Te has apuntado a la actividad.',
          life: 3500,
        }),
      onError: (error) =>
        toast.add({
          severity: 'error',
          summary: 'No se pudo apuntar',
          detail: getErrorMessage(error),
          life: 4000,
        }),
      onSettled: () => {
        busyId.value = null
      },
    },
  )
}

function onUnassign(activity: TimelineActivity): void {
  busyId.value = activity.id
  unassign.mutate(activity.id, {
    onSuccess: () =>
      toast.add({
        severity: 'success',
        summary: 'Te has desapuntado',
        detail: `Ya no estás inscrito en "${activity.title}".`,
        life: 3500,
      }),
    onError: (error) =>
      toast.add({ severity: 'error', summary: 'Error', detail: getErrorMessage(error), life: 4000 }),
    onSettled: () => {
      busyId.value = null
    },
  })
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
                :busy="busyId === act.id"
                @signup="onSignup(act, $event)"
                @unassign="onUnassign(act)"
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
            :busy="busyId === act.id"
            @signup="onSignup(act, $event)"
            @unassign="onUnassign(act)"
            @login="goLogin"
          />
        </div>
      </section>
    </template>

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
        <Button
          label="Cancelar"
          text
          severity="secondary"
          @click="overlapDialog.visible = false"
        />
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
