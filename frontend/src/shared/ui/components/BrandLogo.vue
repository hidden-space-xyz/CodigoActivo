<script setup lang="ts">
withDefaults(defineProps<{ size?: 'sm' | 'md' }>(), { size: 'md' })
</script>

<template>
  <div class="brand" :class="`brand--${size}`">
    <div class="brand__mark" aria-hidden="true">
      <span class="brand__cell" style="background: var(--ca-coral)" />
      <span class="brand__cell" style="background: var(--ca-amber)" />
      <span class="brand__cell" style="background: var(--ca-purple)" />
      <span class="brand__cell" style="background: var(--ca-cyan)" />
    </div>
    <div class="brand__word">Código<span class="brand__accent">Activo</span></div>
  </div>
</template>

<style scoped>
.brand {
  display: flex;
  align-items: center;
  gap: 11px;
}

.brand__mark {
  display: grid;
  grid-template-columns: repeat(2, 1fr);
  gap: 3px;
  width: 30px;
  height: 30px;
  border-radius: 8px;
  overflow: hidden;
}

.brand__cell {
  display: block;
}

.brand__word {
  font-family: var(--ca-font-display);
  font-weight: 700;
  font-size: 19px;
  letter-spacing: -0.01em;
  color: var(--ca-text);
}

.brand__accent {
  color: var(--ca-cyan);
}

.brand--sm .brand__mark {
  width: 28px;
  height: 28px;
  border-radius: 7px;
}

.brand--sm .brand__word {
  font-size: 18px;
}
</style>
