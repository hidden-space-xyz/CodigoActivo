<script setup lang="ts">
import { computed } from 'vue'

import { useAnnouncementDetail } from '@/modules/announcements/presentation/composables/useAnnouncements'
import BaseButton from '@/shared/ui/components/BaseButton.vue'
import RichTextContent from '@/shared/ui/components/RichTextContent.vue'
import { fileContentUrl } from '@/shared/utils/media'
import { isRichTextEmpty } from '@/shared/utils/richtext'

const props = defineProps<{ announcementId: string }>()

const { announcement, isLoading, notFound } = useAnnouncementDetail(() => props.announcementId)

const posterUrl = computed(() => fileContentUrl(announcement.value?.thumbnailId))
const hasDescription = computed(() => !isRichTextEmpty(announcement.value?.description))
</script>

<template>
  <div>
    <section class="detail-back">
      <div class="ca-container--narrow">
        <BaseButton variant="link" :to="{ name: 'announcements' }">
          ← Volver a anuncios
        </BaseButton>
      </div>
    </section>

    <p v-if="isLoading" class="detail-state ca-container--narrow">Cargando…</p>

    <p v-else-if="notFound || !announcement" class="detail-state ca-container--narrow">
      No hemos encontrado ese anuncio.
    </p>

    <template v-else>
      <article class="detail ca-container--narrow">
        <time v-if="announcement.date" class="detail__date">{{ announcement.date }}</time>
        <h1 class="detail__title">{{ announcement.title }}</h1>
        <p v-if="announcement.subtitle" class="detail__subtitle">{{ announcement.subtitle }}</p>

        <img v-if="posterUrl" :src="posterUrl" :alt="announcement.title" class="detail__poster" />

        <RichTextContent v-if="hasDescription" :content="announcement.description" class="detail__body" />
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

.detail__date {
  font-family: var(--ca-font-mono);
  font-size: 13px;
  color: var(--ca-text-faint-2);
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
  color: var(--ca-cyan);
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
  color: #b6bdca;
  white-space: pre-line;
}
</style>
