<script setup lang="ts">
import { computed, ref } from 'vue'

import { ResourceCard, useResources } from '@/entities/resource'
import { AppButton, PageHeading, SearchInput } from '@/shared/ui'

const search = ref('')

const { resources, hasMore, loadMore, isFetchingMore, isLoading } = useResources(() => search.value)

const hasNoResults = computed(
  () => search.value !== '' && !isLoading.value && resources.value.length === 0,
)
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
        <div class="resources-filters">
          <SearchInput
            v-model="search"
            class="resources-filters__search"
            :label="$t('common.searchByTitleOrSubtitle')"
          />
        </div>

        <p v-if="isLoading" class="resources-loading">{{ $t('common.loading') }}</p>
        <p v-else-if="hasNoResults" class="resources-loading">
          {{ $t('pages.resources.noResults') }}
        </p>
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

.resources-filters {
  margin-bottom: 26px;
  display: flex;
}

.resources-filters__search {
  flex: 0 1 340px;
  min-width: 0;
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

@media (max-width: 640px) {
  .resources-filters__search {
    flex-basis: 100%;
  }
}
</style>
