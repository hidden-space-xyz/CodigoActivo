<script setup lang="ts">
import { ref } from 'vue'
import Button from 'primevue/button'
import Select from 'primevue/select'
import Tag from 'primevue/tag'

import BaseButton from '@/shared/ui/components/BaseButton.vue'
import { formatDateTime } from '@/shared/utils/format'

import type { TimelineActivity } from './activity-timeline.types'

const props = defineProps<{
  activity: TimelineActivity
  busy: boolean
  authenticated: boolean
  signupOpen: boolean
  hasHousehold: boolean
}>()
const emit = defineEmits<{
  signup: [roleId: string]
  unassign: []
  unassignMember: [userId: string]
  household: []
  login: []
}>()

const selectedRoleId = ref(
  props.activity.roles.length === 1 ? (props.activity.roles[0]?.id ?? '') : '',
)

function scheduleLabel(): string {
  const { start, end } = props.activity
  if (!start) return 'Sin horario'
  const startLabel = formatDateTime(start.toISOString())
  return end ? `${startLabel} – ${formatDateTime(end.toISOString())}` : startLabel
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

    <div v-if="activity.roles.length" class="act__roles">
      <Tag v-for="role in activity.roles" :key="role.id" :value="role.name" severity="secondary" />
    </div>

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
          type="button"
          class="act__member-remove"
          aria-label="Desapuntar"
          :disabled="busy"
          @click="emit('unassignMember', member.userId)"
        >
          ✕
        </button>
      </li>
    </ul>

    <div class="act__actions">
      <template v-if="!authenticated">
        <BaseButton variant="ghost" @click="emit('login')">Inicia sesión para apuntarte</BaseButton>
      </template>

      <!-- Household flow: an adult with minors picks who to sign up and each role. -->
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
          :loading="busy"
          :disabled="activity.roles.length === 0"
          @click="emit('household')"
        />
        <span v-if="signupOpen && activity.roles.length === 0" class="act__note">
          Sin roles disponibles
        </span>
      </template>

      <!-- Solo flow: a user without minors signs themselves up. -->
      <template v-else-if="activity.assignment">
        <span class="act__note">Inscrito como {{ activity.assignment.roleName || '—' }}</span>
        <Button
          label="Desapuntarme"
          severity="secondary"
          size="small"
          :loading="busy"
          @click="emit('unassign')"
        />
      </template>
      <template v-else-if="!signupOpen">
        <span class="act__note">La inscripción no está abierta.</span>
      </template>
      <template v-else>
        <Select
          v-if="activity.roles.length > 1"
          v-model="selectedRoleId"
          :options="activity.roles"
          option-label="name"
          option-value="id"
          placeholder="Elige un rol"
          class="act__role-select"
        />
        <Button
          label="Apuntarme"
          size="small"
          :loading="busy"
          :disabled="activity.roles.length === 0 || !selectedRoleId"
          @click="onSignup"
        />
        <span v-if="activity.roles.length === 0" class="act__note">Sin roles disponibles</span>
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
  border-color: var(--ca-cyan);
  background: rgba(45, 212, 217, 0.06);
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

.act__roles {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
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
  border-color: var(--ca-amber);
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
