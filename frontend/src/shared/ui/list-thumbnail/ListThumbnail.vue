<script setup lang="ts">
import { computed, ref, watch } from 'vue'

import { AppIcon } from '@/shared/ui/app-icon'
import { fileContentUrl } from '@/shared/api'

const props = withDefaults(
  defineProps<{
    /** Stored file id; without one, or when it fails to load, a placeholder icon fills the frame. */
    thumbnailId?: string | null
    /** Image alt text; defaults to empty, marking the image as decorative. */
    alt?: string
    /** `card` fills its container; `table` is a fixed 56px frame for table rows. */
    size?: 'card' | 'table'
    /** `contain` shows the whole image, e.g. logos; `cover` crops it to the 16:9 frame. */
    fit?: 'cover' | 'contain'
  }>(),
  { thumbnailId: null, alt: '', size: 'card', fit: 'cover' },
)

const failed = ref(false)

const url = computed(() => fileContentUrl(props.thumbnailId))

watch(
  () => props.thumbnailId,
  () => {
    failed.value = false
  },
)
</script>

<template>
  <div
    class="list-thumb"
    :class="{ 'list-thumb--table': size === 'table', 'list-thumb--contain': fit === 'contain' }"
  >
    <img
      v-if="url && !failed"
      :src="url"
      :alt="alt"
      class="list-thumb__img"
      loading="lazy"
      decoding="async"
      @error="failed = true"
    />
    <div v-else class="list-thumb__placeholder" aria-hidden="true">
      <AppIcon name="image" />
    </div>
  </div>
</template>

<style scoped>
.list-thumb {
  aspect-ratio: 16 / 9;
  width: 100%;
  overflow: hidden;
  border-radius: 12px;
  background: var(--ca-bg-deep);
  border: 1px solid var(--ca-border);
}

.list-thumb__img {
  width: 100%;
  height: 100%;
  object-fit: cover;
  display: block;
}

.list-thumb__placeholder {
  width: 100%;
  height: 100%;
  display: flex;
  align-items: center;
  justify-content: center;
  color: var(--ca-text-faint);
  font-size: 22px;
}

.list-thumb--table {
  flex: none;
  width: 56px;
  border-radius: var(--ca-radius-sm);
  border-color: var(--ca-border-soft);
  background: var(--ca-surface-2);
}

.list-thumb--table .list-thumb__placeholder {
  font-size: 16px;
}

.list-thumb--contain .list-thumb__img {
  padding: 2px;
  object-fit: contain;
  background: var(--ca-surface);
}
</style>
