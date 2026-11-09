<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useQuery } from '@tanstack/vue-query'

import { eventCategoryQueries } from '@/entities/event-category'
import type { EventDetail, EventInput } from '@/entities/event'
import { ThumbnailField, uploadFileRequest, useThumbnailUpload } from '@/entities/file'
import { termsDocumentQueries } from '@/entities/terms-document'
import { SUBTITLE_MAX_LENGTH, TITLE_MAX_LENGTH } from '@/shared/config'
import { isDayAfter, isDayBefore } from '@/shared/lib/date'
import { useForm } from '@/shared/lib/form'
import { ActionButton } from '@/shared/ui/action-button'
import { FormDialog } from '@/shared/ui/form-dialog'
import { FormField } from '@/shared/ui/form-field'
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
  <FormDialog
    v-model:visible="visible"
    :title="
      event ? $t('pages.admin.events.form.editHeader') : $t('pages.admin.events.form.newHeader')
    "
    width="min(920px, 94vw)"
    append-to-body
    data-testid="event-form-dialog"
    :saving="saving || uploading"
    scroll-height="68vh"
    @submit="save"
  >
    <FormField
      v-slot="{ control }"
      for="event-title"
      :label="$t('common.title')"
      :invalid="invalid('title')"
    >
      <el-input
        v-model="draft.title"
        v-bind="control"
        :maxlength="TITLE_MAX_LENGTH"
        data-testid="event-form-title"
      />
    </FormField>
    <FormField
      v-slot="{ control }"
      for="event-subtitle"
      :label="$t('common.subtitle')"
      :invalid="invalid('subtitle')"
    >
      <el-input
        v-model="draft.subtitle"
        v-bind="control"
        :maxlength="SUBTITLE_MAX_LENGTH"
        data-testid="event-form-subtitle"
      />
    </FormField>
    <FormField
      v-slot="{ control }"
      for="event-categories"
      :label="$t('pages.admin.events.form.fields.categories')"
      :invalid="invalid('categoryIds')"
      :error="errors.categoryIds"
      error-testid="event-form-categories-error"
    >
      <div class="form__cats">
        <el-select
          :id="control.id"
          v-model="draft.categoryIds"
          multiple
          filterable
          collapse-tags
          collapse-tags-tooltip
          :placeholder="$t('pages.admin.events.form.categoriesPlaceholder')"
          class="form__cats-select"
          :class="control.class"
          data-testid="event-form-categories"
        >
          <el-option
            v-for="category in categoryOptions"
            :key="category.id"
            :label="category.name"
            :value="category.id"
            :data-testid="`option-${category.id}`"
          />
        </el-select>
        <ActionButton
          :label="$t('pages.admin.events.form.newCategory')"
          icon="plus"
          type="primary"
          text
          size="small"
          data-testid="event-form-new-category"
          @click="categoryDialogVisible = true"
        />
      </div>
    </FormField>
    <FormField
      for="event-terms-documents"
      :label="$t('pages.admin.events.form.fields.termsDocuments')"
    >
      <el-select
        id="event-terms-documents"
        v-model="draft.termsDocumentIds"
        multiple
        filterable
        collapse-tags
        collapse-tags-tooltip
        :placeholder="$t('pages.admin.events.form.termsPlaceholder')"
        data-testid="event-form-terms"
      >
        <el-option
          v-for="document in termsOptions"
          :key="document.id"
          :label="document.name"
          :value="document.id"
          :data-testid="`option-${document.id}`"
        />
      </el-select>
      <small class="ca-field__hint">{{ $t('pages.admin.events.form.hints.termsDocuments') }}</small>
      <ul v-if="draft.termsDocumentIds.length" class="form__terms-list">
        <li
          v-for="id in draft.termsDocumentIds"
          :key="id"
          class="form__terms-row"
          data-testid="event-form-terms-row"
        >
          <span class="form__terms-name">{{ termsName(id) }}</span>
          <span v-if="consentIds.has(id)" class="form__terms-consent">{{
            $t('pages.admin.events.form.termsConsentOptional')
          }}</span>
          <el-switch
            v-else
            :model-value="draft.termsRequired[id] ?? true"
            :active-text="$t('pages.admin.events.form.termsRequired')"
            data-testid="event-form-terms-required"
            @update:model-value="(value: string | number | boolean) => setTermsRequired(id, value)"
          />
        </li>
      </ul>
    </FormField>
    <FormField
      v-slot="{ invalid: refused }"
      :label="$t('common.description')"
      :error="errors.description"
    >
      <RichTextEditor
        v-model="draft.description"
        :label="$t('common.description')"
        :upload="uploadFileRequest"
        :invalid="refused"
        data-testid="event-form-description"
      />
    </FormField>
    <div class="ca-form-grid">
      <FormField
        v-slot="{ control }"
        for="event-start"
        :label="$t('pages.admin.events.form.fields.eventStart')"
        :invalid="invalid('startsAt')"
        :error="errors.startsAt"
        error-testid="event-form-starts-on-error"
        data-testid="event-form-starts-on"
      >
        <el-date-picker
          :id="control.id"
          v-model="draft.startsAt"
          type="date"
          :format="DATE_FORMAT"
          :class="control.class"
        />
      </FormField>
      <FormField
        v-slot="{ control }"
        for="event-end"
        :label="$t('pages.admin.events.form.fields.eventEnd')"
        :invalid="invalid('endsAt')"
        :error="errors.endsAt"
        error-testid="event-form-ends-on-error"
        data-testid="event-form-ends-on"
      >
        <el-date-picker
          :id="control.id"
          v-model="draft.endsAt"
          type="date"
          :format="DATE_FORMAT"
          :disabled-date="(date: Date) => isDayBefore(date, draft.startsAt)"
          :class="control.class"
        />
      </FormField>
    </div>
    <FormField
      v-slot="{ control }"
      for="event-early-signup-start"
      :label="$t('pages.admin.events.form.fields.earlySignupStart')"
      :invalid="invalid('earlySignupStartsAt')"
      :error="errors.earlySignupStartsAt"
      error-testid="event-form-early-signup-from-error"
      :hint="$t('pages.admin.events.form.hints.earlySignupStart')"
      data-testid="event-form-early-signup-from"
    >
      <el-date-picker
        :id="control.id"
        v-model="draft.earlySignupStartsAt"
        type="datetime"
        clearable
        :format="DATE_TIME_FORMAT"
        :disabled-date="(date: Date) => isDayAfter(date, draft.signupStartsAt)"
        :class="control.class"
      />
    </FormField>
    <div class="ca-form-grid">
      <FormField
        v-slot="{ control }"
        for="event-signup-start"
        :label="$t('pages.admin.events.form.fields.signupStart')"
        :invalid="invalid('signupStartsAt')"
        :error="errors.signupStartsAt"
        error-testid="event-form-signup-from-error"
        data-testid="event-form-signup-from"
      >
        <el-date-picker
          :id="control.id"
          v-model="draft.signupStartsAt"
          type="datetime"
          :format="DATE_TIME_FORMAT"
          :class="control.class"
        />
      </FormField>
      <FormField
        v-slot="{ control }"
        for="event-signup-end"
        :label="$t('pages.admin.events.form.fields.signupEnd')"
        :invalid="invalid('signupEndsAt')"
        :error="errors.signupEndsAt"
        error-testid="event-form-signup-until-error"
        data-testid="event-form-signup-until"
      >
        <el-date-picker
          :id="control.id"
          v-model="draft.signupEndsAt"
          type="datetime"
          :format="DATE_TIME_FORMAT"
          :disabled-date="
            (date: Date) =>
              isDayBefore(date, draft.signupStartsAt) || isDayAfter(date, draft.endsAt)
          "
          :class="control.class"
        />
      </FormField>
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
  </FormDialog>

  <NewCategoryDialog v-model:visible="categoryDialogVisible" @created="selectCategory" />
</template>

<style scoped>
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
</style>
