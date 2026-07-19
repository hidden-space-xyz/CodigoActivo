<script setup lang="ts">
import { logoMark } from '@/shared/assets'

withDefaults(defineProps<{ size?: 'sm' | 'md' }>(), { size: 'md' })
</script>

<template>
  <div class="brand" :class="`brand--${size}`">
    <img class="brand__mark" :src="logoMark" alt="" aria-hidden="true" width="30" height="30" />
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
  width: 30px;
  height: 30px;
  object-fit: contain;
}

.brand__word {
  font-family: var(--ca-font-display);
  font-weight: 700;
  font-size: 19px;
  letter-spacing: -0.01em;
  color: var(--ca-text);
}

.brand__accent {
  color: var(--ca-orange);
}

.brand--sm .brand__mark {
  width: 28px;
  height: 28px;
}

.brand--sm .brand__word {
  font-size: 18px;
}
</style>
