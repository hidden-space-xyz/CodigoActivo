<script setup lang="ts">
import { logoMark } from '@/shared/branding'
</script>

<template>
  <div class="brand">
    <img class="brand__mark" :src="logoMark" alt="" aria-hidden="true" width="30" height="30" />
    <div class="brand__word">
      {{ $t('common.brand.nameStart')
      }}<span class="brand__accent">{{ $t('common.brand.nameEnd') }}</span>
    </div>
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
</style>
