<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue'
import { useToast } from 'primevue/usetoast'
import Dialog from 'primevue/dialog'
import InputText from 'primevue/inputtext'
import Password from 'primevue/password'
import Select from 'primevue/select'

import { useAccount } from '@/modules/account/presentation/composables/useAccount'
import type { UpdateProfileInput } from '@/modules/account/domain/value-objects/account-inputs'
import BaseButton from '@/shared/ui/components/BaseButton.vue'
import { getErrorMessage } from '@/shared/utils/api-error'
import { formatDate, toDateInput } from '@/shared/utils/format'

const toast = useToast()
const { profile, adultRoles, updateProfile, changeOwnRole, changePassword } = useAccount()

const user = computed(() => profile.data.value ?? null)
const roleNames = computed(() =>
  (user.value?.roles ?? []).map((role) => role.name ?? '').filter((name) => name.length > 0),
)

const editVisible = ref(false)
const editForm = reactive<{
  firstName: string
  lastName: string
  email: string
  phone: string
  birthDate: string
}>({ firstName: '', lastName: '', email: '', phone: '', birthDate: '' })

function openEdit(): void {
  editForm.firstName = user.value?.firstName ?? ''
  editForm.lastName = user.value?.lastName ?? ''
  editForm.email = user.value?.email ?? ''
  editForm.phone = user.value?.phone ?? ''
  editForm.birthDate = toDateInput(user.value?.birthDate)
  editVisible.value = true
}

function saveEdit(): void {
  const request: UpdateProfileInput = {
    firstName: editForm.firstName.trim(),
    lastName: editForm.lastName.trim(),
    email: editForm.email.trim(),
    phone: editForm.phone.trim(),
    birthDate: editForm.birthDate,
  }
  updateProfile.mutate(request, {
    onSuccess: () => {
      editVisible.value = false
      toast.add({
        severity: 'success',
        summary: 'Datos actualizados',
        detail: 'Tu información se ha guardado.',
        life: 3000,
      })
    },
    onError: (error) =>
      toast.add({
        severity: 'error',
        summary: 'Error',
        detail: getErrorMessage(error),
        life: 4000,
      }),
  })
}

const roleVisible = ref(false)
const selectedRoleId = ref('')

watch(
  () => [user.value, adultRoles.data.value] as const,
  () => {
    const options = adultRoles.data.value ?? []
    selectedRoleId.value =
      (user.value?.roles ?? []).find((role) => options.some((option) => option.id === role.id))
        ?.id ?? ''
  },
  { immediate: true },
)

function saveRole(): void {
  if (!selectedRoleId.value) return
  changeOwnRole.mutate(selectedRoleId.value, {
    onSuccess: () => {
      roleVisible.value = false
      toast.add({
        severity: 'success',
        summary: 'Rol actualizado',
        detail: 'Tu rol se ha cambiado.',
        life: 3000,
      })
    },
    onError: (error) =>
      toast.add({
        severity: 'error',
        summary: 'Error',
        detail: getErrorMessage(error),
        life: 4000,
      }),
  })
}

const passwordVisible = ref(false)
const passwordForm = reactive({ current: '', next: '', confirm: '' })
const passwordError = ref('')

function openPassword(): void {
  passwordForm.current = ''
  passwordForm.next = ''
  passwordForm.confirm = ''
  passwordError.value = ''
  passwordVisible.value = true
}

function savePassword(): void {
  passwordError.value = ''
  if (passwordForm.next.length < 8) {
    passwordError.value = 'La nueva contraseña debe tener al menos 8 caracteres.'
    return
  }
  if (passwordForm.next !== passwordForm.confirm) {
    passwordError.value = 'Las contraseñas no coinciden.'
    return
  }
  changePassword.mutate(
    { currentPassword: passwordForm.current, newPassword: passwordForm.next },
    {
      onSuccess: () => {
        passwordVisible.value = false
        toast.add({
          severity: 'success',
          summary: 'Contraseña cambiada',
          detail: 'Tu contraseña se ha actualizado.',
          life: 3000,
        })
      },
      onError: () => {
        passwordError.value = 'No se pudo cambiar. Revisa tu contraseña actual.'
      },
    },
  )
}
</script>

<template>
  <section class="acc-card">
    <div class="acc-card__head">
      <h2 class="acc-card__title">Mis datos</h2>
      <div class="acc-card__actions">
        <BaseButton variant="ghost" @click="openEdit">Editar datos</BaseButton>
        <BaseButton variant="ghost" @click="roleVisible = true">Cambiar rol</BaseButton>
        <BaseButton variant="ghost" @click="openPassword">Cambiar contraseña</BaseButton>
      </div>
    </div>

    <p v-if="profile.isLoading.value" class="acc-card__state">Cargando…</p>
    <dl v-else-if="user" class="acc-info">
      <div class="acc-info__row">
        <dt>Nombre</dt>
        <dd>{{ user.firstName }} {{ user.lastName }}</dd>
      </div>
      <div class="acc-info__row">
        <dt>Correo</dt>
        <dd>{{ user.email || '—' }}</dd>
      </div>
      <div class="acc-info__row">
        <dt>Teléfono</dt>
        <dd>{{ user.phone || '—' }}</dd>
      </div>
      <div class="acc-info__row">
        <dt>Fecha de nacimiento</dt>
        <dd>{{ formatDate(user.birthDate) }}</dd>
      </div>
      <div class="acc-info__row">
        <dt>Rol</dt>
        <dd>{{ roleNames.join(', ') || '—' }}</dd>
      </div>
      <div class="acc-info__row">
        <dt>Estado</dt>
        <dd>{{ user.statusName || '—' }}</dd>
      </div>
    </dl>

    <Dialog
      v-model:visible="editVisible"
      modal
      header="Editar mis datos"
      :style="{ width: '90vw', maxWidth: '520px' }"
    >
      <form class="acc-form" @submit.prevent="saveEdit">
        <div class="acc-form__grid">
          <div class="acc-form__field">
            <label for="p-firstname">Nombre</label>
            <InputText id="p-firstname" v-model="editForm.firstName" required fluid />
          </div>
          <div class="acc-form__field">
            <label for="p-lastname">Apellidos</label>
            <InputText id="p-lastname" v-model="editForm.lastName" required fluid />
          </div>
          <div class="acc-form__field">
            <label for="p-email">Correo</label>
            <InputText id="p-email" v-model="editForm.email" type="email" required fluid />
          </div>
          <div class="acc-form__field">
            <label for="p-phone">Teléfono</label>
            <InputText id="p-phone" v-model="editForm.phone" type="tel" required fluid />
          </div>
          <div class="acc-form__field">
            <label for="p-dob">Fecha de nacimiento</label>
            <input id="p-dob" v-model="editForm.birthDate" type="date" class="acc-date" required />
          </div>
        </div>
        <div class="acc-form__actions">
          <BaseButton variant="link" type="button" @click="editVisible = false"
            >Cancelar</BaseButton
          >
          <BaseButton variant="primary" type="submit" :loading="updateProfile.isPending.value">
            Guardar
          </BaseButton>
        </div>
      </form>
    </Dialog>

    <Dialog
      v-model:visible="roleVisible"
      modal
      header="Cambiar mi rol"
      :style="{ width: '90vw', maxWidth: '460px' }"
    >
      <div class="acc-form__field">
        <label for="p-role">Rol</label>
        <Select
          input-id="p-role"
          v-model="selectedRoleId"
          :options="[...(adultRoles.data.value ?? [])]"
          option-label="name"
          option-value="id"
          placeholder="Selecciona un rol"
          fluid
        />
      </div>
      <div class="acc-form__actions">
        <BaseButton variant="link" type="button" @click="roleVisible = false">Cancelar</BaseButton>
        <BaseButton
          variant="primary"
          :disabled="!selectedRoleId"
          :loading="changeOwnRole.isPending.value"
          @click="saveRole"
        >
          Guardar
        </BaseButton>
      </div>
    </Dialog>

    <Dialog
      v-model:visible="passwordVisible"
      modal
      header="Cambiar contraseña"
      :style="{ width: '90vw', maxWidth: '460px' }"
    >
      <form class="acc-form" @submit.prevent="savePassword">
        <div class="acc-form__field">
          <label for="p-cur">Contraseña actual</label>
          <Password
            input-id="p-cur"
            v-model="passwordForm.current"
            :feedback="false"
            toggle-mask
            required
            fluid
          />
        </div>
        <div class="acc-form__field">
          <label for="p-new">Nueva contraseña</label>
          <Password
            input-id="p-new"
            v-model="passwordForm.next"
            :feedback="false"
            toggle-mask
            required
            fluid
          />
        </div>
        <div class="acc-form__field">
          <label for="p-conf">Repite la nueva contraseña</label>
          <Password
            input-id="p-conf"
            v-model="passwordForm.confirm"
            :feedback="false"
            toggle-mask
            required
            fluid
          />
        </div>
        <p v-if="passwordError" class="acc-form__error">{{ passwordError }}</p>
        <div class="acc-form__actions">
          <BaseButton variant="link" type="button" @click="passwordVisible = false">
            Cancelar
          </BaseButton>
          <BaseButton variant="primary" type="submit" :loading="changePassword.isPending.value">
            Guardar
          </BaseButton>
        </div>
      </form>
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

.acc-card__actions {
  display: flex;
  gap: 8px;
  flex-wrap: wrap;
}

.acc-card__state {
  color: var(--ca-text-dim);
  font-family: var(--ca-font-mono);
}

.acc-info {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 14px 24px;
}

.acc-info__row {
  display: flex;
  flex-direction: column;
  gap: 3px;
}

.acc-info__row dt {
  font-size: 12px;
  color: var(--ca-text-dim);
}

.acc-info__row dd {
  margin: 0;
  font-weight: 600;
  color: var(--ca-text);
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

.acc-form__error {
  color: var(--ca-amber);
  font-size: 13.5px;
  margin: 0 0 10px;
}

@media (max-width: 620px) {
  .acc-info,
  .acc-form__grid {
    grid-template-columns: 1fr;
  }
}
</style>
