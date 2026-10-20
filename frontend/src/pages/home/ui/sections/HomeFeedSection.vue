<script setup lang="ts">
import type { RouteLocationRaw } from 'vue-router'

import { BrandButton } from '@/shared/ui/brand-button'

defineProps<{
  /** Heading of the home section. */
  title: string
  /** Text of the link that opens the whole list this section previews. */
  viewAllLabel: string
  /** Route of the whole list this section previews. */
  viewAllTo: RouteLocationRaw
  /** Shows the loading text in place of the featured item and the grid. */
  loading: boolean
  /** Shortens the top padding, as the section right below the hero needs. */
  compact?: boolean
}>()
</script>

<template>
  <section class="home-section" :class="{ 'home-section--compact': compact }">
    <div class="ca-container">
      <div class="home-section__head">
        <h2 class="home-section__title">{{ title }}</h2>
        <BrandButton variant="link" class="home-section__view-all" :to="viewAllTo">
          {{ viewAllLabel }}
        </BrandButton>
      </div>

      <p v-if="loading" class="home-section__loading">{{ $t('common.loading') }}</p>
      <template v-else>
        <slot name="featured" />
        <div v-if="$slots.default" class="home-section__grid">
          <slot />
        </div>
      </template>
    </div>
  </section>
</template>

<style scoped>
.home-section {
  padding: 28px var(--ca-gutter) 8px;
}

.home-section--compact {
  padding-top: 16px;
}

.home-section__head {
  display: flex;
  align-items: flex-end;
  justify-content: space-between;
  gap: 20px;
  margin-bottom: 26px;
  flex-wrap: wrap;
}

.home-section__title {
  font-family: var(--ca-font-display);
  font-size: 38px;
  font-weight: 700;
  color: var(--ca-text-bright);
  letter-spacing: -0.02em;
}

.base-button--link.home-section__view-all {
  color: var(--ca-orange);
}

.base-button--link.home-section__view-all:hover {
  color: var(--ca-orange-strong);
}

.home-section__grid {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 18px;
  margin-top: 18px;
}

.home-section__loading {
  color: var(--ca-text-dim);
  font-family: var(--ca-font-mono);
}

@media (max-width: 1024px) {
  .home-section__grid {
    grid-template-columns: repeat(2, 1fr);
  }
}

@media (max-width: 640px) {
  .home-section__grid {
    grid-template-columns: 1fr;
  }
}
</style>
