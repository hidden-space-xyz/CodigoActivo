<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue'
import Button from '@/shared/ui/components/AppButton.vue'
import DatePicker from 'primevue/datepicker'
import Dialog from 'primevue/dialog'
import InputNumber from 'primevue/inputnumber'
import InputText from 'primevue/inputtext'

import ThumbnailField from '@/features/files/ThumbnailField.vue'
import { uploadThumbnail } from '@/features/files/useThumbnail'
import type {
  CreatePartnerRequest,
  PartnerResponse,
  UpdatePartnerRequest,
} from '@/shared/api/generated/models'
import { getErrorMessage } from '@/shared/utils/api-error'

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
const pickedFile = ref<File | null>(null)
const uploading = ref(false)
const uploadError = ref('')

const missingThumbnail = computed(() => !pickedFile.value && !props.partner?.thumbnailId)

watch(
  () => props.visible,
  (open) => {
    if (!open) return
    submitted.value = false
    pickedFile.value = null
    uploadError.value = ''
    form.name = props.partner?.name ?? ''
    form.fromDate = props.partner?.fromDate ? new Date(props.partner.fromDate) : null
    form.tier = props.partner?.tier ?? 0
    form.website = props.partner?.website ?? ''
  },
)

function close(): void {
  emit('update:visible', false)
}

async function save(): Promise<void> {
  submitted.value = true
  uploadError.value = ''
  if (!form.name.trim() || missingThumbnail.value) return
  const body: CreatePartnerRequest = {
    name: form.name.trim(),
    tier: form.tier,
    website: form.website.trim() ? form.website.trim() : null,
  }
  if (form.fromDate) body.fromDate = form.fromDate.toISOString().slice(0, 10)
  uploading.value = true
  try {
    if (pickedFile.value) {
      body.thumbnailId = await uploadThumbnail(pickedFile.value, props.partner?.thumbnailId)
    } else if (props.partner?.thumbnailId) {
      body.thumbnailId = props.partner.thumbnailId
    }
    emit('submit', body)
  } catch (error) {
    uploadError.value = getErrorMessage(error, 'No se pudo subir la imagen.')
  } finally {
    uploading.value = false
  }
}
</script>

<template>
  <Dialog
    :visible="visible"
    modal
    :header="partner ? 'Editar patrocinador' : 'Nuevo patrocinador'"
    :style="{ width: '460px' }"
    @update:visible="close"
  >
    <form class="form" @submit.prevent="save">
      <div class="form__field">
        <label for="partner-name">Nombre</label>
        <InputText
          id="partner-name"
          v-model="form.name"
          :invalid="submitted && !form.name.trim()"
          fluid
        />
        <small v-if="submitted && !form.name.trim()" class="form__error"
          >El nombre es obligatorio.</small
        >
      </div>

      <div class="form__field">
        <label for="partner-from">Fecha de alta</label>
        <DatePicker
          id="partner-from"
          v-model="form.fromDate"
          date-format="dd/mm/yy"
          show-icon
          fluid
        />
      </div>

      <div class="form__field">
        <label for="partner-tier">Nivel (tier)</label>
        <InputNumber id="partner-tier" v-model="form.tier" :min="0" show-buttons fluid />
      </div>

      <div class="form__field">
        <label for="partner-website">Sitio web</label>
        <InputText id="partner-website" v-model="form.website" placeholder="https://…" fluid />
      </div>

      <div class="form__field">
        <label>Imagen</label>
        <ThumbnailField
          :existing-thumbnail-id="partner?.thumbnailId"
          :invalid="submitted && missingThumbnail"
          @update:file="pickedFile = $event"
        />
        <small v-if="submitted && missingThumbnail" class="form__error"
          >La imagen es obligatoria.</small
        >
        <small v-if="uploadError" class="form__error">{{ uploadError }}</small>
      </div>
    </form>

    <template #footer>
      <Button
        label="Cancelar"
        text
        severity="secondary"
        :disabled="saving || uploading"
        @click="close"
      />
      <Button label="Guardar" :loading="saving || uploading" @click="save" />
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
  color: var(--ca-coral);
  font-size: 12.5px;
}
</style>
