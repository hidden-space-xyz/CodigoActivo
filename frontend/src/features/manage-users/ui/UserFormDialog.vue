<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue'
import { AppButton as Button } from '@/shared/ui'
import DatePicker from 'primevue/datepicker'
import Dialog from 'primevue/dialog'
import InputText from 'primevue/inputtext'

import type { UpdateUserRequest, UserResponse } from '@/shared/api/generated/models'
import { ageFrom, parseDateOnly, toDateOnly } from '@/shared/lib'

const props = defineProps<{ visible: boolean; user: UserResponse | null; saving: boolean }>()

const emit = defineEmits<{
  'update:visible': [value: boolean]
  submit: [body: UpdateUserRequest]
}>()

interface UserForm {
  firstName: string
  lastName: string
  email: string
  phone: string
  birthDate: Date | null
}

const form = reactive<UserForm>({
  firstName: '',
  lastName: '',
  email: '',
  phone: '',
  birthDate: null,
})
const submitted = ref(false)
const maxBirthDate = new Date()

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
    contactMissing.value
  ) {
    return
  }
  const birthDate = form.birthDate
  if (!birthDate) return
  const body: UpdateUserRequest = {
    firstName: form.firstName.trim(),
    lastName: form.lastName.trim(),
    email: form.email.trim() ? form.email.trim() : null,
    phone: form.phone.trim() ? form.phone.trim() : null,
    birthDate: toDateOnly(birthDate),
    parentId: props.user?.parentId ?? null,
  }
  emit('submit', body)
}
</script>

<template>
  <Dialog
    :visible="visible"
    modal
    header="Editar usuario"
    :style="{ width: '480px' }"
    @update:visible="close"
  >
    <form class="form" @submit.prevent="save">
      <div class="form__row">
        <div class="form__field">
          <label>Nombre</label>
          <InputText
            v-model="form.firstName"
            :maxlength="120"
            :invalid="submitted && !form.firstName.trim()"
            fluid
          />
        </div>
        <div class="form__field">
          <label>Apellidos</label>
          <InputText
            v-model="form.lastName"
            :maxlength="120"
            :invalid="submitted && !form.lastName.trim()"
            fluid
          />
        </div>
      </div>
      <div class="form__field">
        <label>Fecha de nacimiento</label>
        <DatePicker
          v-model="form.birthDate"
          date-format="dd/mm/yy"
          show-icon
          :max-date="maxBirthDate"
          :invalid="submitted && birthDateInvalid"
          fluid
        />
        <small v-if="submitted && birthDateInvalid" class="form__error"
          >Indica una fecha de nacimiento válida.</small
        >
      </div>
      <div class="form__field">
        <label>Correo{{ isMinor ? ' (opcional)' : '' }}</label>
        <InputText
          v-model="form.email"
          type="email"
          :maxlength="256"
          :invalid="submitted && (emailInvalid || (contactMissing && !form.email.trim()))"
          fluid
        />
        <small v-if="submitted && emailInvalid" class="form__error"
          >El correo no tiene un formato válido.</small
        >
      </div>
      <div class="form__field">
        <label>Teléfono{{ isMinor ? ' (opcional)' : '' }}</label>
        <InputText
          v-model="form.phone"
          type="tel"
          :maxlength="40"
          :invalid="submitted && contactMissing && !form.phone.trim()"
          fluid
        />
        <small v-if="submitted && contactMissing" class="form__error"
          >Los usuarios adultos necesitan correo y teléfono.</small
        >
      </div>
    </form>

    <template #footer>
      <Button label="Cancelar" text severity="secondary" :disabled="saving" @click="close" />
      <Button label="Guardar" :loading="saving" @click="save" />
    </template>
  </Dialog>
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
</style>
