<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { AppButton as Button, BaseButton } from '@/shared/ui'
import Select from 'primevue/select'
import Tag from 'primevue/tag'

import { formatTimeRange } from '@/shared/lib'

import type { TimelineActivity, TimelineRole } from '../model/activity-timeline.types'

const props = defineProps<{
  activity: TimelineActivity
  roles: readonly TimelineRole[]
  rolesLoading: boolean
  busy: boolean
  authenticated: boolean
  signupOpen: boolean
  hasHousehold: boolean
  referenceDate?: Date | null
}>()
const emit = defineEmits<{
  signup: [roleId: string]
  unassign: []
  unassignMember: [userId: string]
  household: []
  login: []
}>()

const selectedRoleId = ref('')

watch(
  () => props.roles,
  (roles) => {
    if (roles.length === 1) selectedRoleId.value = roles[0]?.id ?? ''
  },
  { immediate: true },
)

const selectedRoleHighDemand = computed(
  () =>
    props.authenticated &&
    props.signupOpen &&
    !props.hasHousehold &&
    !props.activity.assignment &&
    !!selectedRoleId.value &&
    props.activity.highDemandRoleIds.includes(selectedRoleId.value),
)

function scheduleLabel(): string {
  const { start, end } = props.activity
  if (!start) return 'Sin horario'
  return formatTimeRange(start, end, props.referenceDate)
}

function statusSeverity(name: string): 'success' | 'danger' | 'info' {
  const n = name.toLowerCase()
  if (n.includes('confirm') || n.includes('acept') || n.includes('aprob')) return 'success'
  if (n.includes('rechaz') || n.includes('deneg') || n.includes('cancel')) return 'danger'
  return 'info'
}

function onSignup(): void {
  if (selectedRoleId.value) emit('signup', selectedRoleId.value)
}
</script>

<template>
  <article class="act" :class="{ 'act--mine': activity.assignment || activity.household.length }">
    <div class="act__head">
      <h4 class="act__title">{{ activity.title }}</h4>
      <Tag
        v-if="!hasHousehold && activity.assignment"
        :value="activity.assignment.status"
        :severity="statusSeverity(activity.assignment.status)"
      />
    </div>

    <div class="act__time"><i class="pi pi-clock" /> {{ scheduleLabel() }}</div>

    <div v-if="activity.modality || activity.location" class="act__meta">
      <i class="pi pi-map-marker" />
      <span>{{ [activity.modality, activity.location].filter(Boolean).join(' · ') }}</span>
    </div>

    <p v-if="activity.description" class="act__desc">{{ activity.description }}</p>

    <ul v-if="hasHousehold && activity.household.length" class="act__members">
      <li v-for="member in activity.household" :key="member.userId" class="act__member">
        <span class="act__member-info">
          <b>{{ member.name }}</b> · {{ member.roleName || '—' }}
          <Tag
            :value="member.status"
            :severity="statusSeverity(member.status)"
            class="act__member-tag"
          />
        </span>
        <button
          v-if="signupOpen"
          type="button"
          class="act__member-remove"
          aria-label="Desapuntar"
          title="Desapuntar"
          :disabled="busy"
          @click="emit('unassignMember', member.userId)"
        >
          ✕
        </button>
      </li>
    </ul>

    <p v-if="selectedRoleHighDemand" class="act__demand">
      <i class="pi pi-exclamation-triangle" />
      <span>
        Esta actividad está muy solicitada y es posible que se agoten las plazas. Te recomendamos
        elegir otras opciones adicionales.
      </span>
    </p>

    <div class="act__actions">
      <template v-if="!authenticated">
        <BaseButton variant="ghost" @click="emit('login')">Inicia sesión para apuntarte</BaseButton>
      </template>

      <template v-else-if="hasHousehold">
        <template v-if="!signupOpen">
          <span v-if="!activity.household.length" class="act__note">
            La inscripción no está abierta.
          </span>
        </template>
        <Button
          v-else
          :label="activity.household.length ? 'Apuntar a otro miembro' : 'Apuntar a mi familia'"
          size="small"
          :loading="busy || rolesLoading"
          @click="emit('household')"
        />
      </template>

      <template v-else-if="activity.assignment">
        <span class="act__note">Inscrito como {{ activity.assignment.roleName || '—' }}</span>
        <Button
          v-if="signupOpen"
          label="Desapuntarme"
          severity="secondary"
          size="small"
          :loading="busy"
          @click="emit('unassign')"
        />
        <span v-else class="act__note">El periodo de inscripción ha finalizado.</span>
      </template>
      <template v-else-if="!signupOpen">
        <span class="act__note">La inscripción no está abierta.</span>
      </template>
      <template v-else>
        <span v-if="rolesLoading" class="act__note">Cargando roles…</span>
        <span v-else-if="roles.length === 0" class="act__note">
          No se pudieron cargar los roles de inscripción.
        </span>
        <template v-else>
          <Select
            v-if="roles.length > 1"
            v-model="selectedRoleId"
            :options="[...roles]"
            option-label="name"
            option-value="id"
            placeholder="Elige un rol"
            class="act__role-select"
          />
          <Button
            label="Apuntarme"
            size="small"
            :loading="busy"
            :disabled="!selectedRoleId"
            @click="onSignup"
          />
        </template>
      </template>
    </div>
  </article>
</template>

<style scoped>
.act {
  display: flex;
  flex-direction: column;
  gap: 10px;
  background: var(--ca-surface);
  border: 1px solid var(--ca-border-soft);
  border-radius: 14px;
  padding: 16px 18px;
}

.act--mine {
  border-color: var(--ca-orange);
  background: var(--ca-orange-soft);
}

.act__head {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 12px;
}

.act__title {
  font-family: var(--ca-font-display);
  font-weight: 600;
  font-size: 17px;
  line-height: 1.2;
  color: var(--ca-text-bright);
}

.act__time {
  display: flex;
  align-items: center;
  gap: 7px;
  font-family: var(--ca-font-mono);
  font-size: 12.5px;
  color: var(--ca-text-muted);
}

.act__meta {
  display: flex;
  align-items: center;
  gap: 7px;
  font-size: 13px;
  color: var(--ca-text-muted);
}

.act__desc {
  margin: 0;
  font-size: 14px;
  line-height: 1.5;
  color: var(--ca-text);
  white-space: pre-line;
}

.act__demand {
  display: flex;
  align-items: flex-start;
  gap: 8px;
  margin: 0;
  padding: 8px 10px;
  border-radius: 10px;
  background: var(--ca-warning-soft);
  color: var(--ca-warning-ink);
  font-size: 13px;
  line-height: 1.45;
}

.act__demand .pi {
  margin-top: 2px;
  font-size: 13px;
}

.act__members {
  list-style: none;
  margin: 0;
  padding: 0;
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.act__member {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 10px;
  background: var(--ca-bg-elevated);
  border: 1px solid var(--ca-border-soft);
  border-radius: 10px;
  padding: 7px 10px;
  font-size: 13px;
  color: var(--ca-text);
}

.act__member-info {
  display: flex;
  align-items: center;
  gap: 8px;
  flex-wrap: wrap;
  min-width: 0;
}

.act__member-tag {
  transform: scale(0.85);
  transform-origin: left center;
}

.act__member-remove {
  flex-shrink: 0;
  width: 24px;
  height: 24px;
  border-radius: 7px;
  border: 1px solid var(--ca-border-strong);
  background: var(--ca-surface);
  color: var(--ca-text-muted);
  cursor: pointer;
  font-size: 12px;
  line-height: 1;
}

.act__member-remove:hover:not(:disabled) {
  color: var(--ca-text-bright);
  border-color: var(--ca-danger);
}

.act__member-remove:disabled {
  opacity: 0.5;
  cursor: default;
}

.act__actions {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 10px;
  margin-top: 2px;
}

.act__role-select {
  min-width: 150px;
}

.act__note {
  font-size: 13px;
  color: var(--ca-text-muted);
}
</style>
