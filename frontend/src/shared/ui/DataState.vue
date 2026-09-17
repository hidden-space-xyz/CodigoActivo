<script setup lang="ts">
import AppIcon from './AppIcon.vue'

defineProps<{
  /** Shows a spinner; takes precedence over `error` and `empty`. */
  loading: boolean
  /** Shows the error message instead of the slot; takes precedence over `empty`. */
  error: boolean
  /** Shows the empty message instead of the slot once loading succeeded. */
  empty: boolean
  /** Overrides the generic localized empty message. */
  emptyText?: string
  /** Overrides the generic localized error message. */
  errorText?: string
}>()
</script>

<template>
  <div v-if="loading" class="data-state">
    <span class="data-state__spinner"><AppIcon name="spinner" spin /></span>
    <span>{{ $t('common.loading') }}</span>
  </div>
  <div v-else-if="error" class="data-state data-state--error">
    {{ errorText ?? $t('dataState.error') }}
  </div>
  <div v-else-if="empty" class="data-state">{{ emptyText ?? $t('dataState.empty') }}</div>
  <slot v-else />
</template>

<style scoped>
.data-state {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 12px;
  padding: 48px 24px;
  color: var(--ca-text-muted);
  font-size: 15px;
}

.data-state__spinner {
  display: inline-flex;
  font-size: 36px;
}

.data-state--error {
  color: var(--ca-danger-ink);
}
</style>
