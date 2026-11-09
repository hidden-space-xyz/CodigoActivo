<script setup lang="ts">
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'

import { useAccount } from '../model/use-account'
import type { AccountChild } from '@/entities/account'
import {
  fullName,
  genderLabelKey,
  minorBirthDateRange,
  PersonFields,
  usePersonForm,
} from '@/entities/user'
import { formatDate } from '@/shared/lib/date'
import { useCrudFeedback } from '@/shared/lib/feedback'
import { BrandButton } from '@/shared/ui/brand-button'

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
          t('pages.account.minors.addedDetail'),
          t('pages.account.minors.addedSummary'),
        )
      },
      onError: notifyError,
    })
    return
  }

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
    t('pages.account.minors.updatedDetail'),
    t('pages.account.minors.updatedSummary'),
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
        t('pages.account.minors.deletedDetail'),
        t('pages.account.minors.deletedSummary'),
      )
    },
    onError: notifyError,
  })
}
</script>

<template>
  <section class="acc-pane" data-testid="minors-section">
    <div class="acc-pane__head">
      <div class="acc-pane__heading">
        <h2 class="acc-pane__title">{{ $t('pages.account.minors.title') }}</h2>
        <p class="acc-pane__lead">{{ $t('pages.account.minors.lead') }}</p>
      </div>
      <BrandButton variant="primary" data-testid="minors-add" @click="openAdd">{{
        $t('pages.account.minors.add')
      }}</BrandButton>
    </div>

    <p v-if="children.isLoading.value" class="acc-pane__state">{{ $t('common.loading') }}</p>
    <p v-else-if="items.length === 0" class="acc-pane__state" data-testid="minors-empty">
      {{ $t('pages.account.minors.empty') }}
    </p>

    <ul v-else class="acc-items">
      <li v-for="child in items" :key="child.id" class="acc-item" data-testid="minors-item">
        <div class="acc-item__info">
          <span class="acc-item__name">{{ fullName(child) }}</span>
          <span class="acc-item__meta">
            {{ formatDate(child.birthDate) }} · {{ $t(genderLabelKey(child.gender)) }}
          </span>
        </div>
        <div class="acc-item__actions">
          <BrandButton variant="ghost" data-testid="minors-item-edit" @click="openEdit(child)">{{
            $t('common.edit')
          }}</BrandButton>
          <BrandButton
            variant="link"
            data-testid="minors-item-delete"
            @click="deleteTarget = child"
            >{{ $t('common.delete') }}</BrandButton
          >
        </div>
      </li>
    </ul>

    <el-dialog
      v-model="dialogVisible"
      :data-testid="`minor-dialog-${mode}`"
      :title="
        mode === 'add'
          ? $t('pages.account.minors.addHeader')
          : $t('pages.account.minors.editHeader')
      "
      width="min(90vw, 520px)"
      :close-on-click-modal="false"
    >
      <form class="acc-form" @submit.prevent="save">
        <div class="ca-form-grid acc-form__grid">
          <PersonFields
            v-model:first-name="draft.firstName"
            v-model:last-name="draft.lastName"
            v-model:birth-date="draft.birthDate"
            v-model:gender="draft.gender"
            id-prefix="m"
            :errors="errors"
            :birth-date-range="{ min: minBirthDateIso, max: birthDateRange.max }"
          />
        </div>
        <p
          v-if="mode === 'add'"
          class="acc-form__declaration"
          data-testid="minor-dialog-declaration"
        >
          {{ $t('pages.account.minors.declaration') }}
        </p>
        <div class="acc-form__actions">
          <BrandButton variant="link" type="button" @click="dialogVisible = false">
            {{ $t('common.cancel') }}
          </BrandButton>
          <BrandButton
            variant="primary"
            type="submit"
            :loading="saving"
            data-testid="minor-dialog-save"
            >{{ $t('common.save') }}</BrandButton
          >
        </div>
      </form>
    </el-dialog>

    <el-dialog
      :model-value="deleteTarget !== null"
      data-testid="minor-delete-dialog"
      :title="$t('pages.account.minors.deleteHeader')"
      width="min(90vw, 420px)"
      :close-on-click-modal="false"
      @update:model-value="(value: boolean) => !value && (deleteTarget = null)"
    >
      <i18n-t keypath="pages.account.minors.deleteConfirm" tag="p" class="acc-confirm">
        <template #name
          ><b>{{ deleteTarget?.firstName }} {{ deleteTarget?.lastName }}</b></template
        >
      </i18n-t>
      <div class="acc-form__actions">
        <BrandButton variant="link" type="button" @click="deleteTarget = null">{{
          $t('common.cancel')
        }}</BrandButton>
        <BrandButton
          variant="primary"
          :loading="deleteChild.isPending.value"
          data-testid="minor-delete-dialog-confirm"
          @click="confirmDelete"
        >
          {{ $t('common.delete') }}
        </BrandButton>
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

.acc-item__actions {
  display: flex;
  gap: 6px;
  flex-shrink: 0;
}

.acc-form__declaration {
  margin: 0;
  font-size: 13px;
  line-height: 1.5;
  color: var(--ca-text-muted);
}

.acc-confirm {
  color: var(--ca-text);
  line-height: 1.6;
  margin: 0 0 16px;
}

@media (max-width: 640px) {
  .acc-item__actions {
    justify-content: flex-end;
  }
}
</style>
