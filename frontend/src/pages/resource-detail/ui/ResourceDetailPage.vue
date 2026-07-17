<script setup lang="ts">
import { computed, watchEffect } from 'vue'
import { useI18n } from 'vue-i18n'

import { useResourceDetail } from '@/entities/resource'
import { BaseButton, RichTextContent } from '@/shared/ui'
import {
  fileContentUrl,
  isRichTextEmpty,
  richTextExcerpt,
  useSeo,
  type SeoData,
} from '@/shared/lib'

const props = defineProps<{ resourceId: string }>()

const { t } = useI18n()

const { resource, isLoading, notFound } = useResourceDetail(() => props.resourceId)

const posterUrl = computed(() => fileContentUrl(resource.value?.thumbnailId))
const hasDescription = computed(() => !isRichTextEmpty(resource.value?.description))

const seo = computed<SeoData | undefined>(() => {
  if (notFound.value) return { title: t('pages.resourceDetail.seoNotFound'), noindex: true }
  const current = resource.value
  if (!current) return undefined
  if (current.url) return { title: current.title, noindex: true }
  const description = richTextExcerpt(current.description) || current.subtitle
  return {
    title: current.title,
    description: description || undefined,
    image: posterUrl.value || undefined,
    type: 'article',
  }
})

useSeo(seo)

watchEffect(() => {
  const url = resource.value?.url
  if (url) window.location.replace(url)
})
</script>

<template>
  <div>
    <section class="detail-back">
      <div class="ca-container--narrow">
        <BaseButton variant="link" :to="{ name: 'resources' }">
          {{ $t('pages.resourceDetail.back') }}
        </BaseButton>
      </div>
    </section>

    <p v-if="isLoading" class="detail-state ca-container--narrow">{{ $t('common.loading') }}</p>

    <p v-else-if="notFound || !resource" class="detail-state ca-container--narrow">
      {{ $t('pages.resourceDetail.notFound') }}
    </p>

    <p v-else-if="resource.url" class="detail-state ca-container--narrow">
      {{ $t('pages.resourceDetail.redirecting') }}
    </p>

    <template v-else>
      <article class="detail ca-container--narrow">
        <h1 class="detail__title">{{ resource.title }}</h1>
        <p v-if="resource.subtitle" class="detail__subtitle">{{ resource.subtitle }}</p>

        <img v-if="posterUrl" :src="posterUrl" :alt="resource.title" class="detail__poster" />

        <RichTextContent
          v-if="hasDescription"
          :content="resource.description"
          class="detail__body"
        />
        <p v-else class="detail__body detail__body--muted">
          {{ $t('pages.resourceDetail.noDescription') }}
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

.detail__title {
  font-family: var(--ca-font-display);
  font-weight: 700;
  font-size: 42px;
  line-height: 1.08;
  letter-spacing: -0.03em;
  color: var(--ca-text-bright);
  margin-top: 10px;
}

.detail__subtitle {
  font-family: var(--ca-font-display);
  font-size: 21px;
  margin-top: 10px;
  color: var(--ca-text-muted);
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
