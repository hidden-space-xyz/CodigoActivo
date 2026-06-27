<script setup lang="ts">
import { computed } from 'vue'

import type { EventAgendaItem } from '@/modules/events/domain/entities/event.entity'

const props = defineProps<{ accentColor: string }>()

const items: readonly EventAgendaItem[] = [
  { time: '10:00', description: 'Bienvenida y formación de equipos' },
  { time: '10:30', description: 'Reto guiado: primeros pasos con mentores' },
  { time: '12:30', description: 'Tiempo de proyecto libre y experimentación' },
  { time: '14:00', description: 'Presentación de proyectos y premios' },
]

const accent = computed(() => props.accentColor)
</script>

<template>
  <div class="agenda">
    <div v-for="(item, index) in items" :key="index" class="agenda__row">
      <span class="agenda__time" :style="{ color: accent }">{{ item.time }}</span>
      <span class="agenda__text">{{ item.description }}</span>
    </div>
  </div>
</template>

<style scoped>
.agenda {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.agenda__row {
  display: flex;
  gap: 16px;
  align-items: flex-start;
  background: var(--ca-surface);
  border: 1px solid var(--ca-border);
  border-radius: 12px;
  padding: 16px 18px;
}

.agenda__time {
  font-family: var(--ca-font-mono);
  font-size: 13px;
  font-weight: 600;
  min-width: 54px;
}

.agenda__text {
  font-size: 15.5px;
  color: #d2d7e0;
}
</style>
