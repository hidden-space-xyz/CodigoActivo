<script setup lang="ts">
import { useId } from 'vue'

import { ActionButton } from '@/shared/ui/action-button'

defineProps<{
  /** Heading of the catalog. */
  title: string
  /** Label of the button that creates an entry. */
  newLabel: string
}>()

defineEmits<{
  /** Fired when the create button is pressed. */
  create: []
}>()

const titleId = useId()
</script>

<template>
  <section class="catalog" :aria-labelledby="titleId">
    <div class="catalog__head">
      <h2 :id="titleId" class="catalog__title">{{ title }}</h2>
      <ActionButton
        :label="newLabel"
        icon="plus"
        type="primary"
        plain
        size="small"
        @click="$emit('create')"
      />
    </div>
    <slot />
  </section>
</template>

<style scoped>
.catalog {
  overflow: clip;
  background: var(--ca-surface);
  border: 1px solid var(--ca-border-soft);
  border-radius: var(--ca-radius-lg);
  box-shadow: var(--ca-shadow-sm);
}

.catalog__head {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  padding: 14px 16px;
  border-bottom: 1px solid var(--ca-border-soft);
}

.catalog__title {
  min-width: 0;
  font-family: var(--ca-font-display);
  font-size: 18px;
  font-weight: 600;
  color: var(--ca-text-bright);
}
</style>
