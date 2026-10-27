<script setup lang="ts">
import type { LegalFact } from './legal-fact'

defineProps<{
  /** Labelled values, in order; each `label` must be unique because it is the list key. */
  facts: readonly LegalFact[]
}>()
</script>

<template>
  <li class="legal-entry">
    <dl class="legal-entry__facts">
      <template v-for="fact in facts" :key="fact.label">
        <dt class="legal-entry__label">{{ fact.label }}</dt>
        <dd class="legal-entry__value">{{ fact.value }}</dd>
      </template>
    </dl>
  </li>
</template>

<style scoped>
.legal-entry {
  background: var(--ca-bg-elevated);
  border: 1px solid var(--ca-border-strong);
  border-radius: 16px;
  padding: 18px 20px;
}

.legal-entry__facts {
  display: grid;
  grid-template-columns: 130px minmax(0, 1fr);
  gap: 8px 16px;
  margin: 0;
}

.legal-entry__label {
  font-size: 13px;
  font-weight: 600;
  line-height: 1.7;
  color: var(--ca-text-dim);
}

.legal-entry__value {
  margin: 0;
  font-size: 15px;
  line-height: 1.6;
  color: var(--ca-text);
}

@media (max-width: 640px) {
  .legal-entry {
    padding-inline: 16px;
  }

  .legal-entry__facts {
    grid-template-columns: minmax(0, 1fr);
    gap: 2px;
  }

  .legal-entry__value + .legal-entry__label {
    margin-top: 10px;
  }
}
</style>
