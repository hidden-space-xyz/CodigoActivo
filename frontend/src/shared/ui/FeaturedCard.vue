<script setup lang="ts">
import { computed } from 'vue'
import type { RouteLocationRaw } from 'vue-router'

import { fileContentUrl } from '@/shared/lib'

import BaseButton from './BaseButton.vue'
import ColorTag from './ColorTag.vue'

interface FeaturedTag {
  readonly id: string
  readonly name: string
  readonly color: string
}

interface FeaturedMetaItem {
  readonly label: string
  readonly value: string
}

const props = defineProps<{
  badge: string
  title: string
  subtitle: string
  thumbnailId: string
  to: RouteLocationRaw
  ctaLabel: string
  tags: readonly FeaturedTag[]
  meta: readonly FeaturedMetaItem[]
}>()

const posterUrl = computed(() => fileContentUrl(props.thumbnailId))
</script>

<template>
  <div class="featured">
    <div class="featured__grid">
      <div class="featured__body">
        <span class="featured__badge">{{ badge }}</span>

        <h2 class="featured__title">{{ title }}</h2>
        <div v-if="subtitle" class="featured__slogan">{{ subtitle }}</div>

        <div v-if="tags.length" class="featured__cats">
          <ColorTag v-for="tag in tags" :key="tag.id" :value="tag.name" :color="tag.color" />
        </div>

        <div v-if="meta.length" class="featured__meta">
          <div v-for="item in meta" :key="item.label" class="featured__meta-item">
            <span class="featured__meta-label">{{ item.label }}</span>
            <span class="featured__meta-value">{{ item.value }}</span>
          </div>
        </div>

        <BaseButton :to="to" variant="light" class="featured__cta">{{ ctaLabel }}</BaseButton>
      </div>

      <div class="featured__poster">
        <img v-if="posterUrl" :src="posterUrl" :alt="title" class="featured__poster-img" />
      </div>
    </div>
  </div>
</template>

<style scoped>
.featured {
  position: relative;
  overflow: hidden;
  border-radius: 20px;
  border: 1px solid var(--ca-orange);
  background: var(--ca-orange-soft);
}

.featured__grid {
  display: grid;
  grid-template-columns: 1.15fr 0.85fr;
  align-items: stretch;
}

.featured__body {
  padding: 40px 44px;
}

.featured__badge {
  display: inline-block;
  font-family: var(--ca-font-mono);
  font-size: 11px;
  font-weight: 600;
  letter-spacing: 0.09em;
  text-transform: uppercase;
  color: var(--ca-orange-ink);
  background: var(--ca-surface-2);
  padding: 5px 11px;
  border-radius: 999px;
  margin-bottom: 18px;
}

.featured__title {
  font-family: var(--ca-font-display);
  font-weight: 700;
  font-size: clamp(24px, 5.2vw, 40px);
  line-height: 1.05;
  letter-spacing: -0.02em;
  color: var(--ca-text-bright);
}

.featured__slogan {
  font-family: var(--ca-font-display);
  font-size: 21px;
  margin-top: 8px;
  color: var(--ca-text-muted);
}

.featured__cats {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
  margin-top: 16px;
}

.featured__meta {
  display: flex;
  gap: 22px;
  margin-top: 24px;
  flex-wrap: wrap;
}

.featured__meta-item {
  display: flex;
  flex-direction: column;
}

.featured__meta-label {
  font-size: 12px;
  color: var(--ca-text-dim);
}

.featured__meta-value {
  font-weight: 600;
  color: var(--ca-text);
}

.featured__cta {
  margin-top: 28px;
  font-size: 16px;
  padding: 13px 24px;
  border-radius: 11px;
}

.featured__poster {
  position: relative;
  min-height: 340px;
  background: var(--ca-bg-deep);
  border-left: 1px solid var(--ca-border);
}

.featured__poster-img {
  position: absolute;
  inset: 0;
  width: 100%;
  height: 100%;
  object-fit: cover;
  display: block;
}

@media (max-width: 1024px) {
  .featured__grid {
    grid-template-columns: 1fr;
  }
  .featured__body {
    padding: 32px 28px;
  }
  .featured__poster {
    min-height: 220px;
    border-left: none;
    border-top: 1px solid var(--ca-border);
  }
}
</style>
