<script setup lang="ts">
withDefaults(
  defineProps<{
    /** Filled share from 0 to 100; `null` draws an empty track. */
    percent: number | null
    /** Thinner track for nested rows. */
    small?: boolean
  }>(),
  { small: false },
)
</script>

<template>
  <span class="seat-meter" :class="{ 'seat-meter--sm': small }" aria-hidden="true">
    <span class="seat-meter__fill" :style="{ width: `${percent ?? 0}%` }" />
  </span>
</template>

<style scoped>
.seat-meter {
  display: block;
  height: 8px;
  border-radius: 999px;
  background: var(--ca-surface-2);
  overflow: hidden;
}

.seat-meter--sm {
  height: 6px;
}

.seat-meter__fill {
  display: block;
  height: 100%;
  border-radius: 999px;
  background: var(--ca-lime);
  transition: width 0.3s ease;
}
</style>
