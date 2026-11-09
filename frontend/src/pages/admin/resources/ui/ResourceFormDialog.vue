<script setup lang="ts">
import { computed, watch } from 'vue'
import { useQuery } from '@tanstack/vue-query'

import { ThumbnailField, uploadFileRequest, useThumbnailUpload } from '@/entities/file'
import { resourceQueries, type LearningResource, type ResourceInput } from '@/entities/resource'
import { SUBTITLE_MAX_LENGTH, TITLE_MAX_LENGTH, URL_MAX_LENGTH } from '@/shared/config'
import { useForm } from '@/shared/lib/form'
import { FormDialog } from '@/shared/ui/form-dialog'
import { FormField } from '@/shared/ui/form-field'
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
  <FormDialog
    v-model:visible="visible"
    data-testid="resource-form-dialog"
    :title="
      resource
        ? $t('pages.admin.resources.form.editHeader')
        : $t('pages.admin.resources.form.newHeader')
    "
    width="min(920px, 94vw)"
    :saving="saving || uploading"
    scroll-height="68vh"
    @submit="save"
  >
    <FormField
      v-slot="{ control }"
      for="resource-title"
      :label="$t('common.title')"
      :invalid="invalid('title')"
    >
      <el-input
        v-model="draft.title"
        v-bind="control"
        data-testid="resource-form-title"
        :maxlength="TITLE_MAX_LENGTH"
      />
    </FormField>
    <FormField
      v-slot="{ control }"
      for="resource-subtitle"
      :label="$t('common.subtitle')"
      :invalid="invalid('subtitle')"
    >
      <el-input
        v-model="draft.subtitle"
        v-bind="control"
        data-testid="resource-form-subtitle"
        :maxlength="SUBTITLE_MAX_LENGTH"
      />
    </FormField>
    <FormField
      v-slot="{ control }"
      for="resource-type"
      :label="$t('pages.admin.resources.form.type')"
      :invalid="!!errors.resourceTypeId"
      :error="typesFailed ? undefined : errors.resourceTypeId"
    >
      <el-select
        :id="control.id"
        v-model="draft.resourceTypeId"
        data-testid="resource-form-type"
        :placeholder="$t('pages.admin.resources.form.typePlaceholder')"
        :loading="typesLoading"
        :class="control.class"
      >
        <el-option
          v-for="type in types"
          :key="type.id"
          :label="type.name"
          :value="type.id"
          :data-testid="`option-${type.id}`"
        />
      </el-select>
      <small v-if="typesFailed" class="ca-field__error">{{
        $t('pages.admin.resources.form.typesLoadError')
      }}</small>
    </FormField>
    <FormField
      v-if="selectedType && !selectedType.isExternal"
      v-slot="{ invalid: refused }"
      :label="$t('common.description')"
      :error="errors.description"
      error-testid="resource-form-description-error"
    >
      <RichTextEditor
        v-model="draft.description"
        data-testid="resource-form-description"
        :label="$t('common.description')"
        :upload="uploadFileRequest"
        :invalid="refused"
      />
    </FormField>
    <FormField
      v-if="selectedType?.isExternal"
      v-slot="{ control }"
      for="resource-url"
      :label="$t('pages.admin.resources.form.url')"
      :error="errors.url"
      error-testid="resource-form-url-error"
    >
      <el-input
        v-model="draft.url"
        v-bind="control"
        data-testid="resource-form-url"
        :maxlength="URL_MAX_LENGTH"
        :placeholder="$t('pages.admin.resources.form.urlPlaceholder')"
      />
    </FormField>
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
  </FormDialog>
</template>
