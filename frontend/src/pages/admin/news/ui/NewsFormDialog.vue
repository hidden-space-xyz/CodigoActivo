<script setup lang="ts">
import { watch } from 'vue'

import { ThumbnailField, uploadFileRequest, useThumbnailUpload } from '@/entities/file'
import type { NewsItem, NewsItemInput } from '@/entities/news-item'
import { SUBTITLE_MAX_LENGTH, TITLE_MAX_LENGTH } from '@/shared/config'
import { useForm } from '@/shared/lib/form'
import { FormDialog } from '@/shared/ui/form-dialog'
import { FormField } from '@/shared/ui/form-field'
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
  <FormDialog
    v-model:visible="visible"
    data-testid="news-form-dialog"
    :title="newsItem ? $t('pages.admin.news.form.editHeader') : $t('pages.admin.news.newLabel')"
    width="min(920px, 94vw)"
    :saving="saving || uploading"
    scroll-height="78vh"
    @submit="save"
  >
    <FormField
      v-slot="{ control }"
      for="news-title"
      :label="$t('common.title')"
      :invalid="invalid('title')"
    >
      <el-input
        v-model="draft.title"
        v-bind="control"
        data-testid="news-form-title"
        :maxlength="TITLE_MAX_LENGTH"
      />
    </FormField>
    <FormField
      v-slot="{ control }"
      for="news-subtitle"
      :label="$t('common.subtitle')"
      :invalid="invalid('subtitle')"
    >
      <el-input
        v-model="draft.subtitle"
        v-bind="control"
        data-testid="news-form-subtitle"
        :maxlength="SUBTITLE_MAX_LENGTH"
      />
    </FormField>
    <FormField
      v-slot="{ invalid: refused }"
      :label="$t('common.description')"
      :error="errors.description"
    >
      <RichTextEditor
        v-model="draft.description"
        data-testid="news-form-description"
        :label="$t('common.description')"
        :upload="uploadFileRequest"
        :invalid="refused"
      />
    </FormField>
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
  </FormDialog>
</template>
