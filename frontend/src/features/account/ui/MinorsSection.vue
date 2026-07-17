<script setup lang="ts">
import { computed, reactive, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import Dialog from 'primevue/dialog'
import InputText from 'primevue/inputtext'

import { useAccount } from '../model/useAccount'
import type { AccountChild } from '@/entities/account'
import { BaseButton } from '@/shared/ui'
import { formatDate, toDateInput, todayIso, useCrudFeedback, yearsAgoIso } from '@/shared/lib'

const { t } = useI18n()
const feedback = useCrudFeedback()
const { children, addChild, updateChild, deleteChild } = useAccount()

const items = computed(() => children.data.value ?? [])

const dialogVisible = ref(false)
const mode = ref<'add' | 'edit'>('add')
const editingId = ref<string | null>(null)
const form = reactive({ firstName: '', lastName: '', birthDate: '' })

const maxBirthDateIso = todayIso()
const adultThresholdIso = yearsAgoIso(18)

const saving = computed(() => addChild.isPending.value || updateChild.isPending.value)

function openAdd(): void {
  mode.value = 'add'
  editingId.value = null
  form.firstName = ''
  form.lastName = ''
  form.birthDate = ''
  dialogVisible.value = true
}

function openEdit(child: AccountChild): void {
  mode.value = 'edit'
  editingId.value = child.id ?? null
  form.firstName = child.firstName ?? ''
  form.lastName = child.lastName ?? ''
  form.birthDate = toDateInput(child.birthDate)
  dialogVisible.value = true
}

function notifyError(error: unknown): void {
  feedback.error(error)
}

function save(): void {
  if (mode.value === 'add') {
    addChild.mutate(
      {
        firstName: form.firstName.trim(),
        lastName: form.lastName.trim(),
        birthDate: form.birthDate,
      },
      {
        onSuccess: () => {
          dialogVisible.value = false
          feedback.success(
            t('features.account.minors.addedDetail'),
            t('features.account.minors.addedSummary'),
          )
        },
        onError: notifyError,
      },
    )
    return
  }

  const childId = editingId.value
  if (!childId) return
  updateChild.mutate(
    {
      childId,
      input: {
        firstName: form.firstName.trim(),
        lastName: form.lastName.trim(),
        birthDate: form.birthDate,
      },
    },
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
  <section class="acc-card">
    <div class="acc-card__head">
      <h2 class="acc-card__title">{{ $t('features.account.minors.title') }}</h2>
      <BaseButton variant="primary" @click="openAdd">{{
        $t('features.account.minors.add')
      }}</BaseButton>
    </div>

    <p v-if="children.isLoading.value" class="acc-card__state">{{ $t('common.loading') }}</p>
    <p v-else-if="items.length === 0" class="acc-card__state">
      {{ $t('features.account.minors.empty') }}
    </p>

    <ul v-else class="acc-minors">
      <li v-for="child in items" :key="child.id" class="acc-minor">
        <div class="acc-minor__info">
          <span class="acc-minor__name">{{ child.firstName }} {{ child.lastName }}</span>
          <span class="acc-minor__meta">{{ formatDate(child.birthDate) }}</span>
        </div>
        <div class="acc-minor__actions">
          <BaseButton variant="ghost" @click="openEdit(child)">{{ $t('common.edit') }}</BaseButton>
          <BaseButton variant="link" @click="deleteTarget = child">{{
            $t('common.delete')
          }}</BaseButton>
        </div>
      </li>
    </ul>

    <Dialog
      v-model:visible="dialogVisible"
      modal
      :header="
        mode === 'add'
          ? $t('features.account.minors.addHeader')
          : $t('features.account.minors.editHeader')
      "
      :style="{ width: '90vw', maxWidth: '520px' }"
    >
      <form class="acc-form" @submit.prevent="save">
        <div class="acc-form__grid">
          <div class="acc-form__field">
            <label for="m-firstname">{{ $t('common.firstName') }}</label>
            <InputText id="m-firstname" v-model="form.firstName" :maxlength="120" required fluid />
          </div>
          <div class="acc-form__field">
            <label for="m-lastname">{{ $t('common.lastName') }}</label>
            <InputText id="m-lastname" v-model="form.lastName" :maxlength="120" required fluid />
          </div>
          <div class="acc-form__field">
            <label for="m-dob">{{ $t('common.birthDate') }}</label>
            <input
              id="m-dob"
              v-model="form.birthDate"
              type="date"
              class="acc-date"
              :min="adultThresholdIso"
              :max="maxBirthDateIso"
              required
            />
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
    </Dialog>

    <Dialog
      :visible="deleteTarget !== null"
      modal
      :header="$t('features.account.minors.deleteHeader')"
      :style="{ width: '90vw', maxWidth: '420px' }"
      @update:visible="(value) => !value && (deleteTarget = null)"
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
    </Dialog>
  </section>
</template>

<style scoped>
.acc-card {
  background: var(--ca-bg-elevated);
  border: 1px solid var(--ca-border-strong);
  border-radius: 18px;
  padding: 26px 28px;
}

.acc-card__head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 16px;
  flex-wrap: wrap;
  margin-bottom: 18px;
}

.acc-card__title {
  font-family: var(--ca-font-display);
  font-weight: 700;
  font-size: 20px;
  color: var(--ca-text-bright);
}

.acc-card__state {
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
  color-scheme: dark;
}

.acc-form__actions {
  display: flex;
  justify-content: flex-end;
  gap: 10px;
  margin-top: 8px;
}

.acc-confirm {
  color: var(--ca-text);
  line-height: 1.6;
  margin: 0 0 16px;
}

@media (max-width: 620px) {
  .acc-form__grid {
    grid-template-columns: 1fr;
  }
}
</style>
