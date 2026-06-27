<script setup lang="ts">
defineProps<{ title: string; subtitle?: string }>()
</script>

<template>
  <div class="page-header">
    <div class="page-header__text">
      <h1 class="page-header__title">{{ title }}</h1>
      <p v-if="subtitle" class="page-header__subtitle">{{ subtitle }}</p>
    </div>
    <div class="page-header__actions">
      <slot name="actions" />
    </div>
  </div>
</template>

<style scoped>
.page-header {
  display: flex;
  align-items: flex-end;
  justify-content: space-between;
  gap: 18px;
  margin-bottom: 22px;
  flex-wrap: wrap;
}

.page-header__title {
  font-family: var(--ca-font-display);
  font-size: 28px;
  font-weight: 700;
  color: var(--ca-text-bright);
  letter-spacing: -0.02em;
}

.page-header__subtitle {
  margin-top: 4px;
  font-size: 14.5px;
  color: var(--ca-text-muted);
}

.page-header__actions {
  display: flex;
  gap: 10px;
}
</style>
