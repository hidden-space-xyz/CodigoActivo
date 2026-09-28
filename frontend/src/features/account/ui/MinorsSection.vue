<script setup lang="ts">
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'

import { useAccount } from '../model/useAccount'
import type { AccountChild } from '@/entities/account'
import { genderLabel, genderOptions, minorBirthDateRange, usePersonForm } from '@/entities/user'
import { BaseButton } from '@/shared/ui'
import { formatDate, useCrudFeedback } from '@/shared/lib'

const { t } = useI18n()
const feedback = useCrudFeedback()
const { children, addChild, updateChild, deleteChild } = useAccount()

const items = computed(() => children.data.value ?? [])

const dialogVisible = ref(false)
const editing = ref<AccountChild | null>(null)
const mode = computed(() => (editing.value ? 'edit' : 'add'))
const { draft, errors, load, submit } = usePersonForm({
  kind: 'dependent',
  stored: () => editing.value,
})
const genders = genderOptions()

const birthDateRange = minorBirthDateRange()
const minBirthDateIso = computed(() => (mode.value === 'add' ? birthDateRange.min : undefined))

const saving = computed(() => addChild.isPending.value || updateChild.isPending.value)

function openAdd(): void {
  editing.value = null
  load()
  dialogVisible.value = true
}

function openEdit(child: AccountChild): void {
  editing.value = child
  load(child)
  dialogVisible.value = true
}

function notifyError(error: unknown): void {
  feedback.error(error)
}

function save(): void {
  const submission = submit()
  if (submission?.kind !== 'dependent') return
  const child = editing.value
  if (!child) {
    addChild.mutate(submission.person, {
      onSuccess: () => {
        dialogVisible.value = false
        feedback.success(
          t('features.account.minors.addedDetail'),
          t('features.account.minors.addedSummary'),
        )
      },
      onError: notifyError,
    })
    return
  }

  if (!child.id) return
  updateChild.mutate(
    { childId: child.id, input: submission.person },
    {
      onSuccess: () => finishEdit(),
      onError: notifyError,
    },
  )
}

function finishEdit(): void {
  dialogVisible.value = false
  feedback.success(
    t('features.account.minors.updatedDetail'),
    t('features.account.minors.updatedSummary'),
  )
}

const deleteTarget = ref<AccountChild | null>(null)

function confirmDelete(): void {
  const id = deleteTarget.value?.id
  if (!id) return
  deleteChild.mutate(id, {
    onSuccess: () => {
      deleteTarget.value = null
      feedback.success(
        t('features.account.minors.deletedDetail'),
        t('features.account.minors.deletedSummary'),
      )
    },
    onError: notifyError,
  })
}
</script>

<template>
  <section class="acc-pane">
    <div class="acc-pane__head">
      <div class="acc-pane__heading">
        <h2 class="acc-pane__title">{{ $t('features.account.minors.title') }}</h2>
        <p class="acc-pane__lead">{{ $t('features.account.minors.lead') }}</p>
      </div>
      <BaseButton variant="primary" @click="openAdd">{{
        $t('features.account.minors.add')
      }}</BaseButton>
    </div>

    <p v-if="children.isLoading.value" class="acc-pane__state">{{ $t('common.loading') }}</p>
    <p v-else-if="items.length === 0" class="acc-pane__state">
      {{ $t('features.account.minors.empty') }}
    </p>

    <ul v-else class="acc-minors">
      <li v-for="child in items" :key="child.id" class="acc-minor">
        <div class="acc-minor__info">
          <span class="acc-minor__name">{{ child.firstName }} {{ child.lastName }}</span>
          <span class="acc-minor__meta">
            {{ formatDate(child.birthDate)
            }}{{ child.gender ? ` · ${genderLabel(child.gender)}` : '' }}
          </span>
        </div>
        <div class="acc-minor__actions">
          <BaseButton variant="ghost" @click="openEdit(child)">{{ $t('common.edit') }}</BaseButton>
          <BaseButton variant="link" @click="deleteTarget = child">{{
            $t('common.delete')
          }}</BaseButton>
        </div>
      </li>
    </ul>

    <el-dialog
      v-model="dialogVisible"
      :title="
        mode === 'add'
          ? $t('features.account.minors.addHeader')
          : $t('features.account.minors.editHeader')
      "
      width="min(90vw, 520px)"
      :close-on-click-modal="false"
    >
      <form class="acc-form" @submit.prevent="save">
        <div class="acc-form__grid">
          <div class="acc-form__field">
            <label for="m-firstname">{{ $t('common.firstName') }}</label>
            <el-input
              id="m-firstname"
              v-model="draft.firstName"
              :maxlength="120"
              :class="{ 'ca-invalid': errors.firstName }"
              required
            />
            <small v-if="errors.firstName" class="acc-form__error">{{ errors.firstName }}</small>
          </div>
          <div class="acc-form__field">
            <label for="m-lastname">{{ $t('common.lastName') }}</label>
            <el-input
              id="m-lastname"
              v-model="draft.lastName"
              :maxlength="120"
              :class="{ 'ca-invalid': errors.lastName }"
              required
            />
            <small v-if="errors.lastName" class="acc-form__error">{{ errors.lastName }}</small>
          </div>
          <div class="acc-form__field">
            <label for="m-dob">{{ $t('common.birthDate') }}</label>
            <input
              id="m-dob"
              v-model="draft.birthDate"
              type="date"
              class="acc-date"
              :class="{ 'acc-date--invalid': errors.birthDate }"
              :min="minBirthDateIso"
              :max="birthDateRange.max"
              required
            />
            <small v-if="errors.birthDate" class="acc-form__error">{{ errors.birthDate }}</small>
          </div>
          <div class="acc-form__field">
            <label for="m-gender">{{ $t('common.gender') }}</label>
            <el-select
              id="m-gender"
              v-model="draft.gender"
              :class="{ 'ca-invalid': errors.gender }"
            >
              <el-option
                v-for="option in genders"
                :key="option.value"
                :label="option.label"
                :value="option.value"
              />
            </el-select>
            <small v-if="errors.gender" class="acc-form__error">{{ errors.gender }}</small>
          </div>
        </div>
        <div class="acc-form__actions">
          <BaseButton variant="link" type="button" @click="dialogVisible = false">
            {{ $t('common.cancel') }}
          </BaseButton>
          <BaseButton variant="primary" type="submit" :loading="saving">{{
            $t('common.save')
          }}</BaseButton>
        </div>
      </form>
    </el-dialog>

    <el-dialog
      :model-value="deleteTarget !== null"
      :title="$t('features.account.minors.deleteHeader')"
      width="min(90vw, 420px)"
      :close-on-click-modal="false"
      @update:model-value="(value: boolean) => !value && (deleteTarget = null)"
    >
      <i18n-t keypath="features.account.minors.deleteConfirm" tag="p" class="acc-confirm">
        <template #name
          ><b>{{ deleteTarget?.firstName }} {{ deleteTarget?.lastName }}</b></template
        >
      </i18n-t>
      <div class="acc-form__actions">
        <BaseButton variant="link" type="button" @click="deleteTarget = null">{{
          $t('common.cancel')
        }}</BaseButton>
        <BaseButton variant="primary" :loading="deleteChild.isPending.value" @click="confirmDelete">
          {{ $t('common.delete') }}
        </BaseButton>
      </div>
    </el-dialog>
  </section>
</template>

<style scoped>
.acc-pane__head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 16px;
  flex-wrap: wrap;
  margin-bottom: 18px;
}

.acc-pane__heading {
  flex: 1;
  min-width: 240px;
}

.acc-pane__title {
  font-family: var(--ca-font-display);
  font-weight: 700;
  font-size: 17px;
  color: var(--ca-text-bright);
}

.acc-pane__lead {
  margin-top: 5px;
  font-size: 14px;
  line-height: 1.5;
  color: var(--ca-text-muted);
  max-width: 62ch;
}

.acc-pane__state {
  color: var(--ca-text-dim);
  font-family: var(--ca-font-mono);
}

.acc-minors {
  list-style: none;
  margin: 0;
  padding: 0;
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.acc-minor {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 16px;
  background: var(--ca-surface);
  border: 1px solid var(--ca-border-soft);
  border-radius: 12px;
  padding: 14px 16px;
}

.acc-minor__info {
  display: flex;
  flex-direction: column;
  gap: 3px;
  min-width: 0;
}

.acc-minor__name {
  font-weight: 600;
  color: var(--ca-text-bright);
}

.acc-minor__meta {
  font-size: 13px;
  color: var(--ca-text-muted);
}

.acc-minor__actions {
  display: flex;
  gap: 6px;
  flex-shrink: 0;
}

.acc-form__grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 14px;
}

.acc-form__field {
  display: flex;
  flex-direction: column;
  gap: 6px;
  margin-bottom: 14px;
}

.acc-form__field label {
  font-size: 13px;
  font-weight: 600;
  color: var(--ca-text-muted);
}

.acc-date {
  width: 100%;
  background: var(--ca-input-bg);
  color: var(--ca-text);
  border: 1px solid var(--ca-border-strong);
  border-radius: 10px;
  padding: 11px 13px;
  font-family: inherit;
  font-size: 15px;
  outline: none;
}

.acc-form__actions {
  display: flex;
  justify-content: flex-end;
  gap: 10px;
  margin-top: 8px;
}

.acc-form__error {
  color: var(--ca-danger-ink);
  font-size: 13.5px;
}

.acc-date--invalid {
  border-color: var(--ca-danger);
}

.ca-invalid {
  --el-input-border-color: var(--ca-danger);
  --el-input-hover-border-color: var(--ca-danger);
  --el-input-focus-border-color: var(--ca-danger);
}

.ca-invalid :deep(.el-select__wrapper) {
  box-shadow: 0 0 0 1px var(--ca-danger) inset;
}

.acc-confirm {
  color: var(--ca-text);
  line-height: 1.6;
  margin: 0 0 16px;
}

@media (max-width: 640px) {
  .acc-form__grid {
    grid-template-columns: 1fr;
  }

  .acc-minor {
    flex-direction: column;
    align-items: stretch;
    gap: 12px;
  }

  .acc-minor__actions {
    justify-content: flex-end;
  }
}
</style>
