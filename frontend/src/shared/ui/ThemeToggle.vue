<script setup lang="ts">
import { computed } from 'vue'

import { useTheme } from '@/shared/lib'

const { theme, toggleTheme } = useTheme()

const isDark = computed(() => theme.value === 'dark')
const label = computed(() => (isDark.value ? 'Cambiar a tema claro' : 'Cambiar a tema oscuro'))
</script>

<template>
  <button
    type="button"
    class="theme-toggle"
    :class="{ 'theme-toggle--dark': isDark }"
    :aria-label="label"
    :title="label"
    :aria-pressed="isDark"
    @click="toggleTheme"
  >
    <span class="theme-toggle__track">
      <i class="pi pi-sun theme-toggle__icon theme-toggle__icon--sun" aria-hidden="true" />
      <i class="pi pi-moon theme-toggle__icon theme-toggle__icon--moon" aria-hidden="true" />
      <span class="theme-toggle__thumb" />
    </span>
  </button>
</template>

<style scoped>
.theme-toggle {
  display: inline-flex;
  align-items: center;
  padding: 0;
  border: none;
  background: transparent;
  cursor: pointer;
  line-height: 0;
}

.theme-toggle__track {
  position: relative;
  display: inline-flex;
  align-items: center;
  justify-content: space-between;
  width: 54px;
  height: 28px;
  padding: 0 7px;
  border-radius: 999px;
  border: 1px solid var(--ca-border-strong);
  background: var(--ca-surface-2);
  transition:
    background 0.25s ease,
    border-color 0.25s ease;
}

.theme-toggle:hover .theme-toggle__track {
  border-color: var(--ca-orange);
}

.theme-toggle__icon {
  position: relative;
  z-index: 1;
  font-size: 12px;
  transition:
    color 0.25s ease,
    opacity 0.25s ease;
}

.theme-toggle__icon--sun {
  color: var(--ca-orange);
}

.theme-toggle__icon--moon {
  color: var(--ca-text-faint);
}

.theme-toggle--dark .theme-toggle__icon--sun {
  color: var(--ca-text-faint);
}

.theme-toggle--dark .theme-toggle__icon--moon {
  color: var(--ca-orange);
}

.theme-toggle__thumb {
  position: absolute;
  top: 50%;
  left: 3px;
  width: 22px;
  height: 22px;
  border-radius: 50%;
  background: var(--ca-orange);
  box-shadow: var(--ca-shadow-sm);
  transform: translateY(-50%);
  transition: transform 0.28s cubic-bezier(0.16, 1, 0.3, 1);
}

.theme-toggle--dark .theme-toggle__thumb {
  transform: translate(26px, -50%);
}
</style>
