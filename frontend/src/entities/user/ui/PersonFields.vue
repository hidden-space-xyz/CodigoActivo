<script setup lang="ts">
import { PERSON_NAME_MAX_LENGTH } from '@/shared/config'
import { FormField } from '@/shared/ui/form-field'

import { GENDERS, genderLabelKey, type Gender } from '../model/gender'
import type { PersonField } from '../model/person'
import NationalIdInput from './NationalIdInput.vue'

/** First name of the person. */
const firstName = defineModel<string>('firstName', { required: true })
/** Last names of the person. */
const lastName = defineModel<string>('lastName', { required: true })
/** Gender chosen, `null` until one is. */
const gender = defineModel<Gender | null>('gender', { required: true })
/** DNI or NIE; its field shows only when it is bound, for people who identify themselves. */
const nationalId = defineModel<string>('nationalId')
/** Birth date as `YYYY-MM-DD`; its field shows only when it is bound, for dependents. */
const birthDate = defineModel<string>('birthDate')

defineProps<{
  /** Start of the control ids, unique on the page, such as `reg` for `reg-firstname`. */
  idPrefix: string
  /** Message under each field while its value is refused. */
  errors: Partial<Record<PersonField, string | null>>
  /** Reveals what is wrong with the DNI/NIE even before it is left, once the form was sent. */
  showNationalIdErrors?: boolean
  /** Earliest and latest birth dates the picker offers, as `YYYY-MM-DD`. */
  birthDateRange?: { readonly min?: string | undefined; readonly max: string } | undefined
}>()
</script>

<template>
  <FormField
    v-slot="{ control }"
    :for="`${idPrefix}-firstname`"
    :label="$t('common.firstName')"
    :error="errors.firstName"
  >
    <el-input
      v-model="firstName"
      v-bind="control"
      data-testid="person-fields-first-name"
      :autocomplete="nationalId === undefined ? 'off' : 'given-name'"
      :maxlength="PERSON_NAME_MAX_LENGTH"
      required
    />
  </FormField>
  <FormField
    v-slot="{ control }"
    :for="`${idPrefix}-lastname`"
    :label="$t('common.lastName')"
    :error="errors.lastName"
  >
    <el-input
      v-model="lastName"
      v-bind="control"
      data-testid="person-fields-last-name"
      :autocomplete="nationalId === undefined ? 'off' : 'family-name'"
      :maxlength="PERSON_NAME_MAX_LENGTH"
      required
    />
  </FormField>
  <FormField
    v-if="nationalId !== undefined"
    :for="`${idPrefix}-national-id`"
    :label="$t('common.nationalId')"
  >
    <NationalIdInput
      :id="`${idPrefix}-national-id`"
      v-model="nationalId"
      data-testid="person-fields-national-id"
      :show-errors="showNationalIdErrors ?? false"
    />
  </FormField>
  <FormField
    v-if="birthDate !== undefined"
    v-slot="{ control, invalid }"
    :for="`${idPrefix}-dob`"
    :label="$t('common.birthDate')"
    :error="errors.birthDate"
  >
    <input
      :id="control.id"
      v-model="birthDate"
      data-testid="person-fields-birth-date"
      type="date"
      class="person-fields__date"
      :class="{ 'person-fields__date--invalid': invalid }"
      :aria-invalid="control['aria-invalid']"
      :aria-describedby="control['aria-describedby']"
      :min="birthDateRange?.min"
      :max="birthDateRange?.max"
      required
    />
  </FormField>
  <FormField
    v-slot="{ control }"
    :for="`${idPrefix}-gender`"
    :label="$t('common.gender')"
    :error="errors.gender"
  >
    <el-select
      :id="control.id"
      v-model="gender"
      data-testid="person-fields-gender"
      :class="control.class"
    >
      <el-option
        v-for="option in GENDERS"
        :key="option"
        :label="$t(genderLabelKey(option))"
        :value="option"
        :data-testid="`option-${option}`"
      />
    </el-select>
  </FormField>
</template>

<style scoped>
.person-fields__date {
  width: 100%;
  background: var(--ca-input-bg);
  color: var(--ca-text);
  border: 1px solid var(--ca-border-strong);
  border-radius: 10px;
  padding: 12px 14px;
  font-family: inherit;
  font-size: 15px;
  outline: none;
}

.person-fields__date:focus {
  border-color: var(--ca-orange);
}

.person-fields__date--invalid {
  border-color: var(--ca-danger);
}
</style>
