<script setup lang="ts">
import { computed } from 'vue'

import { useResourceDetail } from '@/entities/resource'
import { BaseButton, RichTextContent } from '@/shared/ui'
import { fileContentUrl, isRichTextEmpty } from '@/shared/lib'

const props = defineProps<{ resourceId: string }>()

const { resource, isLoading, notFound } = useResourceDetail(() => props.resourceId)

const posterUrl = computed(() => fileContentUrl(resource.value?.thumbnailId))
const hasDescription = computed(() => !isRichTextEmpty(resource.value?.description))
</script>

<template>
  <div>
    <section class="detail-back">
      <div class="ca-container--narrow">
        <BaseButton variant="link" :to="{ name: 'resources' }"> ← Volver a recursos </BaseButton>
      </div>
    </section>

    <p v-if="isLoading" class="detail-state ca-container--narrow">Cargando…</p>

    <p v-else-if="notFound || !resource" class="detail-state ca-container--narrow">
      No hemos encontrado ese recurso.
    </p>

    <template v-else>
      <article class="detail ca-container--narrow">
        <span v-if="resource.type" class="detail__type">{{ resource.type }}</span>
        <h1 class="detail__title">{{ resource.title }}</h1>

        <img v-if="posterUrl" :src="posterUrl" :alt="resource.title" class="detail__poster" />

        <RichTextContent
          v-if="hasDescription"
          :content="resource.description"
          class="detail__body"
        />
        <p v-else class="detail__body detail__body--muted">
          Este recurso todavía no tiene una descripción.
        </p>
      </article>
    </template>
  </div>
</template>

<style scoped>
.detail-back {
  padding: 36px 24px 0;
}

.detail-state {
  padding: 40px 24px;
  color: var(--ca-text-dim);
  font-family: var(--ca-font-mono);
}

.detail {
  padding: 24px 24px 80px;
}

.detail__type {
  font-family: var(--ca-font-mono);
  font-size: 12px;
  color: var(--ca-text-faint-2);
  text-transform: uppercase;
  letter-spacing: 0.06em;
}

.detail__title {
  font-family: var(--ca-font-display);
  font-weight: 700;
  font-size: 42px;
  line-height: 1.08;
  letter-spacing: -0.03em;
  color: var(--ca-text-bright);
  margin-top: 10px;
}

.detail__poster {
  width: 100%;
  height: auto;
  border-radius: 18px;
  border: 1px solid var(--ca-border);
  margin: 28px 0;
  display: block;
}

.detail__body {
  margin-top: 20px;
  font-size: 16.5px;
  line-height: 1.7;
  color: var(--ca-text-muted);
  white-space: pre-line;
}

.detail__body--muted {
  color: var(--ca-text-dim);
  font-style: italic;
}
</style>
