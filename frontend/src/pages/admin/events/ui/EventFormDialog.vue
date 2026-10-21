<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useQuery } from '@tanstack/vue-query'

import { eventCategoryQueries } from '@/entities/event-category'
import type { EventDetail, EventInput } from '@/entities/event'
import { ThumbnailField, uploadFileRequest, useThumbnailUpload } from '@/entities/file'
import { termsDocumentQueries } from '@/entities/terms-document'
import { isDayAfter, isDayBefore } from '@/shared/lib/date'
import { useForm } from '@/shared/lib/form'
import { ActionButton } from '@/shared/ui/action-button'
import { RichTextEditor } from '@/shared/ui/rich-text-editor'

import { readEventDraft, toEventDraft } from '../model/event-form'
import NewCategoryDialog from './NewCategoryDialog.vue'

const DATE_FORMAT = 'DD/MM/YYYY'
const DATE_TIME_FORMAT = 'DD/MM/YYYY HH:mm'

/** Opens the dialog; each time it opens the form is refilled from `event`. */
const visible = defineModel<boolean>('visible', { required: true })

const props = defineProps<{
  /** Event being edited; `null` opens the dialog in create mode. */
  event: EventDetail | null
  /** Shows a loading state on the save button while the parent persists the event. */
  saving: boolean
}>()

const emit = defineEmits<{
  /** Fired once the form reads valid and the thumbnail is uploaded. */
  submit: [input: EventInput]
}>()

const termsDocuments = useQuery(termsDocumentQueries.options())
const termsOptions = computed(() => termsDocuments.data.value ?? [])
const consentIds = computed(
  () =>
    new Set(
      termsOptions.value
        .filter((document) => document.kind === 'Consent')
        .map((document) => document.id),
    ),
)
const form = useForm({
  initial: () => toEventDraft(null),
  read: (value) => readEventDraft(value, consentIds.value),
})
const { draft, submitted, errors, invalid } = form
const thumbnail = useThumbnailUpload(() => props.event?.thumbnailId)
const { pickedFile, uploading, uploadError, missingThumbnail } = thumbnail

const categories = useQuery(eventCategoryQueries.options())
const categoryOptions = computed(() => categories.data.value ?? [])
const categoryDialogVisible = ref(false)

watch(visible, (open) => {
  if (!open) return
  form.reset(toEventDraft(props.event))
  thumbnail.reset()
})

function termsName(id: string): string {
  return termsOptions.value.find((document) => document.id === id)?.name ?? ''
}

function setTermsRequired(id: string, value: string | number | boolean): void {
  draft.termsRequired[id] = value === true
}

function selectCategory(categoryId: string): void {
  if (!draft.categoryIds.includes(categoryId)) draft.categoryIds.push(categoryId)
}

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
      event ? $t('pages.admin.events.form.editHeader') : $t('pages.admin.events.form.newHeader')
    "
    width="min(920px, 94vw)"
    append-to-body
  >
    <form class="form form--scroll" @submit.prevent="save">
      <div class="ca-field">
        <label class="ca-field__label" for="event-title">{{
          $t('pages.admin.events.form.fields.title')
        }}</label>
        <el-input
          id="event-title"
          v-model="draft.title"
          :maxlength="200"
          :class="{ 'ca-invalid': invalid('title') }"
          :aria-invalid="invalid('title')"
        />
      </div>
      <div class="ca-field">
        <label class="ca-field__label" for="event-subtitle">{{
          $t('pages.admin.events.form.fields.subtitle')
        }}</label>
        <el-input
          id="event-subtitle"
          v-model="draft.subtitle"
          :maxlength="300"
          :class="{ 'ca-invalid': invalid('subtitle') }"
          :aria-invalid="invalid('subtitle')"
        />
      </div>
      <div class="ca-field">
        <label class="ca-field__label" for="event-categories">{{
          $t('pages.admin.events.form.fields.categories')
        }}</label>
        <div class="form__cats">
          <el-select
            id="event-categories"
            v-model="draft.categoryIds"
            multiple
            filterable
            collapse-tags
            collapse-tags-tooltip
            :placeholder="$t('pages.admin.events.form.categoriesPlaceholder')"
            class="form__cats-select"
            :class="{ 'ca-invalid': invalid('categoryIds') }"
          >
            <el-option
              v-for="category in categoryOptions"
              :key="category.id"
              :label="category.name"
              :value="category.id"
            />
          </el-select>
          <ActionButton
            :label="$t('pages.admin.events.form.newCategory')"
            icon="plus"
            type="primary"
            text
            size="small"
            @click="categoryDialogVisible = true"
          />
        </div>
        <small v-if="errors.categoryIds" class="ca-field__error">{{ errors.categoryIds }}</small>
      </div>
      <div class="ca-field">
        <label class="ca-field__label" for="event-terms-documents">{{
          $t('pages.admin.events.form.fields.termsDocuments')
        }}</label>
        <el-select
          id="event-terms-documents"
          v-model="draft.termsDocumentIds"
          multiple
          filterable
          collapse-tags
          collapse-tags-tooltip
          :placeholder="$t('pages.admin.events.form.termsPlaceholder')"
        >
          <el-option
            v-for="document in termsOptions"
            :key="document.id"
            :label="document.name"
            :value="document.id"
          />
        </el-select>
        <small class="ca-field__hint">{{
          $t('pages.admin.events.form.hints.termsDocuments')
        }}</small>
        <ul v-if="draft.termsDocumentIds.length" class="form__terms-list">
          <li v-for="id in draft.termsDocumentIds" :key="id" class="form__terms-row">
            <span class="form__terms-name">{{ termsName(id) }}</span>
            <span v-if="consentIds.has(id)" class="form__terms-consent">{{
              $t('pages.admin.events.form.termsConsentOptional')
            }}</span>
            <el-switch
              v-else
              :model-value="draft.termsRequired[id] ?? true"
              :active-text="$t('pages.admin.events.form.termsRequired')"
              @update:model-value="
                (value: string | number | boolean) => setTermsRequired(id, value)
              "
            />
          </li>
        </ul>
      </div>
      <div class="ca-field">
        <div class="ca-field__label">{{ $t('pages.admin.events.form.fields.description') }}</div>
        <RichTextEditor
          v-model="draft.description"
          :label="$t('pages.admin.events.form.fields.description')"
          :upload="uploadFileRequest"
          :invalid="!!errors.description"
        />
        <small v-if="errors.description" class="ca-field__error">{{ errors.description }}</small>
      </div>
      <div class="ca-form-grid">
        <div class="ca-field">
          <label class="ca-field__label" for="event-start">{{
            $t('pages.admin.events.form.fields.eventStart')
          }}</label>
          <el-date-picker
            id="event-start"
            v-model="draft.startsAt"
            type="date"
            :format="DATE_FORMAT"
            :class="{ 'ca-invalid': invalid('startsAt') }"
          />
          <small v-if="errors.startsAt" class="ca-field__error">{{ errors.startsAt }}</small>
        </div>
        <div class="ca-field">
          <label class="ca-field__label" for="event-end">{{
            $t('pages.admin.events.form.fields.eventEnd')
          }}</label>
          <el-date-picker
            id="event-end"
            v-model="draft.endsAt"
            type="date"
            :format="DATE_FORMAT"
            :disabled-date="(date: Date) => isDayBefore(date, draft.startsAt)"
            :class="{ 'ca-invalid': invalid('endsAt') }"
          />
          <small v-if="errors.endsAt" class="ca-field__error">{{ errors.endsAt }}</small>
        </div>
      </div>
      <div class="ca-field">
        <label class="ca-field__label" for="event-early-signup-start">{{
          $t('pages.admin.events.form.fields.earlySignupStart')
        }}</label>
        <el-date-picker
          id="event-early-signup-start"
          v-model="draft.earlySignupStartsAt"
          type="datetime"
          clearable
          :format="DATE_TIME_FORMAT"
          :disabled-date="(date: Date) => isDayAfter(date, draft.signupStartsAt)"
          :class="{ 'ca-invalid': invalid('earlySignupStartsAt') }"
        />
        <small v-if="errors.earlySignupStartsAt" class="ca-field__error">{{
          errors.earlySignupStartsAt
        }}</small>
        <small v-else class="ca-field__hint">{{
          $t('pages.admin.events.form.hints.earlySignupStart')
        }}</small>
      </div>
      <div class="ca-form-grid">
        <div class="ca-field">
          <label class="ca-field__label" for="event-signup-start">{{
            $t('pages.admin.events.form.fields.signupStart')
          }}</label>
          <el-date-picker
            id="event-signup-start"
            v-model="draft.signupStartsAt"
            type="datetime"
            :format="DATE_TIME_FORMAT"
            :class="{ 'ca-invalid': invalid('signupStartsAt') }"
          />
          <small v-if="errors.signupStartsAt" class="ca-field__error">{{
            errors.signupStartsAt
          }}</small>
        </div>
        <div class="ca-field">
          <label class="ca-field__label" for="event-signup-end">{{
            $t('pages.admin.events.form.fields.signupEnd')
          }}</label>
          <el-date-picker
            id="event-signup-end"
            v-model="draft.signupEndsAt"
            type="datetime"
            :format="DATE_TIME_FORMAT"
            :disabled-date="
              (date: Date) =>
                isDayBefore(date, draft.signupStartsAt) || isDayAfter(date, draft.endsAt)
            "
            :class="{ 'ca-invalid': invalid('signupEndsAt') }"
          />
          <small v-if="errors.signupEndsAt" class="ca-field__error">{{
            errors.signupEndsAt
          }}</small>
        </div>
      </div>
      <div class="ca-field">
        <div id="event-image-label" class="ca-field__label">{{ $t('common.image') }}</div>
        <ThumbnailField
          role="group"
          aria-labelledby="event-image-label"
          :existing-thumbnail-id="event?.thumbnailId"
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

  <NewCategoryDialog v-model:visible="categoryDialogVisible" @created="selectCategory" />
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

.form__cats {
  display: flex;
  align-items: center;
  gap: 8px;
}

.form__cats-select {
  flex: 1 1 auto;
  min-width: 0;
}

.form__terms-list {
  list-style: none;
  margin: 4px 0 0;
  padding: 0;
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.form__terms-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  background: var(--ca-surface);
  border: 1px solid var(--ca-border-soft);
  border-radius: 10px;
  padding: 8px 12px;
}

.form__terms-name {
  font-size: 13.5px;
  color: var(--ca-text);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.form__terms-consent {
  font-size: 12.5px;
  color: var(--ca-text-muted);
  white-space: nowrap;
}

@media (max-width: 640px) {
  .form--scroll {
    max-height: none;
    overflow-y: visible;
  }
}
</style>
