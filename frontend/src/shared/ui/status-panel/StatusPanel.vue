<script setup lang="ts">
import { AppIcon } from '@/shared/ui/app-icon'

defineProps<{
  /** Outcome the panel reports: work still running, done, or failed. */
  state: 'pending' | 'success' | 'error'
  /** Heading above the text. */
  title?: string
  /** Main message; announced politely while pending and as an alert on error. */
  text: string
  /** Secondary advice under the message. */
  hint?: string
}>()
</script>

<template>
  <div class="status-panel" :class="`status-panel--${state}`">
    <span v-if="state === 'pending'" class="status-panel__icon" aria-hidden="true">
      <AppIcon name="spinner" spin />
    </span>
    <div
      v-else
      class="status-panel__icon"
      :class="`status-panel__icon--${state}`"
      aria-hidden="true"
    >
      {{ state === 'success' ? '✓' : '!' }}
    </div>
    <h2 v-if="title" class="status-panel__title">{{ title }}</h2>
    <p
      class="status-panel__text"
      :role="state === 'error' ? 'alert' : undefined"
      :aria-live="state === 'pending' ? 'polite' : undefined"
    >
      {{ text }}
    </p>
    <p v-if="hint" class="status-panel__hint">{{ hint }}</p>
    <slot />
    <div v-if="$slots.actions" class="status-panel__actions">
      <slot name="actions" />
    </div>
  </div>
</template>

<style scoped>
.status-panel {
  max-width: 540px;
  margin: 0 auto;
  text-align: center;
  background: var(--ca-bg-elevated);
  border: 1px solid var(--ca-border-strong);
  border-radius: 20px;
  padding: 44px 36px;
}

.status-panel__icon {
  width: 64px;
  height: 64px;
  border-radius: 50%;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 32px;
  margin: 0 auto 20px;
}

.status-panel__icon--success {
  background: var(--ca-success-soft);
  border: 1px solid var(--ca-success);
  color: var(--ca-success);
}

.status-panel__icon--error {
  background: var(--ca-danger-soft);
  border: 1px solid var(--ca-danger);
  color: var(--ca-danger);
  font-weight: 700;
}

.status-panel__title {
  font-family: var(--ca-font-display);
  font-weight: 700;
  font-size: 24px;
  color: var(--ca-text-bright);
}

.status-panel__text {
  margin-top: 10px;
  font-size: 15.5px;
  line-height: 1.6;
  color: var(--ca-text-muted);
}

.status-panel__hint {
  margin-top: 12px;
  font-size: 14px;
  line-height: 1.6;
  color: var(--ca-text-muted);
}

.status-panel__actions {
  display: flex;
  gap: 14px;
  justify-content: center;
  margin-top: 24px;
  flex-wrap: wrap;
}
</style>
