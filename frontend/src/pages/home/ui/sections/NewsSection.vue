<script setup lang="ts">
import { FeaturedNewsCard, NewsCard, useHomeNews } from '@/entities/news-item'
import { BaseButton } from '@/shared/ui'

const { featured, items: recent, isLoading } = useHomeNews()
</script>

<template>
  <section v-if="isLoading || featured" class="home-section">
    <div class="ca-container">
      <div class="home-section__head">
        <h2 class="home-section__title">{{ $t('pages.home.news.title') }}</h2>
        <BaseButton variant="link" class="home-section__view-all" :to="{ name: 'news' }">
          {{ $t('pages.home.news.viewAll') }}
        </BaseButton>
      </div>

      <p v-if="isLoading" class="home-section__loading">{{ $t('common.loading') }}</p>
      <template v-else-if="featured">
        <FeaturedNewsCard :news-item="featured" />
        <div v-if="recent.length" class="home-section__grid">
          <NewsCard v-for="newsItem in recent" :key="newsItem.id" :news-item="newsItem" />
        </div>
      </template>
    </div>
  </section>
</template>

<style scoped>
.home-section {
  padding: 16px var(--ca-gutter) 8px;
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
