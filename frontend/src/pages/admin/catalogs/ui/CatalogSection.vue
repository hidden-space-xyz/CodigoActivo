<script setup lang="ts">
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
</script>

<template>
  <section class="catalog">
    <div class="catalog__head">
      <h2 class="catalog__title">{{ title }}</h2>
      <ActionButton
        :label="newLabel"
        icon="plus"
        type="primary"
        size="small"
        @click="$emit('create')"
      />
    </div>
    <slot />
  </section>
</template>

<style scoped>
.catalog {
  background: var(--ca-surface);
  border: 1px solid var(--ca-border-soft);
  border-radius: 14px;
  padding: 18px 20px;
}

.catalog__head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 14px;
}

.catalog__title {
  font-family: var(--ca-font-display);
  font-size: 18px;
  font-weight: 600;
  color: var(--ca-text-bright);
}
</style>
