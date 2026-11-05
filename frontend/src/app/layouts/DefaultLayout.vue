<script setup lang="ts">
import { useRoute } from 'vue-router'

import { SiteFooter } from '@/widgets/site-footer'
import { SiteHeader } from '@/widgets/site-header'
import { AppBackground } from '@/shared/ui/app-background'

const route = useRoute()
</script>

<template>
  <div class="layout">
    <AppBackground />
    <SiteHeader />
    <main class="layout__main" :data-testid="`page-${String(route.name)}`">
      <slot />
    </main>
    <SiteFooter />
  </div>
</template>

<style scoped>
.layout {
  position: relative;
  min-height: 100vh;
  min-height: 100dvh;
  display: flex;
  flex-direction: column;
  color: var(--ca-text);
}

.layout__main {
  flex: 1;
}
</style>
