<script setup lang="ts">
import { computed, reactive, ref } from 'vue'
import { useI18n } from 'vue-i18n'

import { isPasswordTooShort } from '@/entities/account'
import {
  minorBirthDateRange,
  parseDependentPerson,
  parseIndependentPerson,
  PersonFields,
  personProblemKey,
  type PersonField,
  type PersonProblem,
} from '@/entities/user'
import {
  EMAIL_ADDRESS_MAX_LENGTH,
  PASSWORD_MAX_LENGTH,
  PASSWORD_MIN_LENGTH,
  PHONE_MAX_LENGTH,
} from '@/shared/config'
import { toDateOnly } from '@/shared/lib/date'
import { BrandButton } from '@/shared/ui/brand-button'
import { FormField } from '@/shared/ui/form-field'

import { canAddMinor, createEmptyMinor, type RegistrationForm } from '../model/registration-form'
import PrivacyNotice from './PrivacyNotice.vue'

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

const { t } = useI18n()
const model = props.form

const submitted = ref(false)
const touched = reactive({
  email: false,
  password: false,
  confirmPassword: false,
  secondaryPhone: false,
})
const today = toDateOnly(new Date())
const birthDateRange = minorBirthDateRange(today)

const adult = computed(() => parseIndependentPerson(model))
const adultErrors = computed(() => {
  const { problems } = adult.value
  const visible = (field: 'firstName' | 'lastName' | 'gender' | 'phone') =>
    submitted.value ? personMessage(field, problems[field]) : null
  const visibleOnceLeft = (field: 'email' | 'secondaryPhone') =>
    submitted.value || (touched[field] && model[field].trim() !== '')
      ? personMessage(field, problems[field])
      : null
  return {
    firstName: visible('firstName'),
    lastName: visible('lastName'),
    gender: visible('gender'),
    phone: visible('phone'),
    email: visibleOnceLeft('email'),
    secondaryPhone: visibleOnceLeft('secondaryPhone'),
  }
})
const minors = computed(() =>
  model.minors.map((minor) => parseDependentPerson(minor, { today }).problems),
)

function personMessage(field: PersonField, problem: PersonProblem | undefined): string | null {
  return problem ? t(personProblemKey(field, problem)) : null
}

function minorErrors(index: number): Partial<Record<PersonField, string | null>> {
  const problems = minors.value[index]
  if (!submitted.value || !problems) return {}
  return {
    firstName: personMessage('firstName', problems.firstName),
    lastName: personMessage('lastName', problems.lastName),
    gender: personMessage('gender', problems.gender),
    birthDate: personMessage('birthDate', problems.birthDate),
  }
}

const passwordTooShort = computed(() => isPasswordTooShort(model.password))
const showPasswordTooShort = computed(
  () => passwordTooShort.value && (submitted.value || (touched.password && !!model.password)),
)
const passwordsMismatch = computed(() => model.confirmPassword !== model.password)
const showMismatch = computed(
  () => passwordsMismatch.value && (submitted.value || touched.confirmPassword),
)

const isValid = computed(
  () =>
    adult.value.person !== null &&
    !passwordTooShort.value &&
    !passwordsMismatch.value &&
    minors.value.every((problems) => Object.keys(problems).length === 0),
)

function onSubmit(): void {
  submitted.value = true
  if (!isValid.value) return
  emit('submit')
}

function addMinor(): void {
  if (canAddMinor(model)) model.minors.push(createEmptyMinor())
}

function removeMinor(index: number): void {
  model.minors.splice(index, 1)
}
</script>

<template>
  <div class="reg">
    <div class="reg__head">
      <BrandButton variant="back" @click="emit('back')">{{
        $t('pages.register.back')
      }}</BrandButton>
    </div>

    <form class="reg__form" @submit.prevent="onSubmit">
      <section class="reg__section" data-testid="register-your-data">
        <h2 class="reg__section-title">{{ $t('pages.register.form.yourData') }}</h2>
        <div class="ca-form-grid">
          <PersonFields
            v-model:first-name="model.firstName"
            v-model:last-name="model.lastName"
            v-model:national-id="model.nationalId"
            v-model:gender="model.gender"
            id-prefix="reg"
            :errors="adultErrors"
            :show-national-id-errors="submitted"
          />
        </div>
      </section>

      <section class="reg__section" data-testid="register-contact">
        <h2 class="reg__section-title">{{ $t('pages.register.form.contactTitle') }}</h2>
        <div class="ca-form-grid">
          <FormField
            v-slot="{ control }"
            for="reg-phone"
            :label="$t('common.phone')"
            :error="adultErrors.phone"
          >
            <el-input
              v-model="model.phone"
              v-bind="control"
              data-testid="register-phone"
              type="tel"
              inputmode="tel"
              autocomplete="tel"
              :maxlength="PHONE_MAX_LENGTH"
              required
            />
          </FormField>
          <FormField
            v-slot="{ control }"
            for="reg-secondary-phone"
            :label="$t('common.secondaryPhoneOptional')"
            :error="adultErrors.secondaryPhone"
          >
            <el-input
              v-model="model.secondaryPhone"
              v-bind="control"
              type="tel"
              inputmode="tel"
              autocomplete="tel"
              :maxlength="PHONE_MAX_LENGTH"
              @blur="touched.secondaryPhone = true"
            />
          </FormField>
        </div>
      </section>

      <section class="reg__section" data-testid="register-access">
        <h2 class="reg__section-title">{{ $t('pages.register.form.accessTitle') }}</h2>
        <div class="ca-form-grid">
          <FormField
            v-slot="{ control }"
            for="reg-email"
            :label="$t('common.email')"
            :error="adultErrors.email"
            wide
          >
            <el-input
              v-model="model.email"
              v-bind="control"
              data-testid="register-email"
              type="email"
              inputmode="email"
              autocomplete="email"
              autocapitalize="none"
              autocorrect="off"
              spellcheck="false"
              :maxlength="EMAIL_ADDRESS_MAX_LENGTH"
              required
              @blur="touched.email = true"
            />
          </FormField>
          <FormField
            v-slot="{ control }"
            for="reg-password"
            :label="$t('common.password')"
            :error="
              showPasswordTooShort
                ? $t('validation.passwordMin', { min: PASSWORD_MIN_LENGTH })
                : null
            "
            error-testid="register-password-error"
          >
            <el-input
              v-model="model.password"
              v-bind="control"
              data-testid="register-password"
              type="password"
              show-password
              autocomplete="new-password"
              :maxlength="PASSWORD_MAX_LENGTH"
              required
              @blur="touched.password = true"
            />
          </FormField>
          <FormField
            v-slot="{ control }"
            for="reg-password-confirm"
            :label="$t('common.confirmPassword')"
            :error="showMismatch ? $t('validation.passwordsMismatch') : null"
            error-testid="register-password-confirm-error"
          >
            <el-input
              v-model="model.confirmPassword"
              v-bind="control"
              data-testid="register-password-confirm"
              type="password"
              show-password
              autocomplete="new-password"
              :maxlength="PASSWORD_MAX_LENGTH"
              required
              @blur="touched.confirmPassword = true"
            />
          </FormField>
        </div>
      </section>

      <section class="reg__minors">
        <h2 class="reg__section-title">{{ $t('pages.register.form.minorsTitle') }}</h2>
        <p class="reg__minors-note">
          {{ $t('pages.register.form.minorsNote') }}
        </p>

        <transition-group name="reg-fade" tag="div">
          <fieldset
            v-for="(minor, index) in model.minors"
            :key="minor.key"
            class="reg__minor"
            data-testid="register-minor"
          >
            <legend class="reg__minor-legend">
              {{ $t('pages.register.form.minorLegend', { n: index + 1 }) }}
            </legend>
            <button
              type="button"
              class="reg__minor-remove"
              :aria-label="$t('pages.register.form.removeMinor')"
              :title="$t('pages.register.form.removeMinor')"
              @click="removeMinor(index)"
            >
              ✕
            </button>
            <div class="ca-form-grid">
              <PersonFields
                v-model:first-name="minor.firstName"
                v-model:last-name="minor.lastName"
                v-model:birth-date="minor.birthDate"
                v-model:gender="minor.gender"
                :id-prefix="`minor-${index}`"
                :errors="minorErrors(index)"
                :birth-date-range="birthDateRange"
              />
            </div>
          </fieldset>
        </transition-group>

        <BrandButton
          v-if="canAddMinor(model)"
          variant="ghost"
          type="button"
          class="reg__add-minor"
          data-testid="register-add-minor"
          @click="addMinor"
          >{{ $t('pages.register.form.addMinor') }}</BrandButton
        >
      </section>

      <div class="reg__footer">
        <div class="reg__consent">
          <el-checkbox
            id="reg-news-consent"
            v-model="model.newsConsent"
            data-testid="register-news-consent"
            class="ca-checkbox-text reg__consent-label"
          >
            {{ $t('common.newsConsentOption') }}
          </el-checkbox>
        </div>
        <PrivacyNotice />

        <BrandButton
          type="submit"
          variant="primary"
          block
          :loading="isSubmitting"
          class="reg__submit"
          data-testid="register-submit"
        >
          {{ $t('pages.register.form.submit') }}
        </BrandButton>
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
</style>
