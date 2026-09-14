<script setup lang="ts">
import { ResourceCard, useResources } from '@/entities/resource'
import { AppButton, PageHeading } from '@/shared/ui'

const { resources, hasMore, loadMore, isFetchingMore, isLoading } = useResources()
</script>

<template>
  <div>
    <section class="resources-head">
      <div class="ca-container">
        <PageHeading
          :title="$t('pages.resources.title')"
          :description="$t('pages.resources.intro')"
        />
      </div>
    </section>

    <section class="resources-grid-section">
      <div class="ca-container">
        <p v-if="isLoading" class="resources-loading">{{ $t('common.loading') }}</p>
        <div v-else class="resources-grid">
          <ResourceCard v-for="resource in resources" :key="resource.id" :resource="resource" />
        </div>
        <div v-if="hasMore" class="resources-more">
          <AppButton
            :label="$t('common.loadMore')"
            plain
            :loading="isFetchingMore"
            @click="loadMore"
          />
        </div>
      </div>
    </section>
  </div>
</template>

<style scoped>
.resources-head {
  padding: 48px var(--ca-gutter) 20px;
}

.resources-grid-section {
  padding: 30px var(--ca-gutter) 80px;
}

.resources-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(min(300px, 100%), 1fr));
  gap: 18px;
}

.resources-loading {
  color: var(--ca-text-dim);
  font-family: var(--ca-font-mono);
}

.resources-more {
  margin-top: 28px;
  display: flex;
  justify-content: center;
}
</style>
