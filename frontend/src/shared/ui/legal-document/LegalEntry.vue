<script setup lang="ts">
import type { LegalFact } from './legal-fact'

defineProps<{
  /** Labelled values listed under the head, in order. */
  facts: readonly LegalFact[]
}>()
</script>

<template>
  <li class="legal-entry">
    <div v-if="$slots.head" class="legal-entry__head">
      <slot name="head" />
    </div>
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

.legal-entry__head {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  justify-content: space-between;
  gap: 8px 12px;
}

.legal-entry__facts {
  display: grid;
  grid-template-columns: 130px minmax(0, 1fr);
  gap: 8px 16px;
  margin: 0;
}

.legal-entry__head + .legal-entry__facts {
  margin-top: 14px;
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
