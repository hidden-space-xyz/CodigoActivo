<script setup lang="ts">
defineProps<{ title: string; subtitle?: string }>()
</script>

<template>
  <section class="chart-card">
    <header class="chart-card__head">
      <h2 class="chart-card__title">{{ title }}</h2>
      <p v-if="subtitle" class="chart-card__subtitle">{{ subtitle }}</p>
    </header>
    <div class="chart-card__body">
      <slot />
    </div>
  </section>
</template>

<style scoped>
.chart-card {
  display: flex;
  flex-direction: column;
  height: 100%;
  background: var(--ca-surface);
  border: 1px solid var(--ca-border-soft);
  border-radius: 16px;
  padding: 20px 22px;
}

.chart-card__title {
  font-family: var(--ca-font-display);
  font-size: 16px;
  font-weight: 600;
  color: var(--ca-text-bright);
  letter-spacing: -0.01em;
}

.chart-card__subtitle {
  margin-top: 3px;
  font-size: 12.5px;
  color: var(--ca-text-muted);
}

.chart-card__body {
  margin-top: 16px;
  flex: 1;
}
</style>
