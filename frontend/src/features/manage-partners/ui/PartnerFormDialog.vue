<script setup lang="ts">
import { reactive, ref, watch } from 'vue'
import { AppButton as Button } from '@/shared/ui'
import DatePicker from 'primevue/datepicker'
import Dialog from 'primevue/dialog'
import InputNumber from 'primevue/inputnumber'
import InputText from 'primevue/inputtext'

import { ThumbnailField, useThumbnailUpload } from '@/entities/file'
import type {
  CreatePartnerRequest,
  PartnerResponse,
  UpdatePartnerRequest,
} from '@/shared/api/generated/models'
import { parseDateOnly, toDateOnly } from '@/shared/lib'

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
  tier: number
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
    tier: form.tier,
    website: form.website.trim() ? form.website.trim() : null,
    fromDate: toDateOnly(form.fromDate),
    thumbnailId,
  } satisfies CreatePartnerRequest)
}
</script>

<template>
  <Dialog
    :visible="visible"
    modal
    :header="partner ? $t('features.managePartners.editHeader') : $t('features.managePartners.newHeader')"
    :style="{ width: '460px' }"
    @update:visible="close"
  >
    <form class="form" @submit.prevent="save">
      <div class="form__field">
        <label for="partner-name">{{ $t('common.name') }}</label>
        <InputText
          id="partner-name"
          v-model="form.name"
          :maxlength="200"
          :invalid="submitted && !form.name.trim()"
          fluid
        />
        <small v-if="submitted && !form.name.trim()" class="form__error"
          >{{ $t('features.managePartners.nameRequired') }}</small
        >
      </div>

      <div class="form__field">
        <label for="partner-from">{{ $t('features.managePartners.fromDate') }}</label>
        <DatePicker
          id="partner-from"
          v-model="form.fromDate"
          date-format="dd/mm/yy"
          show-icon
          :invalid="submitted && !form.fromDate"
          fluid
        />
        <small v-if="submitted && !form.fromDate" class="form__error"
          >{{ $t('features.managePartners.fromDateRequired') }}</small
        >
      </div>

      <div class="form__field">
        <label for="partner-tier">{{ $t('features.managePartners.tier') }}</label>
        <InputNumber id="partner-tier" v-model="form.tier" :min="0" show-buttons fluid />
      </div>

      <div class="form__field">
        <label for="partner-website">{{ $t('features.managePartners.website') }}</label>
        <InputText
          id="partner-website"
          v-model="form.website"
          :placeholder="$t('features.managePartners.urlPlaceholder')"
          fluid
        />
      </div>

      <div class="form__field">
        <label>{{ $t('common.image') }}</label>
        <ThumbnailField
          :existing-thumbnail-id="partner?.thumbnailId"
          :invalid="submitted && missingThumbnail"
          @update:file="pickedFile = $event"
        />
        <small v-if="submitted && missingThumbnail" class="form__error"
          >{{ $t('common.imageRequired') }}</small
        >
        <small v-if="uploadError" class="form__error">{{ uploadError }}</small>
      </div>
    </form>

    <template #footer>
      <Button
        :label="$t('common.cancel')"
        text
        severity="secondary"
        :disabled="saving || uploading"
        @click="close"
      />
      <Button :label="$t('common.save')" :loading="saving || uploading" @click="save" />
    </template>
  </Dialog>
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
</style>
