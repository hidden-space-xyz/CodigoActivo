<script setup lang="ts">
import { computed } from 'vue'

import { useDashboardSummary } from '../model/useDashboardSummary'
import { AdminPageHeader, DataState } from '@/shared/ui'

const { data, isLoading, isError } = useDashboardSummary()

const cards = computed(() => {
  const summary = data.value
  return [
    {
      label: 'Eventos',
      value: summary?.events ?? 0,
      icon: 'pi pi-calendar',
      color: 'var(--ca-orange)',
    },
    {
      label: 'Actividades',
      value: summary?.activities ?? 0,
      icon: 'pi pi-check-square',
      color: 'var(--ca-lime)',
    },
    {
      label: 'Recursos',
      value: summary?.resources ?? 0,
      icon: 'pi pi-book',
      color: 'var(--ca-azure)',
    },
    {
      label: 'Anuncios',
      value: summary?.announcements ?? 0,
      icon: 'pi pi-megaphone',
      color: 'var(--ca-orange)',
    },
    {
      label: 'Socios',
      value: summary?.partners ?? 0,
      icon: 'pi pi-building',
      color: 'var(--ca-lime)',
    },
    { label: 'Usuarios', value: summary?.users ?? 0, icon: 'pi pi-users', color: 'var(--ca-azure)' },
  ]
})
</script>

<template>
  <div>
    <AdminPageHeader title="Panel" subtitle="Resumen general de la plataforma" />

    <DataState
      :loading="isLoading"
      :error="isError"
      :empty="false"
      error-text="No se pudieron cargar los indicadores."
    >
      <div class="dashboard-grid">
        <article
          v-for="card in cards"
          :key="card.label"
          class="dashboard-card"
          :style="{ '--accent': card.color }"
        >
          <div class="dashboard-card__icon"><i :class="card.icon" /></div>
          <div class="dashboard-card__value">{{ card.value }}</div>
          <div class="dashboard-card__label">{{ card.label }}</div>
        </article>
      </div>
    </DataState>
  </div>
</template>

<style scoped>
.dashboard-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(220px, 1fr));
  gap: 18px;
}

.dashboard-card {
  background: var(--ca-surface);
  border: 1px solid var(--ca-border-soft);
  border-radius: 16px;
  padding: 24px;
  border-left: 3px solid var(--accent);
}

.dashboard-card__icon {
  color: var(--accent);
  font-size: 22px;
}

.dashboard-card__value {
  margin-top: 14px;
  font-family: var(--ca-font-display);
  font-weight: 700;
  font-size: 38px;
  color: var(--ca-text-bright);
}

.dashboard-card__label {
  margin-top: 4px;
  font-size: 14px;
  color: var(--ca-text-muted);
}
</style>
