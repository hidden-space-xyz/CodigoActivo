<script setup lang="ts">
import { computed, reactive, ref } from 'vue'

import { createEmptyMinor, type RegistrationForm } from '../model/registration-form'
import { genderOptions } from '@/entities/user'
import { BaseButton, NationalIdInput } from '@/shared/ui'
import { isValidNationalId, todayIso, yearsAgoIso } from '@/shared/lib'

const props = defineProps<{
  /** Reactive form state owned by the parent; the component edits it in place. */
  form: RegistrationForm
  /** Shows a loading state on the submit button while registering. */
  isSubmitting: boolean
}>()

const emit = defineEmits<{
  /** Fired once every field validates; the data is already in `form`. */
  submit: []
  /** Fired when the user returns to the age gate. */
  back: []
}>()

const model = props.form

const submitted = ref(false)
const touched = reactive({
  email: false,
  password: false,
  confirmPassword: false,
  secondaryPhone: false,
})
const emailValid = computed(() => /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(model.email.trim()))
const showEmailInvalid = computed(
  () => !emailValid.value && (submitted.value || (touched.email && !!model.email.trim())),
)
const passwordTooShort = computed(() => model.password.length < 12)
const showPasswordTooShort = computed(
  () => passwordTooShort.value && (submitted.value || (touched.password && !!model.password)),
)
const passwordsMismatch = computed(() => model.confirmPassword !== model.password)
const showMismatch = computed(
  () => passwordsMismatch.value && (submitted.value || touched.confirmPassword),
)
const secondaryPhoneRepeated = computed(
  () => !!model.secondaryPhone.trim() && model.secondaryPhone.trim() === model.phone.trim(),
)
const showSecondaryPhoneRepeated = computed(
  () => secondaryPhoneRepeated.value && (submitted.value || touched.secondaryPhone),
)

const isValid = computed(() => {
  if (!model.firstName.trim() || !model.lastName.trim()) return false
  if (!isValidNationalId(model.nationalId) || !model.gender) return false
  if (!model.phone.trim() || secondaryPhoneRepeated.value) return false
  if (!emailValid.value || passwordTooShort.value || passwordsMismatch.value) return false
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
      <BaseButton variant="back" @click="emit('back')">{{
        $t('features.register.back')
      }}</BaseButton>
    </div>

    <form class="reg__form" @submit.prevent="onSubmit">
      <section class="reg__section">
        <h2 class="reg__section-title">{{ $t('features.register.form.yourData') }}</h2>
        <div class="reg__grid">
          <div class="reg__field">
            <label class="reg__label" for="reg-firstname">{{ $t('common.firstName') }}</label>
            <el-input
              id="reg-firstname"
              v-model="model.firstName"
              autocomplete="given-name"
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
              autocomplete="family-name"
              :maxlength="120"
              :class="{ 'ca-invalid': submitted && !model.lastName.trim() }"
              required
            />
          </div>
          <div class="reg__field">
            <label class="reg__label" for="reg-national-id">{{ $t('common.nationalId') }}</label>
            <NationalIdInput
              id="reg-national-id"
              v-model="model.nationalId"
              :show-errors="submitted"
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
      </section>

      <section class="reg__section">
        <h2 class="reg__section-title">{{ $t('features.register.form.contactTitle') }}</h2>
        <div class="reg__grid">
          <div class="reg__field">
            <label class="reg__label" for="reg-phone">{{ $t('common.phone') }}</label>
            <el-input
              id="reg-phone"
              v-model="model.phone"
              type="tel"
              inputmode="tel"
              autocomplete="tel"
              :maxlength="40"
              :class="{ 'ca-invalid': submitted && !model.phone.trim() }"
              required
            />
          </div>
          <div class="reg__field">
            <label class="reg__label" for="reg-secondary-phone">{{
              $t('common.secondaryPhoneOptional')
            }}</label>
            <el-input
              id="reg-secondary-phone"
              v-model="model.secondaryPhone"
              type="tel"
              inputmode="tel"
              autocomplete="tel"
              :maxlength="40"
              :class="{ 'ca-invalid': showSecondaryPhoneRepeated }"
              @blur="touched.secondaryPhone = true"
            />
            <small v-if="showSecondaryPhoneRepeated" class="reg__error">{{
              $t('validation.secondaryPhoneSameAsPrimary')
            }}</small>
          </div>
        </div>
      </section>

      <section class="reg__section">
        <h2 class="reg__section-title">{{ $t('features.register.form.accessTitle') }}</h2>
        <div class="reg__grid">
          <div class="reg__field reg__field--wide">
            <label class="reg__label" for="reg-email">{{ $t('common.email') }}</label>
            <el-input
              id="reg-email"
              v-model="model.email"
              type="email"
              inputmode="email"
              autocomplete="email"
              autocapitalize="none"
              autocorrect="off"
              spellcheck="false"
              :maxlength="256"
              :class="{ 'ca-invalid': showEmailInvalid }"
              required
              @blur="touched.email = true"
            />
            <small v-if="showEmailInvalid" class="reg__error">{{
              $t('validation.emailInvalid')
            }}</small>
          </div>
          <div class="reg__field">
            <label class="reg__label" for="reg-password">{{ $t('common.password') }}</label>
            <el-input
              id="reg-password"
              v-model="model.password"
              type="password"
              show-password
              autocomplete="new-password"
              :maxlength="128"
              :class="{ 'ca-invalid': showPasswordTooShort }"
              required
              @blur="touched.password = true"
            />
            <small v-if="showPasswordTooShort" class="reg__error">{{
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
              autocomplete="new-password"
              :maxlength="128"
              :class="{ 'ca-invalid': showMismatch }"
              required
              @blur="touched.confirmPassword = true"
            />
            <small v-if="showMismatch" class="reg__error">{{
              $t('validation.passwordsMismatch')
            }}</small>
          </div>
        </div>
      </section>

      <section class="reg__minors">
        <h2 class="reg__section-title">{{ $t('features.register.form.minorsTitle') }}</h2>
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

        <BaseButton variant="ghost" type="button" class="reg__add-minor" @click="addMinor">{{
          $t('features.register.form.addMinor')
        }}</BaseButton>
      </section>

      <div class="reg__footer">
        <div class="reg__consent">
          <el-checkbox id="reg-promotional-consent" v-model="model.promotionalConsent" />
          <label for="reg-promotional-consent" class="reg__consent-label">{{
            $t('common.promotionalConsentOption')
          }}</label>
        </div>

        <BaseButton
          type="submit"
          variant="primary"
          block
          :loading="isSubmitting"
          class="reg__submit"
        >
          {{ $t('features.register.form.submit') }}
        </BaseButton>
      </div>
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

.reg__section + .reg__section {
  margin-top: 24px;
}

.reg__section-title {
  font-family: var(--ca-font-display);
  font-weight: 600;
  font-size: 18px;
  color: var(--ca-text-bright);
  margin-bottom: 14px;
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

.reg__field--wide {
  grid-column: 1 / -1;
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

.reg__minors-note {
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

.reg__add-minor {
  margin-top: 16px;
}

.reg__footer {
  margin-top: 28px;
  padding-top: 24px;
  border-top: 1px solid var(--ca-border);
}

.reg__consent {
  display: flex;
  align-items: flex-start;
  gap: 10px;
}

.reg__consent-label {
  font-size: 13.5px;
  line-height: 1.5;
  color: var(--ca-text-muted);
  cursor: pointer;
}

.reg__submit {
  margin-top: 20px;
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

@media (max-width: 640px) {
  .reg__grid {
    grid-template-columns: 1fr;
  }
}
</style>
