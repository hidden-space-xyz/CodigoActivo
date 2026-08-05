<script setup lang="ts">
import { reactive, ref, watch } from 'vue'
import { AppButton as Button } from '@/shared/ui'

import { ThumbnailField, useThumbnailUpload } from '@/entities/file'
import type {
  CreatePartnerRequest,
  PartnerResponse,
  UpdatePartnerRequest,
} from '@/shared/api/generated/models'
import { parseDateOnly, toDateOnly } from '@/shared/lib'

const DATE_FORMAT = 'DD/MM/YYYY'

const props = defineProps<{
  visible: boolean
  partner: PartnerResponse | null
  saving: boolean
}>()

const emit = defineEmits<{
  'update:visible': [value: boolean]
  submit: [body: CreatePartnerRequest | UpdatePartnerRequest]
}>()

interface PartnerForm {
  name: string
  fromDate: Date | null
  tier: number | undefined
  website: string
}

const form = reactive<PartnerForm>({ name: '', fromDate: null, tier: 0, website: '' })
const submitted = ref(false)
const {
  pickedFile,
  uploading,
  uploadError,
  missingThumbnail,
  reset: resetThumbnail,
  resolveThumbnailId,
} = useThumbnailUpload(() => props.partner?.thumbnailId)

watch(
  () => props.visible,
  (open) => {
    if (!open) return
    submitted.value = false
    resetThumbnail()
    form.name = props.partner?.name ?? ''
    form.fromDate = parseDateOnly(props.partner?.fromDate)
    form.tier = props.partner?.tier ?? 0
    form.website = props.partner?.website ?? ''
  },
)

function close(): void {
  emit('update:visible', false)
}

async function save(): Promise<void> {
  submitted.value = true
  if (!form.name.trim() || !form.fromDate || missingThumbnail.value) return
  const thumbnailId = await resolveThumbnailId()
  if (!thumbnailId) return
  emit('submit', {
    name: form.name.trim(),
    tier: form.tier ?? 0,
    website: form.website.trim() ? form.website.trim() : null,
    fromDate: toDateOnly(form.fromDate),
    thumbnailId,
  } satisfies CreatePartnerRequest)
}
</script>

<template>
  <el-dialog
    :model-value="visible"
    :title="
      partner ? $t('features.managePartners.editHeader') : $t('features.managePartners.newHeader')
    "
    width="min(460px, 92vw)"
    @update:model-value="close"
  >
    <form class="form" @submit.prevent="save">
      <div class="form__field">
        <label for="partner-name">{{ $t('common.name') }}</label>
        <el-input
          id="partner-name"
          v-model="form.name"
          :maxlength="200"
          :class="{ 'ca-invalid': submitted && !form.name.trim() }"
        />
        <small v-if="submitted && !form.name.trim()" class="form__error">{{
          $t('features.managePartners.nameRequired')
        }}</small>
      </div>

      <div class="form__field">
        <label for="partner-from">{{ $t('features.managePartners.fromDate') }}</label>
        <el-date-picker
          id="partner-from"
          v-model="form.fromDate"
          type="date"
          :format="DATE_FORMAT"
          :class="{ 'ca-invalid': submitted && !form.fromDate }"
        />
        <small v-if="submitted && !form.fromDate" class="form__error">{{
          $t('features.managePartners.fromDateRequired')
        }}</small>
      </div>

      <div class="form__field">
        <label for="partner-tier">{{ $t('features.managePartners.tier') }}</label>
        <el-input-number id="partner-tier" v-model="form.tier" :min="0" controls-position="right" />
      </div>

      <div class="form__field">
        <label for="partner-website">{{ $t('features.managePartners.website') }}</label>
        <el-input
          id="partner-website"
          v-model="form.website"
          :placeholder="$t('features.managePartners.urlPlaceholder')"
        />
      </div>

      <div class="form__field">
        <label>{{ $t('common.image') }}</label>
        <ThumbnailField
          :existing-thumbnail-id="partner?.thumbnailId"
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

.form :deep(.el-date-editor),
.form :deep(.el-input-number) {
  width: 100%;
}

.form :deep(.ca-invalid) {
  --el-input-border-color: var(--ca-danger);
  --el-input-hover-border-color: var(--ca-danger);
  --el-input-focus-border-color: var(--ca-danger);
}
</style>
