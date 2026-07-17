<script setup lang="ts">
import ProgressSpinner from 'primevue/progressspinner'

defineProps<{
  loading: boolean
  error: boolean
  empty: boolean
  emptyText?: string
  errorText?: string
}>()
</script>

<template>
  <div v-if="loading" class="data-state">
    <ProgressSpinner style="width: 36px; height: 36px" stroke-width="4" />
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

.data-state--error {
  color: var(--ca-danger-ink);
}
</style>
