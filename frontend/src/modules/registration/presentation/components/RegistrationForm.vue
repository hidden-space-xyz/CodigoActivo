<script setup lang="ts">
import { computed } from 'vue'
import InputText from 'primevue/inputtext'
import Password from 'primevue/password'

import type { RegistrationForm } from '@/modules/registration/domain/value-objects/registration-form'
import { ROLE_LABELS } from '@/modules/registration/domain/value-objects/registration-role'
import BaseButton from '@/shared/ui/components/BaseButton.vue'

const props = defineProps<{
  form: RegistrationForm
  isUserMinor: boolean
  isSubmitting: boolean
}>()

const emit = defineEmits<{ submit: []; back: [] }>()

const model = props.form
const todayIso = computed(() => new Date().toISOString().slice(0, 10))
const roleTitle = computed(() => ROLE_LABELS[model.role])
</script>

<template>
  <div class="reg">
    <div class="reg__head">
      <BaseButton variant="link" @click="emit('back')">← Cambiar tipo de registro</BaseButton>
      <span class="reg__role">{{ roleTitle }}</span>
    </div>

    <form class="reg__form" @submit.prevent="emit('submit')">
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
            :max="todayIso"
            required
          />
        </div>
      </div>

      <transition name="reg-fade">
        <fieldset v-if="isUserMinor" class="reg__guardian">
          <legend class="reg__guardian-legend">Datos del padre / madre / tutor legal</legend>
          <p class="reg__guardian-note">
            Eres menor de edad, así que necesitamos los datos de tu padre, madre o tutor legal.
          </p>
          <div class="reg__grid">
            <div class="reg__field">
              <label class="reg__label" for="reg-g-firstname">Nombre del responsable</label>
              <InputText id="reg-g-firstname" v-model="model.guardian.firstName" required fluid />
            </div>
            <div class="reg__field">
              <label class="reg__label" for="reg-g-lastname">Apellidos del responsable</label>
              <InputText id="reg-g-lastname" v-model="model.guardian.lastName" required fluid />
            </div>
            <div class="reg__field">
              <label class="reg__label" for="reg-g-email">Correo del responsable</label>
              <InputText
                id="reg-g-email"
                v-model="model.guardian.email"
                type="email"
                required
                fluid
              />
            </div>
            <div class="reg__field">
              <label class="reg__label" for="reg-g-phone">Teléfono del responsable</label>
              <InputText
                id="reg-g-phone"
                v-model="model.guardian.phone"
                type="tel"
                required
                fluid
              />
            </div>
          </div>
        </fieldset>
      </transition>

      <BaseButton type="submit" variant="primary" block :loading="isSubmitting" class="reg__submit">
        Completar registro
      </BaseButton>
    </form>
  </div>
</template>

<style scoped>
.reg__head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 16px;
  margin-bottom: 20px;
}

.reg__role {
  font-family: var(--ca-font-mono);
  font-size: 12px;
  font-weight: 600;
  letter-spacing: 0.06em;
  text-transform: uppercase;
  color: var(--ca-cyan);
  background: rgba(45, 212, 217, 0.13);
  padding: 6px 12px;
  border-radius: 999px;
}

.reg__form {
  background: var(--ca-bg-elevated);
  border: 1px solid var(--ca-border-strong);
  border-radius: 18px;
  padding: 30px;
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
  border-color: var(--ca-cyan);
}

.reg__guardian {
  margin-top: 22px;
  padding: 22px;
  border: 1px solid var(--ca-border-strong);
  border-radius: 14px;
  background: var(--ca-surface);
}

.reg__guardian-legend {
  font-family: var(--ca-font-display);
  font-weight: 600;
  font-size: 16px;
  color: var(--ca-text);
  padding: 0 8px;
}

.reg__guardian-note {
  font-size: 13.5px;
  line-height: 1.5;
  color: var(--ca-amber);
  margin-bottom: 16px;
}

.reg__submit {
  margin-top: 22px;
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
