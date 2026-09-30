<script setup lang="ts">
import { computed, watch } from 'vue'
import { useQuery } from '@tanstack/vue-query'

import { ThumbnailField, uploadFileRequest, useThumbnailUpload } from '@/entities/file'
import { resourceQueries, type LearningResource, type ResourceInput } from '@/entities/resource'
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
  if (!value || missingThumbnail.value) return
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
      <div class="form__field">
        <label for="resource-title">{{ $t('pages.admin.resources.form.title') }}</label>
        <el-input
          id="resource-title"
          v-model="draft.title"
          :maxlength="200"
          :class="{ 'ca-invalid': invalid('title') }"
        />
      </div>
      <div class="form__field">
        <label for="resource-subtitle">{{ $t('pages.admin.resources.form.subtitle') }}</label>
        <el-input
          id="resource-subtitle"
          v-model="draft.subtitle"
          :maxlength="300"
          :class="{ 'ca-invalid': invalid('subtitle') }"
        />
      </div>
      <div class="form__field">
        <label for="resource-type">{{ $t('pages.admin.resources.form.type') }}</label>
        <el-select
          id="resource-type"
          v-model="draft.resourceTypeId"
          :placeholder="$t('pages.admin.resources.form.typePlaceholder')"
          :loading="typesLoading"
          :class="{ 'ca-invalid': errors.resourceTypeId }"
        >
          <el-option v-for="type in types" :key="type.id" :label="type.name" :value="type.id" />
        </el-select>
        <small v-if="typesFailed" class="form__error">{{
          $t('pages.admin.resources.form.typesLoadError')
        }}</small>
        <small v-else-if="errors.resourceTypeId" class="form__error">{{
          errors.resourceTypeId
        }}</small>
      </div>
      <div v-if="selectedType && !selectedType.isExternal" class="form__field">
        <div class="form__label">{{ $t('pages.admin.resources.form.description') }}</div>
        <RichTextEditor
          v-model="draft.description"
          :label="$t('pages.admin.resources.form.description')"
          :upload="uploadFileRequest"
          :invalid="!!errors.description"
        />
        <small v-if="errors.description" class="form__error">{{ errors.description }}</small>
      </div>
      <div v-if="selectedType?.isExternal" class="form__field">
        <label for="resource-url">{{ $t('pages.admin.resources.form.url') }}</label>
        <el-input
          id="resource-url"
          v-model="draft.url"
          :maxlength="500"
          :placeholder="$t('pages.admin.resources.form.urlPlaceholder')"
          :class="{ 'ca-invalid': errors.url }"
        />
        <small v-if="errors.url" class="form__error">{{ errors.url }}</small>
      </div>
      <div class="form__field">
        <div id="resource-image-label" class="form__label">{{ $t('common.image') }}</div>
        <ThumbnailField
          role="group"
          aria-labelledby="resource-image-label"
          :existing-thumbnail-id="resource?.thumbnailId"
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
}

.form--scroll {
  max-height: 68vh;
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

.form :deep(.el-select) {
  width: 100%;
}

@media (max-width: 640px) {
  .form--scroll {
    max-height: none;
    overflow-y: visible;
  }
}
</style>
