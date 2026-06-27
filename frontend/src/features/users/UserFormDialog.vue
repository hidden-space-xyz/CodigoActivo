<script setup lang="ts">
import { reactive, ref, watch } from 'vue'
import Button from 'primevue/button'
import DatePicker from 'primevue/datepicker'
import Dialog from 'primevue/dialog'
import InputText from 'primevue/inputtext'

import type { UpdateUserRequest, UserResponse } from '@/shared/api/generated/models'

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

watch(
  () => props.visible,
  (open) => {
    if (!open) return
    submitted.value = false
    form.firstName = props.user?.firstName ?? ''
    form.lastName = props.user?.lastName ?? ''
    form.email = props.user?.email ?? ''
    form.phone = props.user?.phone ?? ''
    form.birthDate = props.user?.birthDate ? new Date(props.user.birthDate) : null
  },
)

function close(): void {
  emit('update:visible', false)
}

function save(): void {
  submitted.value = true
  if (!form.firstName.trim() || !form.lastName.trim()) return
  const body: UpdateUserRequest = {
    firstName: form.firstName.trim(),
    lastName: form.lastName.trim(),
    email: form.email.trim() ? form.email.trim() : null,
    phone: form.phone.trim() ? form.phone.trim() : null,
  }
  if (form.birthDate) body.birthDate = form.birthDate.toISOString().slice(0, 10)
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
            :invalid="submitted && !form.firstName.trim()"
            fluid
          />
        </div>
        <div class="form__field">
          <label>Apellidos</label>
          <InputText v-model="form.lastName" :invalid="submitted && !form.lastName.trim()" fluid />
        </div>
      </div>
      <div class="form__field">
        <label>Correo</label>
        <InputText v-model="form.email" type="email" fluid />
      </div>
      <div class="form__field">
        <label>Teléfono</label>
        <InputText v-model="form.phone" type="tel" fluid />
      </div>
      <div class="form__field">
        <label>Fecha de nacimiento</label>
        <DatePicker v-model="form.birthDate" date-format="dd/mm/yy" show-icon fluid />
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
</style>
