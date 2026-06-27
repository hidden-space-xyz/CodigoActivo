<script setup lang="ts">
import type { LearningResource } from '@/modules/resources/domain/entities/learning-resource.entity'

defineProps<{ resource: LearningResource }>()
</script>

<template>
  <article class="resource-card">
    <div class="resource-card__top">
      <div class="resource-card__icon">&lt;/&gt;</div>
    </div>

    <h3 class="resource-card__title">{{ resource.title }}</h3>
    <p class="resource-card__desc">{{ resource.description }}</p>

    <div class="resource-card__footer">
      <span class="resource-card__type">{{ resource.type }}</span>
      <span class="resource-card__open">Abrir →</span>
    </div>
  </article>
</template>

<style scoped>
.resource-card {
  background: var(--ca-surface);
  border: 1px solid var(--ca-border-soft);
  border-radius: 16px;
  padding: 26px;
  display: flex;
  flex-direction: column;
  cursor: pointer;
  transition:
    transform 0.16s ease,
    border-color 0.16s ease;
}

.resource-card:hover {
  transform: translateY(-4px);
  border-color: rgba(255, 255, 255, 0.2);
}

.resource-card__top {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 18px;
}

.resource-card__icon {
  width: 46px;
  height: 46px;
  border-radius: 12px;
  display: flex;
  align-items: center;
  justify-content: center;
  font-family: var(--ca-font-mono);
  font-weight: 600;
  font-size: 16px;
  color: var(--ca-cyan);
  background: rgba(45, 212, 217, 0.14);
}

.resource-card__title {
  font-family: var(--ca-font-display);
  font-weight: 600;
  font-size: 20px;
  color: var(--ca-text);
}

.resource-card__desc {
  margin-top: 8px;
  font-size: 14.5px;
  line-height: 1.55;
  color: var(--ca-text-muted);
  flex: 1;
}

.resource-card__footer {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-top: 18px;
  padding-top: 16px;
  border-top: 1px solid var(--ca-border);
}

.resource-card__type {
  font-family: var(--ca-font-mono);
  font-size: 12px;
  color: var(--ca-text-faint-2);
}

.resource-card__open {
  font-size: 14px;
  font-weight: 600;
  color: var(--ca-cyan);
}
</style>
