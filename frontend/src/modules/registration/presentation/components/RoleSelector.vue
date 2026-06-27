<script setup lang="ts">
import {
  REGISTRATION_ROLES,
  ROLE_DESCRIPTIONS,
  ROLE_LABELS,
  type RegistrationRole,
} from '@/modules/registration/domain/value-objects/registration-role'

const emit = defineEmits<{ select: [role: RegistrationRole] }>()

const roleVisuals: Record<RegistrationRole, { icon: string; color: string; soft: string }> = {
  member: { icon: '🤝', color: 'var(--ca-cyan)', soft: 'rgba(45,212,217,0.13)' },
  occasionalVolunteer: { icon: '✋', color: 'var(--ca-green)', soft: 'rgba(91,229,132,0.13)' },
  participant: { icon: '🎓', color: 'var(--ca-purple)', soft: 'rgba(167,139,250,0.13)' },
}
</script>

<template>
  <div class="role-selector">
    <h2 class="role-selector__heading">¿Cómo quieres registrarte?</h2>
    <div class="role-selector__grid">
      <button
        v-for="role in REGISTRATION_ROLES"
        :key="role"
        type="button"
        class="role-card"
        :style="{ '--role-color': roleVisuals[role].color }"
        @click="emit('select', role)"
      >
        <div class="role-card__icon" :style="{ background: roleVisuals[role].soft }">
          {{ roleVisuals[role].icon }}
        </div>
        <div class="role-card__title">{{ ROLE_LABELS[role] }}</div>
        <div class="role-card__desc">{{ ROLE_DESCRIPTIONS[role] }}</div>
        <div class="role-card__cta" :style="{ color: roleVisuals[role].color }">Elegir →</div>
      </button>
    </div>
  </div>
</template>

<style scoped>
.role-selector__heading {
  font-family: var(--ca-font-display);
  font-weight: 700;
  font-size: 28px;
  letter-spacing: -0.02em;
  color: var(--ca-text-bright);
  margin-bottom: 24px;
}

.role-selector__grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(240px, 1fr));
  gap: 18px;
}

.role-card {
  display: flex;
  flex-direction: column;
  align-items: flex-start;
  text-align: left;
  background: var(--ca-surface);
  border: 1px solid var(--ca-border-soft);
  border-radius: 16px;
  padding: 26px;
  cursor: pointer;
  transition:
    transform 0.16s ease,
    border-color 0.16s ease;
}

.role-card:hover {
  transform: translateY(-4px);
  border-color: var(--role-color);
}

.role-card__icon {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 46px;
  height: 46px;
  border-radius: 12px;
  font-size: 22px;
  margin-bottom: 16px;
}

.role-card__title {
  font-family: var(--ca-font-display);
  font-weight: 600;
  font-size: 20px;
  color: var(--ca-text);
}

.role-card__desc {
  margin-top: 8px;
  font-size: 14.5px;
  line-height: 1.55;
  color: var(--ca-text-muted);
  flex: 1;
}

.role-card__cta {
  margin-top: 18px;
  font-family: var(--ca-font-display);
  font-weight: 600;
  font-size: 14px;
}
</style>
