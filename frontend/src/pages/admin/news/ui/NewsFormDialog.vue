<script setup lang="ts">
import { watch } from 'vue'

import { ThumbnailField, uploadFileRequest, useThumbnailUpload } from '@/entities/file'
import type { NewsItem, NewsItemInput } from '@/entities/news-item'
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
const { draft, submitted, invalid } = form
const thumbnail = useThumbnailUpload(() => props.newsItem?.thumbnailId)
const { pickedFile, uploading, uploadError, missingThumbnail } = thumbnail

watch(visible, (open) => {
  if (!open) return
  form.reset(toNewsDraft(props.newsItem))
  thumbnail.reset()
})

async function save(): Promise<void> {
  const value = form.submit()
  if (!value || missingThumbnail.value) return
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
      <div class="form__field">
        <label for="news-title">{{ $t('pages.admin.news.form.title') }}</label>
        <el-input
          id="news-title"
          v-model="draft.title"
          :maxlength="200"
          :class="{ 'ca-invalid': invalid('title') }"
        />
      </div>
      <div class="form__field">
        <label for="news-subtitle">{{ $t('pages.admin.news.form.subtitle') }}</label>
        <el-input
          id="news-subtitle"
          v-model="draft.subtitle"
          :maxlength="300"
          :class="{ 'ca-invalid': invalid('subtitle') }"
        />
      </div>
      <div class="form__field">
        <div class="form__label">{{ $t('pages.admin.news.form.description') }}</div>
        <RichTextEditor
          v-model="draft.description"
          :label="$t('pages.admin.news.form.description')"
          :upload="uploadFileRequest"
        />
      </div>
      <div class="form__field">
        <div id="news-image-label" class="form__label">{{ $t('common.image') }}</div>
        <ThumbnailField
          role="group"
          aria-labelledby="news-image-label"
          :existing-thumbnail-id="newsItem?.thumbnailId"
          :invalid="submitted && missingThumbnail"
          @update:file="pickedFile = $event"
        />
        <small v-if="submitted && missingThumbnail" class="form__error">{{
          $t('common.imageRequired')
        }}</small>
        <small v-if="uploadError" class="form__error">{{ uploadError }}</small>
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

.form__field {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.form__field label,
.form__label {
  font-size: 13px;
  font-weight: 600;
  color: var(--ca-text-muted);
}

.form__error {
  color: var(--ca-danger-ink);
  font-size: 12.5px;
}

@media (max-width: 640px) {
  .form {
    max-height: none;
    overflow-y: visible;
  }
}
</style>
