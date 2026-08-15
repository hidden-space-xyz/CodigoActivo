<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { AppButton as Button, ColorTag, RichTextEditor } from '@/shared/ui'

import { ThumbnailField, uploadFileRequest, useThumbnailUpload } from '@/entities/file'
import {
  useCreateEventCategoryType,
  useEventCategoryTypesList,
  useTermsDocumentsList,
} from '@/entities/catalog'
import type {
  CreateEventRequest,
  EventCategoryTypeResponse,
  EventResponse,
  TermsDocumentResponse,
  UpdateEventRequest,
} from '@/shared/api/generated/models'
import { EMPTY_DOC_JSON, getErrorMessage, parseDateOnly, toDateOnly } from '@/shared/lib'
import { DEFAULT_CATEGORY_COLOR } from '@/shared/config'

const DATE_FORMAT = 'DD/MM/YYYY'
const DATE_TIME_FORMAT = 'DD/MM/YYYY HH:mm'

const props = defineProps<{ visible: boolean; event: EventResponse | null; saving: boolean }>()

const emit = defineEmits<{
  'update:visible': [value: boolean]
  submit: [body: CreateEventRequest | UpdateEventRequest]
}>()

const { t } = useI18n()

interface EventForm {
  title: string
  subtitle: string
  description: string
  categoryIds: string[]
  termsDocumentId: string
  eventStartsAt: Date | null
  eventEndsAt: Date | null
  earlySignupStartsAt: Date | null
  signupStartsAt: Date | null
  signupEndsAt: Date | null
}

const form = reactive<EventForm>({
  title: '',
  subtitle: '',
  description: '',
  categoryIds: [],
  termsDocumentId: '',
  eventStartsAt: null,
  eventEndsAt: null,
  earlySignupStartsAt: null,
  signupStartsAt: null,
  signupEndsAt: null,
})
const submitted = ref(false)
const {
  pickedFile,
  uploading,
  uploadError,
  missingThumbnail,
  reset: resetThumbnail,
  resolveThumbnailId,
} = useThumbnailUpload(() => props.event?.thumbnailId)

const categoriesQuery = useEventCategoryTypesList()
const categoryOptions = computed<EventCategoryTypeResponse[]>(
  () => categoriesQuery.data.value ?? [],
)
const categoriesMissing = computed(() => form.categoryIds.length === 0)

const termsQuery = useTermsDocumentsList()
const termsOptions = computed<TermsDocumentResponse[]>(() => termsQuery.data.value ?? [])

const createCategory = useCreateEventCategoryType()
const catDialogVisible = ref(false)
const catSubmitted = ref(false)
const creatingCat = createCategory.isPending
const catError = ref('')
const newCat = reactive<{ name: string; color: string }>({
  name: '',
  color: DEFAULT_CATEGORY_COLOR,
})
const newCatHex = computed(() => `#${newCat.color.replace(/^#/, '')}`)

function setNewCatColor(value: string | null): void {
  newCat.color = value ?? DEFAULT_CATEGORY_COLOR
}

function openNewCategory(): void {
  newCat.name = ''
  newCat.color = DEFAULT_CATEGORY_COLOR
  catSubmitted.value = false
  catError.value = ''
  catDialogVisible.value = true
}

function submitNewCategory(): void {
  catSubmitted.value = true
  catError.value = ''
  if (!newCat.name.trim()) return
  createCategory.mutate(
    { name: newCat.name.trim(), color: newCatHex.value },
    {
      onSuccess: (created) => {
        if (created.id && !form.categoryIds.includes(created.id)) form.categoryIds.push(created.id)
        catDialogVisible.value = false
      },
      onError: (error) => {
        catError.value = getErrorMessage(
          error,
          t('features.manageEvents.categoryDialog.createError'),
        )
      },
    },
  )
}

function parse(value?: string | null): Date | null {
  return value ? new Date(value) : null
}

function disabledEventEndDate(date: Date): boolean {
  const start = form.eventStartsAt
  return !!start && toDateOnly(date) < toDateOnly(start)
}

function disabledEarlySignupDate(date: Date): boolean {
  const max = form.signupStartsAt
  return !!max && toDateOnly(date) > toDateOnly(max)
}

function disabledSignupEndDate(date: Date): boolean {
  const start = form.signupStartsAt
  return !!start && toDateOnly(date) < toDateOnly(start)
}

const eventStartMissing = computed(() => !form.eventStartsAt)
const eventEndMissing = computed(() => !form.eventEndsAt)
const eventOrderInvalid = computed(
  () => !!form.eventStartsAt && !!form.eventEndsAt && form.eventEndsAt < form.eventStartsAt,
)
const signupStartMissing = computed(() => !form.signupStartsAt)
const signupEndMissing = computed(() => !form.signupEndsAt)
const signupOrderInvalid = computed(
  () => !!form.signupStartsAt && !!form.signupEndsAt && form.signupEndsAt <= form.signupStartsAt,
)
const signupAfterEventEnd = computed(
  () =>
    !!form.signupStartsAt &&
    !!form.eventEndsAt &&
    toDateOnly(form.signupStartsAt) > toDateOnly(form.eventEndsAt),
)
const earlySignupOrderInvalid = computed(
  () =>
    !!form.earlySignupStartsAt &&
    !!form.signupStartsAt &&
    form.earlySignupStartsAt >= form.signupStartsAt,
)
const datesValid = computed(
  () =>
    !eventStartMissing.value &&
    !eventEndMissing.value &&
    !eventOrderInvalid.value &&
    !signupStartMissing.value &&
    !signupEndMissing.value &&
    !signupOrderInvalid.value &&
    !signupAfterEventEnd.value &&
    !earlySignupOrderInvalid.value,
)

watch(
  () => props.visible,
  (open) => {
    if (!open) return
    submitted.value = false
    resetThumbnail()
    form.title = props.event?.title ?? ''
    form.subtitle = props.event?.subtitle ?? ''
    form.description = props.event?.description ?? ''
    form.categoryIds = (props.event?.categories ?? [])
      .map((cat) => cat.categoryTypeId)
      .filter((id): id is string => !!id)
    form.termsDocumentId = props.event?.termsDocument?.id ?? ''
    form.eventStartsAt = parseDateOnly(props.event?.eventStartsAt)
    form.eventEndsAt = parseDateOnly(props.event?.eventEndsAt)
    form.earlySignupStartsAt = parse(props.event?.earlySignupStartsAt)
    form.signupStartsAt = parse(props.event?.signupStartsAt)
    form.signupEndsAt = parse(props.event?.signupEndsAt)
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
    missingThumbnail.value ||
    !datesValid.value ||
    categoriesMissing.value
  ) {
    return
  }
  const { eventStartsAt, eventEndsAt, signupStartsAt, signupEndsAt } = form
  if (!eventStartsAt || !eventEndsAt || !signupStartsAt || !signupEndsAt) return
  const thumbnailId = await resolveThumbnailId()
  if (!thumbnailId) return
  emit('submit', {
    title: form.title.trim(),
    subtitle: form.subtitle.trim(),
    description: form.description.trim() ? form.description : EMPTY_DOC_JSON,
    categoryTypeIds: form.categoryIds,
    eventStartsAt: toDateOnly(eventStartsAt),
    eventEndsAt: toDateOnly(eventEndsAt),
    earlySignupStartsAt: form.earlySignupStartsAt?.toISOString() ?? null,
    signupStartsAt: signupStartsAt.toISOString(),
    signupEndsAt: signupEndsAt.toISOString(),
    thumbnailId,
    termsDocumentId: form.termsDocumentId || null,
  } satisfies CreateEventRequest)
}
</script>

<template>
  <el-dialog
    :model-value="visible"
    :title="event ? $t('features.manageEvents.editHeader') : $t('features.manageEvents.newHeader')"
    width="min(920px, 94vw)"
    append-to-body
    @update:model-value="close"
  >
    <form class="form form--scroll" @submit.prevent="save">
      <div class="form__field">
        <label>{{ $t('features.manageEvents.fields.title') }}</label>
        <el-input
          v-model="form.title"
          :maxlength="200"
          :class="{ 'ca-invalid': submitted && !form.title.trim() }"
        />
      </div>
      <div class="form__field">
        <label>{{ $t('features.manageEvents.fields.subtitle') }}</label>
        <el-input
          v-model="form.subtitle"
          :maxlength="300"
          :class="{ 'ca-invalid': submitted && !form.subtitle.trim() }"
        />
      </div>
      <div class="form__field">
        <label>{{ $t('features.manageEvents.fields.categories') }}</label>
        <div class="form__cats">
          <el-select
            v-model="form.categoryIds"
            multiple
            filterable
            collapse-tags
            collapse-tags-tooltip
            :placeholder="$t('features.manageEvents.categoriesPlaceholder')"
            class="form__cats-select"
            :class="{ 'ca-invalid': submitted && categoriesMissing }"
          >
            <el-option
              v-for="category in categoryOptions"
              :key="category.id ?? ''"
              :label="category.name ?? ''"
              :value="category.id ?? ''"
            />
          </el-select>
          <Button
            :label="$t('features.manageEvents.newCategory')"
            icon="plus"
            type="primary"
            text
            size="small"
            @click="openNewCategory"
          />
        </div>
        <small v-if="submitted && categoriesMissing" class="form__error">{{
          $t('features.manageEvents.errors.categoriesRequired')
        }}</small>
      </div>
      <div class="form__field">
        <label>{{ $t('features.manageEvents.fields.termsDocument') }}</label>
        <el-select
          v-model="form.termsDocumentId"
          clearable
          filterable
          :placeholder="$t('features.manageEvents.termsPlaceholder')"
        >
          <el-option
            v-for="terms in termsOptions"
            :key="terms.id ?? ''"
            :label="terms.name ?? ''"
            :value="terms.id ?? ''"
          />
        </el-select>
        <small class="form__hint">{{ $t('features.manageEvents.hints.termsDocument') }}</small>
      </div>
      <div class="form__field">
        <label>{{ $t('features.manageEvents.fields.description') }}</label>
        <RichTextEditor v-model="form.description" :upload="uploadFileRequest" />
      </div>
      <div class="form__row">
        <div class="form__field">
          <label>{{ $t('features.manageEvents.fields.eventStart') }}</label>
          <el-date-picker
            v-model="form.eventStartsAt"
            type="date"
            :format="DATE_FORMAT"
            :class="{ 'ca-invalid': submitted && eventStartMissing }"
          />
          <small v-if="submitted && eventStartMissing" class="form__error">{{
            $t('features.manageEvents.errors.eventStartRequired')
          }}</small>
        </div>
        <div class="form__field">
          <label>{{ $t('features.manageEvents.fields.eventEnd') }}</label>
          <el-date-picker
            v-model="form.eventEndsAt"
            type="date"
            :format="DATE_FORMAT"
            :disabled-date="disabledEventEndDate"
            :class="{
              'ca-invalid': submitted && (eventEndMissing || eventOrderInvalid),
            }"
          />
          <small v-if="submitted && eventEndMissing" class="form__error">{{
            $t('features.manageEvents.errors.eventEndRequired')
          }}</small>
          <small v-else-if="submitted && eventOrderInvalid" class="form__error">{{
            $t('features.manageEvents.errors.eventOrderInvalid')
          }}</small>
        </div>
      </div>
      <div class="form__field">
        <label>{{ $t('features.manageEvents.fields.earlySignupStart') }}</label>
        <el-date-picker
          v-model="form.earlySignupStartsAt"
          type="datetime"
          clearable
          :format="DATE_TIME_FORMAT"
          :disabled-date="disabledEarlySignupDate"
          :class="{ 'ca-invalid': submitted && earlySignupOrderInvalid }"
        />
        <small v-if="submitted && earlySignupOrderInvalid" class="form__error">{{
          $t('features.manageEvents.errors.earlySignupOrderInvalid')
        }}</small>
        <small v-else class="form__hint">{{
          $t('features.manageEvents.hints.earlySignupStart')
        }}</small>
      </div>
      <div class="form__row">
        <div class="form__field">
          <label>{{ $t('features.manageEvents.fields.signupStart') }}</label>
          <el-date-picker
            v-model="form.signupStartsAt"
            type="datetime"
            :format="DATE_TIME_FORMAT"
            :class="{
              'ca-invalid': submitted && (signupStartMissing || signupAfterEventEnd),
            }"
          />
          <small v-if="submitted && signupStartMissing" class="form__error">{{
            $t('features.manageEvents.errors.signupStartRequired')
          }}</small>
          <small v-else-if="submitted && signupAfterEventEnd" class="form__error">{{
            $t('features.manageEvents.errors.signupAfterEventEnd')
          }}</small>
        </div>
        <div class="form__field">
          <label>{{ $t('features.manageEvents.fields.signupEnd') }}</label>
          <el-date-picker
            v-model="form.signupEndsAt"
            type="datetime"
            :format="DATE_TIME_FORMAT"
            :disabled-date="disabledSignupEndDate"
            :class="{
              'ca-invalid': submitted && (signupEndMissing || signupOrderInvalid),
            }"
          />
          <small v-if="submitted && signupEndMissing" class="form__error">{{
            $t('features.manageEvents.errors.signupEndRequired')
          }}</small>
          <small v-else-if="submitted && signupOrderInvalid" class="form__error">{{
            $t('features.manageEvents.errors.signupOrderInvalid')
          }}</small>
        </div>
      </div>
      <div class="form__field">
        <label>{{ $t('common.image') }}</label>
        <ThumbnailField
          :existing-thumbnail-id="event?.thumbnailId"
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

  <el-dialog
    v-model="catDialogVisible"
    :title="$t('features.manageEvents.categoryDialog.header')"
    width="min(380px, 92vw)"
    append-to-body
  >
    <form class="form" @submit.prevent="submitNewCategory">
      <div class="form__field">
        <label>{{ $t('common.name') }}</label>
        <el-input
          v-model="newCat.name"
          :maxlength="120"
          :class="{ 'ca-invalid': catSubmitted && !newCat.name.trim() }"
        />
      </div>
      <div class="form__field">
        <label>{{ $t('features.manageEvents.categoryDialog.colorLabel') }}</label>
        <div class="form__cat-color">
          <el-color-picker
            :model-value="newCatHex"
            color-format="hex"
            @update:model-value="setNewCatColor"
          />
          <ColorTag
            :value="newCat.name.trim() || $t('features.manageEvents.categoryDialog.example')"
            :color="newCatHex"
          />
          <span class="form__hex">{{ newCatHex }}</span>
        </div>
      </div>
      <small v-if="catError" class="form__error">{{ catError }}</small>
    </form>
    <template #footer>
      <Button
        :label="$t('common.cancel')"
        text
        :disabled="creatingCat"
        @click="catDialogVisible = false"
      />
      <Button
        :label="$t('common.create')"
        type="primary"
        :loading="creatingCat"
        @click="submitNewCategory"
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

.form__row {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 14px;
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

.form__cats {
  display: flex;
  align-items: center;
  gap: 8px;
}

.form__cats-select {
  flex: 1 1 auto;
  min-width: 0;
}

.form__cat-color {
  display: flex;
  align-items: center;
  gap: 12px;
}

.form__hex {
  font-family: var(--ca-font-mono);
  font-size: 13px;
  color: var(--ca-text-muted);
}

.form__error {
  color: var(--ca-danger-ink);
  font-size: 12.5px;
}

.form__hint {
  color: var(--ca-text-muted);
  font-size: 12.5px;
}

.form :deep(.el-date-editor) {
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

  .form__row {
    grid-template-columns: 1fr;
  }

  .form__cat-color {
    flex-wrap: wrap;
  }
}
</style>
