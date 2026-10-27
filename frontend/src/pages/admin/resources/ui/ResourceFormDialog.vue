<script setup lang="ts">
import { computed, watch } from 'vue'
import { useQuery } from '@tanstack/vue-query'

import { ThumbnailField, uploadFileRequest, useThumbnailUpload } from '@/entities/file'
import { resourceQueries, type LearningResource, type ResourceInput } from '@/entities/resource'
import { SUBTITLE_MAX_LENGTH, TITLE_MAX_LENGTH, URL_MAX_LENGTH } from '@/shared/config'
import { useForm } from '@/shared/lib/form'
import { ActionButton } from '@/shared/ui/action-button'
import { RichTextEditor } from '@/shared/ui/rich-text-editor'

import { readResourceDraft, toResourceDraft } from '../model/resource-form'

/** Opens the dialog; each time it opens the form is refilled from `resource`. */
const visible = defineModel<boolean>('visible', { required: true })

const props = defineProps<{
  /** Resource being edited; `null` opens the dialog in create mode. */
  resource: LearningResource | null
  /** Shows a loading state on the save button while the parent persists the resource. */
  saving: boolean
}>()

const emit = defineEmits<{
  /** Fired once the form reads valid and the thumbnail is uploaded. */
  submit: [input: ResourceInput]
}>()

const {
  data: typeList,
  isLoading: typesLoading,
  isError: typesFailed,
  refetch: refetchTypes,
} = useQuery(resourceQueries.types())
const types = computed(() => typeList.value ?? [])

const form = useForm({
  initial: () => toResourceDraft(null),
  read: (current) => readResourceDraft(current, types.value),
})
const { draft, errors, submitted, invalid } = form
const selectedType = computed(() => types.value.find((type) => type.id === draft.resourceTypeId))
const thumbnail = useThumbnailUpload(() => props.resource?.thumbnailId)
const { pickedFile, uploading, uploadError, missingThumbnail } = thumbnail

watch(visible, (open) => {
  if (!open) return
  if (typesFailed.value) void refetchTypes()
  form.reset(toResourceDraft(props.resource))
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
    :title="
      resource
        ? $t('pages.admin.resources.form.editHeader')
        : $t('pages.admin.resources.form.newHeader')
    "
    width="min(920px, 94vw)"
  >
    <form class="form form--scroll" @submit.prevent="save">
      <div class="ca-field">
        <label class="ca-field__label" for="resource-title">{{
          $t('pages.admin.resources.form.title')
        }}</label>
        <el-input
          id="resource-title"
          v-model="draft.title"
          :maxlength="TITLE_MAX_LENGTH"
          :class="{ 'ca-invalid': invalid('title') }"
          :aria-invalid="invalid('title')"
        />
      </div>
      <div class="ca-field">
        <label class="ca-field__label" for="resource-subtitle">{{
          $t('pages.admin.resources.form.subtitle')
        }}</label>
        <el-input
          id="resource-subtitle"
          v-model="draft.subtitle"
          :maxlength="SUBTITLE_MAX_LENGTH"
          :class="{ 'ca-invalid': invalid('subtitle') }"
          :aria-invalid="invalid('subtitle')"
        />
      </div>
      <div class="ca-field">
        <label class="ca-field__label" for="resource-type">{{
          $t('pages.admin.resources.form.type')
        }}</label>
        <el-select
          id="resource-type"
          v-model="draft.resourceTypeId"
          :placeholder="$t('pages.admin.resources.form.typePlaceholder')"
          :loading="typesLoading"
          :class="{ 'ca-invalid': errors.resourceTypeId }"
        >
          <el-option v-for="type in types" :key="type.id" :label="type.name" :value="type.id" />
        </el-select>
        <small v-if="typesFailed" class="ca-field__error">{{
          $t('pages.admin.resources.form.typesLoadError')
        }}</small>
        <small v-else-if="errors.resourceTypeId" class="ca-field__error">{{
          errors.resourceTypeId
        }}</small>
      </div>
      <div v-if="selectedType && !selectedType.isExternal" class="ca-field">
        <div class="ca-field__label">{{ $t('pages.admin.resources.form.description') }}</div>
        <RichTextEditor
          v-model="draft.description"
          :label="$t('pages.admin.resources.form.description')"
          :upload="uploadFileRequest"
          :invalid="!!errors.description"
        />
        <small v-if="errors.description" class="ca-field__error">{{ errors.description }}</small>
      </div>
      <div v-if="selectedType?.isExternal" class="ca-field">
        <label class="ca-field__label" for="resource-url">{{
          $t('pages.admin.resources.form.url')
        }}</label>
        <el-input
          id="resource-url"
          v-model="draft.url"
          :maxlength="URL_MAX_LENGTH"
          :placeholder="$t('pages.admin.resources.form.urlPlaceholder')"
          :class="{ 'ca-invalid': errors.url }"
          :aria-invalid="!!errors.url"
          :aria-describedby="errors.url ? 'resource-url-error' : undefined"
        />
        <small v-if="errors.url" id="resource-url-error" class="ca-field__error">{{
          errors.url
        }}</small>
      </div>
      <div class="ca-field">
        <div id="resource-image-label" class="ca-field__label">{{ $t('common.image') }}</div>
        <ThumbnailField
          role="group"
          aria-labelledby="resource-image-label"
          :existing-thumbnail-id="resource?.thumbnailId"
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
}

.form--scroll {
  max-height: 68vh;
  overflow-y: auto;
}

@media (max-width: 640px) {
  .form--scroll {
    max-height: none;
    overflow-y: visible;
  }
}
</style>
