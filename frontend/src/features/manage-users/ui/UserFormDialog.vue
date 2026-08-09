<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue'
import { AppButton as Button } from '@/shared/ui'

import { genderOptions } from '@/entities/user'
import type { UpdateUserInput, User } from '@/entities/user'
import type { Gender } from '@/shared/api/generated/models'
import { ageFrom, parseDateOnly, toDateOnly } from '@/shared/lib'

const DATE_FORMAT = 'DD/MM/YYYY'

const props = defineProps<{ visible: boolean; user: User | null; saving: boolean }>()

const emit = defineEmits<{
  'update:visible': [value: boolean]
  submit: [body: UpdateUserInput]
}>()

interface UserForm {
  firstName: string
  lastName: string
  email: string
  phone: string
  birthDate: Date | null
  gender: Gender | null
}

const form = reactive<UserForm>({
  firstName: '',
  lastName: '',
  email: '',
  phone: '',
  birthDate: null,
  gender: null,
})
const submitted = ref(false)
const genders = genderOptions()

function disabledBirthDate(date: Date): boolean {
  return date > new Date()
}

const isMinor = computed(() => {
  const age = ageFrom(form.birthDate)
  return age !== null && age < 18
})
const birthDateInvalid = computed(() => !form.birthDate || form.birthDate > new Date())
const emailInvalid = computed(() => {
  const value = form.email.trim()
  return value.length > 0 && !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(value)
})
const contactMissing = computed(() => !isMinor.value && (!form.email.trim() || !form.phone.trim()))

watch(
  () => props.visible,
  (open) => {
    if (!open) return
    submitted.value = false
    form.firstName = props.user?.firstName ?? ''
    form.lastName = props.user?.lastName ?? ''
    form.email = props.user?.email ?? ''
    form.phone = props.user?.phone ?? ''
    form.birthDate = parseDateOnly(props.user?.birthDate)
    form.gender = props.user?.gender ?? null
  },
)

function close(): void {
  emit('update:visible', false)
}

function save(): void {
  submitted.value = true
  if (
    !form.firstName.trim() ||
    !form.lastName.trim() ||
    birthDateInvalid.value ||
    emailInvalid.value ||
    contactMissing.value ||
    !form.gender
  ) {
    return
  }
  const birthDate = form.birthDate
  const gender = form.gender
  if (!birthDate || !gender) return
  const body: UpdateUserInput = {
    firstName: form.firstName.trim(),
    lastName: form.lastName.trim(),
    email: form.email.trim() ? form.email.trim() : null,
    phone: form.phone.trim() ? form.phone.trim() : null,
    birthDate: toDateOnly(birthDate),
    gender,
    parentId: props.user?.parentId ?? null,
  }
  emit('submit', body)
}
</script>

<template>
  <el-dialog
    :model-value="visible"
    :title="$t('features.manageUsers.editHeader')"
    width="min(480px, 92vw)"
    @update:model-value="close"
  >
    <form class="form" @submit.prevent="save">
      <div class="form__row">
        <div class="form__field">
          <label>{{ $t('common.firstName') }}</label>
          <el-input
            v-model="form.firstName"
            :maxlength="120"
            :class="{ 'ca-invalid': submitted && !form.firstName.trim() }"
          />
        </div>
        <div class="form__field">
          <label>{{ $t('common.lastName') }}</label>
          <el-input
            v-model="form.lastName"
            :maxlength="120"
            :class="{ 'ca-invalid': submitted && !form.lastName.trim() }"
          />
        </div>
      </div>
      <div class="form__field">
        <label>{{ $t('common.birthDate') }}</label>
        <el-date-picker
          v-model="form.birthDate"
          type="date"
          :format="DATE_FORMAT"
          :disabled-date="disabledBirthDate"
          :class="{ 'ca-invalid': submitted && birthDateInvalid }"
        />
        <small v-if="submitted && birthDateInvalid" class="form__error">{{
          $t('features.manageUsers.birthDateInvalid')
        }}</small>
      </div>
      <div class="form__field">
        <label>{{ $t('common.gender') }}</label>
        <el-select v-model="form.gender" :class="{ 'ca-invalid': submitted && !form.gender }">
          <el-option
            v-for="option in genders"
            :key="option.value"
            :label="option.label"
            :value="option.value"
          />
        </el-select>
        <small v-if="submitted && !form.gender" class="form__error">{{
          $t('validation.genderRequired')
        }}</small>
      </div>
      <div class="form__field">
        <label
          >{{ $t('common.email')
          }}{{ isMinor ? $t('features.manageUsers.optionalSuffix') : '' }}</label
        >
        <el-input
          v-model="form.email"
          type="email"
          :maxlength="256"
          :class="{
            'ca-invalid': submitted && (emailInvalid || (contactMissing && !form.email.trim())),
          }"
        />
        <small v-if="submitted && emailInvalid" class="form__error">{{
          $t('validation.emailFormat')
        }}</small>
      </div>
      <div class="form__field">
        <label
          >{{ $t('common.phone')
          }}{{ isMinor ? $t('features.manageUsers.optionalSuffix') : '' }}</label
        >
        <el-input
          v-model="form.phone"
          type="tel"
          :maxlength="40"
          :class="{
            'ca-invalid': submitted && contactMissing && !form.phone.trim(),
          }"
        />
        <small v-if="submitted && contactMissing" class="form__error">{{
          $t('features.manageUsers.contactRequired')
        }}</small>
      </div>
    </form>

    <template #footer>
      <Button :label="$t('common.cancel')" text :disabled="saving" @click="close" />
      <Button :label="$t('common.save')" type="primary" :loading="saving" @click="save" />
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

.form__error {
  color: var(--ca-danger-ink);
  font-size: 12.5px;
}

.form :deep(.el-select),
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
  .form__row {
    grid-template-columns: 1fr;
  }
}
</style>
