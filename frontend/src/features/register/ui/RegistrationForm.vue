<script setup lang="ts">
import { computed, ref } from 'vue'

import { createEmptyMinor, type RegistrationForm } from '../model/registration-form'
import { genderOptions } from '@/entities/user'
import { BaseButton } from '@/shared/ui'
import { todayIso, yearsAgoIso } from '@/shared/lib'

const props = defineProps<{
  form: RegistrationForm
  isSubmitting: boolean
}>()

const emit = defineEmits<{ submit: []; back: [] }>()

const model = props.form

const submitted = ref(false)
const confirmTouched = ref(false)
const emailValid = computed(() => /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(model.email.trim()))
const passwordTooShort = computed(() => model.password.length < 8)
const passwordsMismatch = computed(() => model.confirmPassword !== model.password)
const showMismatch = computed(
  () => (submitted.value || confirmTouched.value) && passwordsMismatch.value,
)

const isValid = computed(() => {
  if (!model.firstName.trim() || !model.lastName.trim()) return false
  if (!emailValid.value || !model.phone.trim()) return false
  if (passwordTooShort.value || passwordsMismatch.value) return false
  if (!model.dateOfBirth || !model.gender) return false
  return model.minors.every(
    (minor) => minor.firstName.trim() && minor.lastName.trim() && minor.dateOfBirth && minor.gender,
  )
})

const genders = genderOptions()

function onSubmit(): void {
  submitted.value = true
  if (!isValid.value) return
  emit('submit')
}

const maxBirthDateIso = todayIso()
const adultThresholdIso = yearsAgoIso(18)

function addMinor(): void {
  model.minors.push(createEmptyMinor())
}

function removeMinor(index: number): void {
  model.minors.splice(index, 1)
}
</script>

<template>
  <div class="reg">
    <div class="reg__head">
      <BaseButton variant="link" @click="emit('back')">{{
        $t('features.register.back')
      }}</BaseButton>
    </div>

    <form class="reg__form" @submit.prevent="onSubmit">
      <h2 class="reg__section-title">{{ $t('features.register.form.yourData') }}</h2>
      <div class="reg__grid">
        <div class="reg__field">
          <label class="reg__label" for="reg-firstname">{{ $t('common.firstName') }}</label>
          <el-input
            id="reg-firstname"
            v-model="model.firstName"
            :maxlength="120"
            :class="{ 'ca-invalid': submitted && !model.firstName.trim() }"
            required
          />
        </div>
        <div class="reg__field">
          <label class="reg__label" for="reg-lastname">{{ $t('common.lastName') }}</label>
          <el-input
            id="reg-lastname"
            v-model="model.lastName"
            :maxlength="120"
            :class="{ 'ca-invalid': submitted && !model.lastName.trim() }"
            required
          />
        </div>
        <div class="reg__field">
          <label class="reg__label" for="reg-email">{{ $t('common.email') }}</label>
          <el-input
            id="reg-email"
            v-model="model.email"
            type="email"
            :maxlength="256"
            :class="{ 'ca-invalid': submitted && !emailValid }"
            required
          />
          <small v-if="submitted && !emailValid" class="reg__error">{{
            $t('validation.emailInvalid')
          }}</small>
        </div>
        <div class="reg__field">
          <label class="reg__label" for="reg-phone">{{ $t('common.phone') }}</label>
          <el-input
            id="reg-phone"
            v-model="model.phone"
            type="tel"
            :maxlength="40"
            :class="{ 'ca-invalid': submitted && !model.phone.trim() }"
            required
          />
        </div>
        <div class="reg__field">
          <label class="reg__label" for="reg-password">{{ $t('common.password') }}</label>
          <el-input
            id="reg-password"
            v-model="model.password"
            type="password"
            show-password
            :maxlength="128"
            :class="{ 'ca-invalid': submitted && passwordTooShort }"
            required
          />
          <small v-if="submitted && passwordTooShort" class="reg__error">{{
            $t('validation.passwordMin')
          }}</small>
        </div>
        <div class="reg__field">
          <label class="reg__label" for="reg-password-confirm">{{
            $t('common.confirmPassword')
          }}</label>
          <el-input
            id="reg-password-confirm"
            v-model="model.confirmPassword"
            type="password"
            show-password
            :maxlength="128"
            :class="{ 'ca-invalid': showMismatch }"
            required
            @blur="confirmTouched = true"
          />
          <small v-if="showMismatch" class="reg__error">{{
            $t('validation.passwordsMismatch')
          }}</small>
        </div>
        <div class="reg__field">
          <label class="reg__label" for="reg-dob">{{ $t('common.birthDate') }}</label>
          <input
            id="reg-dob"
            v-model="model.dateOfBirth"
            type="date"
            class="reg__date"
            :max="adultThresholdIso"
            required
          />
        </div>
        <div class="reg__field">
          <label class="reg__label" for="reg-gender">{{ $t('common.gender') }}</label>
          <el-select
            id="reg-gender"
            v-model="model.gender"
            :class="{ 'ca-invalid': submitted && !model.gender }"
          >
            <el-option
              v-for="option in genders"
              :key="option.value"
              :label="option.label"
              :value="option.value"
            />
          </el-select>
          <small v-if="submitted && !model.gender" class="reg__error">{{
            $t('validation.genderRequired')
          }}</small>
        </div>
      </div>

      <div class="reg__minors">
        <div class="reg__minors-head">
          <h2 class="reg__section-title">{{ $t('features.register.form.minorsTitle') }}</h2>
          <BaseButton variant="ghost" type="button" @click="addMinor">{{
            $t('features.register.form.addMinor')
          }}</BaseButton>
        </div>
        <p class="reg__minors-note">
          {{ $t('features.register.form.minorsNote') }}
        </p>

        <transition-group name="reg-fade" tag="div">
          <fieldset v-for="(minor, index) in model.minors" :key="minor.key" class="reg__minor">
            <legend class="reg__minor-legend">
              {{ $t('features.register.form.minorLegend', { n: index + 1 }) }}
            </legend>
            <button
              type="button"
              class="reg__minor-remove"
              :aria-label="$t('features.register.form.removeMinor')"
              :title="$t('features.register.form.removeMinor')"
              @click="removeMinor(index)"
            >
              ✕
            </button>
            <div class="reg__grid">
              <div class="reg__field">
                <label class="reg__label" :for="`minor-firstname-${index}`">{{
                  $t('common.firstName')
                }}</label>
                <el-input
                  :id="`minor-firstname-${index}`"
                  v-model="minor.firstName"
                  :maxlength="120"
                  :class="{ 'ca-invalid': submitted && !minor.firstName.trim() }"
                  required
                />
              </div>
              <div class="reg__field">
                <label class="reg__label" :for="`minor-lastname-${index}`">{{
                  $t('common.lastName')
                }}</label>
                <el-input
                  :id="`minor-lastname-${index}`"
                  v-model="minor.lastName"
                  :maxlength="120"
                  :class="{ 'ca-invalid': submitted && !minor.lastName.trim() }"
                  required
                />
              </div>
              <div class="reg__field">
                <label class="reg__label" :for="`minor-dob-${index}`">{{
                  $t('common.birthDate')
                }}</label>
                <input
                  :id="`minor-dob-${index}`"
                  v-model="minor.dateOfBirth"
                  type="date"
                  class="reg__date"
                  :min="adultThresholdIso"
                  :max="maxBirthDateIso"
                  required
                />
              </div>
              <div class="reg__field">
                <label class="reg__label" :for="`minor-gender-${index}`">{{
                  $t('common.gender')
                }}</label>
                <el-select
                  :id="`minor-gender-${index}`"
                  v-model="minor.gender"
                  :class="{ 'ca-invalid': submitted && !minor.gender }"
                >
                  <el-option
                    v-for="option in genders"
                    :key="option.value"
                    :label="option.label"
                    :value="option.value"
                  />
                </el-select>
                <small v-if="submitted && !minor.gender" class="reg__error">{{
                  $t('validation.genderRequired')
                }}</small>
              </div>
            </div>
          </fieldset>
        </transition-group>
      </div>

      <BaseButton type="submit" variant="primary" block :loading="isSubmitting" class="reg__submit">
        {{ $t('features.register.form.submit') }}
      </BaseButton>
    </form>
  </div>
</template>

<style scoped>
.reg__head {
  margin-bottom: 16px;
}

.reg__form {
  background: var(--ca-bg-elevated);
  border: 1px solid var(--ca-border-strong);
  border-radius: 18px;
  padding: 30px;
}

.reg__section-title {
  font-family: var(--ca-font-display);
  font-weight: 600;
  font-size: 18px;
  color: var(--ca-text-bright);
  margin-bottom: 16px;
}

.reg__grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 16px;
}

.reg__field {
  display: flex;
  flex-direction: column;
}

.reg__label {
  font-size: 13px;
  font-weight: 600;
  color: var(--ca-text-muted);
  margin-bottom: 6px;
}

.reg__date {
  width: 100%;
  background: var(--ca-input-bg);
  color: var(--ca-text);
  border: 1px solid var(--ca-border-strong);
  border-radius: 10px;
  padding: 12px 14px;
  font-family: inherit;
  font-size: 15px;
  outline: none;
  color-scheme: dark;
}

.reg__date:focus {
  border-color: var(--ca-orange);
}

.reg__error {
  margin-top: 6px;
  font-size: 12.5px;
  color: var(--ca-danger-ink);
}

.ca-invalid {
  --el-input-border-color: var(--ca-danger);
  --el-input-hover-border-color: var(--ca-danger);
  --el-input-focus-border-color: var(--ca-danger);
}

.ca-invalid :deep(.el-select__wrapper) {
  box-shadow: 0 0 0 1px var(--ca-danger) inset;
}

.reg__minors {
  margin-top: 28px;
  padding-top: 24px;
  border-top: 1px solid var(--ca-border);
}

.reg__minors-head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 16px;
}

.reg__minors-note {
  margin: 6px 0 16px;
  font-size: 13.5px;
  line-height: 1.5;
  color: var(--ca-text-dim);
}

.reg__minor {
  position: relative;
  margin-top: 16px;
  padding: 22px;
  border: 1px solid var(--ca-border-strong);
  border-radius: 14px;
  background: var(--ca-surface);
}

.reg__minor-legend {
  font-family: var(--ca-font-display);
  font-weight: 600;
  font-size: 14px;
  color: var(--ca-text);
  padding: 0 8px;
}

.reg__minor-remove {
  position: absolute;
  top: 14px;
  right: 14px;
  width: 28px;
  height: 28px;
  border-radius: 8px;
  border: 1px solid var(--ca-border-strong);
  background: var(--ca-bg-elevated);
  color: var(--ca-text-muted);
  cursor: pointer;
  font-size: 13px;
  line-height: 1;
}

.reg__minor-remove:hover {
  color: var(--ca-text-bright);
  border-color: var(--ca-danger);
}

.reg__submit {
  margin-top: 26px;
  width: 100%;
}

.reg-fade-enter-active,
.reg-fade-leave-active {
  transition:
    opacity 0.2s ease,
    transform 0.2s ease;
}

.reg-fade-enter-from,
.reg-fade-leave-to {
  opacity: 0;
  transform: translateY(-6px);
}

@media (max-width: 620px) {
  .reg__grid {
    grid-template-columns: 1fr;
  }
}
</style>
