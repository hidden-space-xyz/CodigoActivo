<script setup lang="ts">
import { watch } from 'vue'

import { ThumbnailField, uploadFileRequest, useThumbnailUpload } from '@/entities/file'
import type { NewsItem, NewsItemInput } from '@/entities/news-item'
import { SUBTITLE_MAX_LENGTH, TITLE_MAX_LENGTH } from '@/shared/config'
import { useForm } from '@/shared/lib/form'
import { ActionButton } from '@/shared/ui/action-button'
import { RichTextEditor } from '@/shared/ui/rich-text-editor'

import { readNewsDraft, toNewsDraft } from '../model/news-form'

/** Opens the dialog; each time it opens the form is refilled from `newsItem`. */
const visible = defineModel<boolean>('visible', { required: true })

const props = defineProps<{
  /** News item being edited; `null` opens the dialog in create mode. */
  newsItem: NewsItem | null
  /** Shows a loading state on the save button while the parent persists the news item. */
  saving: boolean
}>()

const emit = defineEmits<{
  /** Fired once the form reads valid and the thumbnail is uploaded. */
  submit: [input: NewsItemInput]
}>()

const form = useForm({ initial: () => toNewsDraft(null), read: readNewsDraft })
const { draft, submitted, errors, invalid } = form
const thumbnail = useThumbnailUpload(() => props.newsItem?.thumbnailId)
const { pickedFile, uploading, uploadError, missingThumbnail } = thumbnail

watch(visible, (open) => {
  if (!open) return
  form.reset(toNewsDraft(props.newsItem))
  thumbnail.reset()
})

async function save(): Promise<void> {
  const value = form.submit()
  if (!value) return
  const thumbnailId = await thumbnail.resolveThumbnailId()
  if (!thumbnailId) return
  emit('submit', { ...value, thumbnailId })
}
</script>

<template>
  <el-dialog
    v-model="visible"
    :title="newsItem ? $t('pages.admin.news.form.editHeader') : $t('pages.admin.news.newLabel')"
    width="min(920px, 94vw)"
  >
    <form class="form" @submit.prevent="save">
      <div class="ca-field">
        <label class="ca-field__label" for="news-title">{{
          $t('pages.admin.news.form.title')
        }}</label>
        <el-input
          id="news-title"
          v-model="draft.title"
          :maxlength="TITLE_MAX_LENGTH"
          :class="{ 'ca-invalid': invalid('title') }"
          :aria-invalid="invalid('title')"
        />
      </div>
      <div class="ca-field">
        <label class="ca-field__label" for="news-subtitle">{{
          $t('pages.admin.news.form.subtitle')
        }}</label>
        <el-input
          id="news-subtitle"
          v-model="draft.subtitle"
          :maxlength="SUBTITLE_MAX_LENGTH"
          :class="{ 'ca-invalid': invalid('subtitle') }"
          :aria-invalid="invalid('subtitle')"
        />
      </div>
      <div class="ca-field">
        <div class="ca-field__label">{{ $t('pages.admin.news.form.description') }}</div>
        <RichTextEditor
          v-model="draft.description"
          :label="$t('pages.admin.news.form.description')"
          :upload="uploadFileRequest"
          :invalid="!!errors.description"
        />
        <small v-if="errors.description" class="ca-field__error">{{ errors.description }}</small>
      </div>
      <div class="ca-field">
        <div id="news-image-label" class="ca-field__label">{{ $t('common.image') }}</div>
        <ThumbnailField
          role="group"
          aria-labelledby="news-image-label"
          :existing-thumbnail-id="newsItem?.thumbnailId"
          :invalid="submitted && missingThumbnail"
          :upload-error="uploadError"
          @update:file="pickedFile = $event"
        />
      </div>
    </form>

    <template #footer>
      <ActionButton
        :label="$t('common.cancel')"
        text
        :disabled="saving || uploading"
        @click="visible = false"
      />
      <ActionButton
        :label="$t('common.save')"
        type="primary"
        :loading="saving || uploading"
        @click="save"
      />
    </template>
  </el-dialog>
</template>

<style scoped>
.form {
  display: flex;
  flex-direction: column;
  gap: 16px;
  padding-top: 6px;
  max-height: 78vh;
  overflow-y: auto;
}

@media (max-width: 640px) {
  .form {
    max-height: none;
    overflow-y: visible;
  }
}
</style>
