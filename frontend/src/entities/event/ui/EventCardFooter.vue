<script setup lang="ts">
import type { EventStatus } from '../model/types'

defineProps<{ status: EventStatus }>()
</script>

<template>
  <div class="event-card-footer">
    <span class="event-card-footer__status" :class="`event-card-footer__status--${status.kind}`">
      {{ status.label }}
    </span>
  </div>
</template>

<style scoped>
.event-card-footer {
  display: flex;
  align-items: center;
  margin-top: auto;
  padding-top: 14px;
  border-top: 1px solid var(--ca-border);
}

.event-card-footer__status {
  font-family: var(--ca-font-mono);
  font-size: 12px;
  font-weight: 600;
}

.event-card-footer__status--earlySignupOpen {
  color: var(--ca-orange);
}

.event-card-footer__status--signupOpen {
  color: var(--ca-success-ink);
}

.event-card-footer__status--signupClosed {
  color: var(--ca-warning-ink);
}

.event-card-footer__status--upcoming {
  color: var(--ca-info-ink);
}

.event-card-footer__status--finished {
  color: var(--ca-text-dim);
}
</style>
