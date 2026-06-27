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
}>()
const emit = defineEmits<{ signup: [roleId: string]; unassign: []; login: [] }>()

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
  <article class="act" :class="{ 'act--mine': activity.assignment }">
    <div class="act__head">
      <h4 class="act__title">{{ activity.title }}</h4>
      <Tag
        v-if="activity.assignment"
        :value="activity.assignment.status"
        :severity="statusSeverity(activity.assignment.status)"
      />
    </div>

    <div class="act__time"><i class="pi pi-clock" /> {{ scheduleLabel() }}</div>

    <div v-if="activity.roles.length" class="act__roles">
      <Tag v-for="role in activity.roles" :key="role.id" :value="role.name" severity="secondary" />
    </div>

    <div class="act__actions">
      <template v-if="activity.assignment">
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
      <template v-else-if="!authenticated">
        <BaseButton variant="ghost" @click="emit('login')">Inicia sesión para apuntarte</BaseButton>
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
