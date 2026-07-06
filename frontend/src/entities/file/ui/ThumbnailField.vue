<script setup lang="ts">
import { onBeforeUnmount, ref, watch } from 'vue'
import { AppButton as Button } from '@/shared/ui'
import { fileContentUrl } from '@/shared/lib'

import { getThumbnailNameRequest } from '../api/requests'

const props = defineProps<{ existingThumbnailId?: string | null | undefined; invalid?: boolean }>()

const emit = defineEmits<{ 'update:file': [file: File | null] }>()

const fileInput = ref<HTMLInputElement | null>(null)
const previewUrl = ref<string | null>(null)
const fileName = ref<string>('')
const pickedFile = ref<File | null>(null)
const sizeError = ref('')
// Mirrors the backend upload cap (FileStorageOptions.DefaultMaxSizeBytes = 10 MiB).
const MAX_SIZE = 10 * 1024 * 1024
let objectUrl: string | null = null

function revokeObjectUrl(): void {
  if (objectUrl) {
    URL.revokeObjectURL(objectUrl)
    objectUrl = null
  }
}

function showExisting(id: string): void {
  revokeObjectUrl()
  // The browser loads (and caches) the image itself — only the display name needs a request.
  previewUrl.value = fileContentUrl(id)
  fileName.value = ''
  void getThumbnailNameRequest(id)
    .then((name) => {
      fileName.value = name
    })
    .catch(() => undefined)
}

watch(
  () => props.existingThumbnailId,
  (id) => {
    pickedFile.value = null
    if (id) showExisting(id)
    else {
      revokeObjectUrl()
      previewUrl.value = null
      fileName.value = ''
    }
  },
  { immediate: true },
)

function pick(): void {
  fileInput.value?.click()
}

function onChange(event: Event): void {
  const input = event.target as HTMLInputElement
  const file = input.files?.[0] ?? null
  input.value = ''
  if (!file) return
  if (file.size > MAX_SIZE) {
    sizeError.value = 'La imagen supera el tamaño máximo de 10 MB.'
    return
  }
  sizeError.value = ''
  pickedFile.value = file
  fileName.value = file.name
  revokeObjectUrl()
  objectUrl = URL.createObjectURL(file)
  previewUrl.value = objectUrl
  emit('update:file', file)
}

function clearSelection(): void {
  pickedFile.value = null
  emit('update:file', null)
  if (props.existingThumbnailId) showExisting(props.existingThumbnailId)
  else {
    revokeObjectUrl()
    previewUrl.value = null
    fileName.value = ''
  }
}

onBeforeUnmount(revokeObjectUrl)
</script>

<template>
  <div class="thumb">
    <div class="thumb__preview" :class="{ 'thumb__preview--invalid': invalid }">
      <img v-if="previewUrl" :src="previewUrl" alt="Miniatura" class="thumb__img" />
      <span v-else class="thumb__placeholder">Sin imagen</span>
    </div>

    <div class="thumb__controls">
      <Button
        type="button"
        :label="previewUrl ? 'Cambiar imagen' : 'Seleccionar imagen'"
        icon="pi pi-image"
        size="small"
        severity="secondary"
        @click="pick"
      />
      <Button
        v-if="pickedFile"
        type="button"
        label="Quitar"
        text
        size="small"
        severity="secondary"
        @click="clearSelection"
      />
      <span v-if="fileName" class="thumb__name">{{ fileName }}</span>
    </div>

    <small v-if="sizeError" class="thumb__error">{{ sizeError }}</small>

    <input ref="fileInput" type="file" accept="image/*" class="thumb__input" @change="onChange" />
  </div>
</template>

<style scoped>
.thumb {
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.thumb__preview {
  width: 100%;
  height: 150px;
  border: 1px dashed var(--ca-border-strong);
  border-radius: 12px;
  display: flex;
  align-items: center;
  justify-content: center;
  overflow: hidden;
  background: var(--ca-input-bg);
}

.thumb__preview--invalid {
  border-color: var(--ca-danger);
}

.thumb__img {
  max-width: 100%;
  max-height: 100%;
  object-fit: contain;
}

.thumb__placeholder {
  color: var(--ca-text-ghost);
  font-size: 13px;
}

.thumb__controls {
  display: flex;
  align-items: center;
  gap: 10px;
  flex-wrap: wrap;
}

.thumb__name {
  font-size: 12.5px;
  color: var(--ca-text-muted);
  word-break: break-all;
}

.thumb__error {
  font-size: 12.5px;
  color: var(--ca-danger-ink);
}

.thumb__input {
  display: none;
}
</style>
