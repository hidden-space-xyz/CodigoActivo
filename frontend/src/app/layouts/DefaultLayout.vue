<script setup lang="ts">
import { AppBackground, AppToast } from '@/shared/ui'
import AppFooter from './AppFooter.vue'
import AppHeader from './AppHeader.vue'
</script>

<template>
  <div class="layout">
    <AppBackground />
    <AppHeader />
    <main class="layout__main">
      <slot />
    </main>
    <AppFooter />
    <AppToast position="top-right" />
  </div>
</template>

<style scoped>
.layout {
  position: relative;
  min-height: 100vh;
  display: flex;
  flex-direction: column;
  color: var(--ca-text);
}

.layout__main {
  flex: 1;
}
</style>
