<script setup lang="ts">
import { computed } from 'vue'
import InputText from 'primevue/inputtext'
import Password from 'primevue/password'
import Select from 'primevue/select'

import {
  createEmptyMinor,
  type RegistrationForm,
} from '@/modules/registration/domain/value-objects/registration-form'
import type { UserTypeResponse } from '@/shared/api/generated/models'
import BaseButton from '@/shared/ui/components/BaseButton.vue'

const props = defineProps<{
  form: RegistrationForm
  adultRoles: UserTypeResponse[]
  minorRoles: UserTypeResponse[]
  isSubmitting: boolean
}>()

const emit = defineEmits<{ submit: []; back: [] }>()

const model = props.form

const todayIso = computed(() => new Date().toISOString().slice(0, 10))
// Latest birth date that still makes someone an adult (today minus 18 years).
const adultThresholdIso = computed(() => {
  const d = new Date()
  d.setFullYear(d.getFullYear() - 18)
  return d.toISOString().slice(0, 10)
})

const adultRoleDescription = computed(
  () => props.adultRoles.find((role) => role.id === model.roleId)?.description ?? '',
)

function minorRoleDescription(roleId: string): string {
  return props.minorRoles.find((role) => role.id === roleId)?.description ?? ''
}

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
      <BaseButton variant="link" @click="emit('back')">← Volver</BaseButton>
    </div>

    <form class="reg__form" @submit.prevent="emit('submit')">
      <h2 class="reg__section-title">Tus datos</h2>
      <div class="reg__grid">
        <div class="reg__field">
          <label class="reg__label" for="reg-firstname">Nombre</label>
          <InputText id="reg-firstname" v-model="model.firstName" required fluid />
        </div>
        <div class="reg__field">
          <label class="reg__label" for="reg-lastname">Apellidos</label>
          <InputText id="reg-lastname" v-model="model.lastName" required fluid />
        </div>
        <div class="reg__field">
          <label class="reg__label" for="reg-email">Correo</label>
          <InputText id="reg-email" v-model="model.email" type="email" required fluid />
        </div>
        <div class="reg__field">
          <label class="reg__label" for="reg-phone">Teléfono</label>
          <InputText id="reg-phone" v-model="model.phone" type="tel" required fluid />
        </div>
        <div class="reg__field">
          <label class="reg__label" for="reg-password">Contraseña</label>
          <Password
            input-id="reg-password"
            v-model="model.password"
            :feedback="false"
            toggle-mask
            required
            fluid
          />
        </div>
        <div class="reg__field">
          <label class="reg__label" for="reg-dob">Fecha de nacimiento</label>
          <input
            id="reg-dob"
            v-model="model.dateOfBirth"
            type="date"
            class="reg__date"
            :max="adultThresholdIso"
            required
          />
        </div>
        <div class="reg__field reg__field--full">
          <label class="reg__label" for="reg-role">¿Cómo quieres participar?</label>
          <Select
            input-id="reg-role"
            v-model="model.roleId"
            :options="adultRoles"
            option-label="name"
            option-value="id"
            placeholder="Selecciona un rol"
            required
            fluid
          />
          <p v-if="adultRoleDescription" class="reg__role-desc">{{ adultRoleDescription }}</p>
        </div>
      </div>

      <div class="reg__minors">
        <div class="reg__minors-head">
          <h2 class="reg__section-title">Menores a mi cargo</h2>
          <BaseButton variant="ghost" type="button" @click="addMinor">+ Añadir menor</BaseButton>
        </div>
        <p class="reg__minors-note">
          Opcional. Añade a los menores que quieras inscribir; solo necesitamos su nombre, fecha de
          nacimiento y cómo quieren participar.
        </p>

        <transition-group name="reg-fade" tag="div">
          <fieldset v-for="(minor, index) in model.minors" :key="index" class="reg__minor">
            <legend class="reg__minor-legend">Menor {{ index + 1 }}</legend>
            <button
              type="button"
              class="reg__minor-remove"
              aria-label="Quitar menor"
              @click="removeMinor(index)"
            >
              ✕
            </button>
            <div class="reg__grid">
              <div class="reg__field">
                <label class="reg__label" :for="`minor-firstname-${index}`">Nombre</label>
                <InputText
                  :id="`minor-firstname-${index}`"
                  v-model="minor.firstName"
                  required
                  fluid
                />
              </div>
              <div class="reg__field">
                <label class="reg__label" :for="`minor-lastname-${index}`">Apellidos</label>
                <InputText
                  :id="`minor-lastname-${index}`"
                  v-model="minor.lastName"
                  required
                  fluid
                />
              </div>
              <div class="reg__field">
                <label class="reg__label" :for="`minor-dob-${index}`">Fecha de nacimiento</label>
                <input
                  :id="`minor-dob-${index}`"
                  v-model="minor.dateOfBirth"
                  type="date"
                  class="reg__date"
                  :min="adultThresholdIso"
                  :max="todayIso"
                  required
                />
              </div>
              <div class="reg__field">
                <label class="reg__label" :for="`minor-role-${index}`">¿Cómo participa?</label>
                <Select
                  :input-id="`minor-role-${index}`"
                  v-model="minor.roleId"
                  :options="minorRoles"
                  option-label="name"
                  option-value="id"
                  placeholder="Selecciona un rol"
                  required
                  fluid
                />
                <p v-if="minorRoleDescription(minor.roleId)" class="reg__role-desc">
                  {{ minorRoleDescription(minor.roleId) }}
                </p>
              </div>
            </div>
          </fieldset>
        </transition-group>
      </div>

      <BaseButton type="submit" variant="primary" block :loading="isSubmitting" class="reg__submit">
        Completar registro
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

.reg__field--full {
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
  border-color: var(--ca-cyan);
}

.reg__role-desc {
  margin-top: 8px;
  font-size: 13px;
  line-height: 1.5;
  color: var(--ca-text-muted);
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
  border-color: var(--ca-amber);
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
