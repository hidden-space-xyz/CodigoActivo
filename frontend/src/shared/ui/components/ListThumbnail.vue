<script setup lang="ts">
import { computed } from 'vue'

import { fileContentUrl } from '@/shared/utils/media'

const props = defineProps<{ thumbnailId?: string | null; alt?: string }>()

const url = computed(() => fileContentUrl(props.thumbnailId))
</script>

<template>
  <div class="list-thumb">
    <img v-if="url" :src="url" :alt="alt ?? ''" class="list-thumb__img" loading="lazy" />
    <div v-else class="list-thumb__placeholder" aria-hidden="true">
      <i class="pi pi-image" />
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
  color: var(--ca-text-ghost);
  font-size: 22px;
}
</style>
