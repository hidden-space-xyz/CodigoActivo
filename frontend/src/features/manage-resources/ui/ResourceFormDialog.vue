<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue'
import { AppButton as Button, RichTextEditor } from '@/shared/ui'

import { ThumbnailField, uploadFileRequest, useThumbnailUpload } from '@/entities/file'
import { useResourceTypesList } from '@/entities/catalog'
import type {
  CreateResourceRequest,
  ResourceResponse,
  ResourceTypeResponse,
  UpdateResourceRequest,
} from '@/shared/api/generated/models'
import { isRichTextBlank } from '@/shared/lib'

const props = defineProps<{
  visible: boolean
  resource: ResourceResponse | null
  saving: boolean
}>()

const emit = defineEmits<{
  'update:visible': [value: boolean]
  submit: [body: CreateResourceRequest | UpdateResourceRequest]
}>()

interface ResourceForm {
  title: string
  subtitle: string
  resourceTypeId: string
  description: string
  url: string
}

const form = reactive<ResourceForm>({
  title: '',
  subtitle: '',
  resourceTypeId: '',
  description: '',
  url: '',
})
const submitted = ref(false)
const {
  pickedFile,
  uploading,
  uploadError,
  missingThumbnail,
  reset: resetThumbnail,
  resolveThumbnailId,
} = useThumbnailUpload(() => props.resource?.thumbnailId)

const typesQuery = useResourceTypesList()
const typeOptions = computed<ResourceTypeResponse[]>(() => typesQuery.data.value ?? [])
const selectedType = computed(() =>
  typeOptions.value.find((type) => type.id === form.resourceTypeId),
)
const isExternal = computed(() => selectedType.value?.isExternal === true)

const typeMissing = computed(() => !form.resourceTypeId)
const typeUnresolved = computed(() => !!form.resourceTypeId && !selectedType.value)
const descriptionMissing = computed(
  () => !!selectedType.value && !isExternal.value && isRichTextBlank(form.description),
)
const urlMissing = computed(() => isExternal.value && !form.url.trim())
const urlInvalid = computed(
  () => isExternal.value && !!form.url.trim() && !isValidHttpUrl(form.url.trim()),
)

function isValidHttpUrl(value: string): boolean {
  if (!/^https?:\/\//i.test(value)) return false
  try {
    new URL(value)
    return true
  } catch {
    return false
  }
}

watch(
  () => props.visible,
  (open) => {
    if (!open) return
    submitted.value = false
    if (typesQuery.isError.value) void typesQuery.refetch()
    resetThumbnail()
    form.title = props.resource?.title ?? ''
    form.subtitle = props.resource?.subtitle ?? ''
    form.resourceTypeId = props.resource?.type?.id ?? ''
    form.description = props.resource?.description ?? ''
    form.url = props.resource?.url ?? ''
  },
)

function close(): void {
  emit('update:visible', false)
}

async function save(): Promise<void> {
  submitted.value = true
  if (
    !form.title.trim() ||
    !form.subtitle.trim() ||
    typeMissing.value ||
    typeUnresolved.value ||
    descriptionMissing.value ||
    urlMissing.value ||
    urlInvalid.value ||
    missingThumbnail.value
  ) {
    return
  }
  const thumbnailId = await resolveThumbnailId()
  if (!thumbnailId) return
  emit('submit', {
    title: form.title.trim(),
    subtitle: form.subtitle.trim(),
    description: isExternal.value ? null : form.description,
    url: isExternal.value ? form.url.trim() : null,
    resourceTypeId: form.resourceTypeId,
    thumbnailId,
  } satisfies CreateResourceRequest)
}
</script>

<template>
  <el-dialog
    :model-value="visible"
    :title="
      resource
        ? $t('features.manageResources.editHeader')
        : $t('features.manageResources.newHeader')
    "
    width="min(920px, 94vw)"
    @update:model-value="close"
  >
    <form class="form form--scroll" @submit.prevent="save">
      <div class="form__field">
        <label>{{ $t('features.manageResources.title') }}</label>
        <el-input
          v-model="form.title"
          :maxlength="200"
          :class="{ 'ca-invalid': submitted && !form.title.trim() }"
        />
      </div>
      <div class="form__field">
        <label>{{ $t('features.manageResources.subtitle') }}</label>
        <el-input
          v-model="form.subtitle"
          :maxlength="300"
          :class="{ 'ca-invalid': submitted && !form.subtitle.trim() }"
        />
      </div>
      <div class="form__field">
        <label>{{ $t('features.manageResources.type') }}</label>
        <el-select
          v-model="form.resourceTypeId"
          :placeholder="$t('features.manageResources.typePlaceholder')"
          :loading="typesQuery.isLoading.value"
          :class="{ 'ca-invalid': submitted && typeMissing }"
        >
          <el-option
            v-for="type in typeOptions"
            :key="type.id ?? ''"
            :label="type.name ?? ''"
            :value="type.id ?? ''"
          />
        </el-select>
        <small v-if="typesQuery.isError.value" class="form__error">{{
          $t('features.manageResources.typesLoadError')
        }}</small>
        <small v-else-if="submitted && typeMissing" class="form__error">{{
          $t('features.manageResources.typeRequired')
        }}</small>
      </div>
      <div v-if="selectedType && !isExternal" class="form__field">
        <label>{{ $t('features.manageResources.description') }}</label>
        <RichTextEditor v-model="form.description" :upload="uploadFileRequest" />
        <small v-if="submitted && descriptionMissing" class="form__error">{{
          $t('features.manageResources.descriptionRequired')
        }}</small>
      </div>
      <div v-if="isExternal" class="form__field">
        <label>{{ $t('features.manageResources.url') }}</label>
        <el-input
          v-model="form.url"
          :maxlength="500"
          :placeholder="$t('features.manageResources.urlPlaceholder')"
          :class="{ 'ca-invalid': submitted && (urlMissing || urlInvalid) }"
        />
        <small v-if="submitted && urlMissing" class="form__error">{{
          $t('features.manageResources.urlRequired')
        }}</small>
        <small v-else-if="submitted && urlInvalid" class="form__error">{{
          $t('features.manageResources.urlInvalid')
        }}</small>
      </div>
      <div class="form__field">
        <label>{{ $t('common.image') }}</label>
        <ThumbnailField
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
      <Button :label="$t('common.cancel')" text :disabled="saving || uploading" @click="close" />
      <Button
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

.form__field label {
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

.form :deep(.ca-invalid) {
  --el-input-border-color: var(--ca-danger);
  --el-input-hover-border-color: var(--ca-danger);
  --el-input-focus-border-color: var(--ca-danger);
}

.form :deep(.ca-invalid .el-select__wrapper) {
  box-shadow: 0 0 0 1px var(--ca-danger) inset;
}

@media (max-width: 640px) {
  .form--scroll {
    max-height: none;
    overflow-y: visible;
  }
}
</style>
