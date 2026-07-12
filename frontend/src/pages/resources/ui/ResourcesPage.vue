<script setup lang="ts">
import { ResourceCard, useResources } from '@/entities/resource'
import { AppButton, SectionEyebrow } from '@/shared/ui'

const { resources, hasMore, loadMore, isFetchingMore, isLoading } = useResources()
</script>

<template>
  <div>
    <section class="resources-head">
      <div class="ca-container">
        <SectionEyebrow text="// recursos" color="var(--ca-lime-ink)" />
        <h1 class="resources-head__title">Aprende por tu cuenta</h1>
        <p class="resources-head__intro">Material de nuestros talleres, abierto y gratuito.</p>
      </div>
    </section>

    <section class="resources-grid-section">
      <div class="ca-container">
        <p v-if="isLoading" class="resources-loading">Cargando…</p>
        <div v-else class="resources-grid">
          <ResourceCard v-for="resource in resources" :key="resource.id" :resource="resource" />
        </div>
        <div v-if="hasMore" class="resources-more">
          <AppButton label="Cargar más" outlined :loading="isFetchingMore" @click="loadMore" />
        </div>
      </div>
    </section>
  </div>
</template>

<style scoped>
.resources-head {
  padding: 64px 24px 24px;
}

.resources-head__title {
  font-family: var(--ca-font-display);
  font-weight: 700;
  font-size: 46px;
  letter-spacing: -0.03em;
  color: var(--ca-text-bright);
}

.resources-head__intro {
  margin-top: 14px;
  font-size: 17px;
  line-height: 1.6;
  color: var(--ca-text-muted);
  max-width: 600px;
}

.resources-grid-section {
  padding: 30px 24px 80px;
}

.resources-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(300px, 1fr));
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
