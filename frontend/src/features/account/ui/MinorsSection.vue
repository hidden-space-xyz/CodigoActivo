<script setup lang="ts">
import { computed, reactive, ref } from 'vue'
import Dialog from 'primevue/dialog'
import InputText from 'primevue/inputtext'

import { useAccount } from '../model/useAccount'
import type { AccountChild } from '@/entities/account'
import { BaseButton } from '@/shared/ui'
import { formatDate, toDateInput, todayIso, useCrudFeedback, yearsAgoIso } from '@/shared/lib'

const feedback = useCrudFeedback()
const { children, addChild, updateChild, deleteChild } = useAccount()

const items = computed(() => children.data.value ?? [])

const dialogVisible = ref(false)
const mode = ref<'add' | 'edit'>('add')
const editingId = ref<string | null>(null)
const form = reactive({ firstName: '', lastName: '', birthDate: '' })

const maxBirthDateIso = todayIso()
const adultThresholdIso = yearsAgoIso(18)

const saving = computed(() => addChild.isPending.value || updateChild.isPending.value)

function openAdd(): void {
  mode.value = 'add'
  editingId.value = null
  form.firstName = ''
  form.lastName = ''
  form.birthDate = ''
  dialogVisible.value = true
}

function openEdit(child: AccountChild): void {
  mode.value = 'edit'
  editingId.value = child.id ?? null
  form.firstName = child.firstName ?? ''
  form.lastName = child.lastName ?? ''
  form.birthDate = toDateInput(child.birthDate)
  dialogVisible.value = true
}

function notifyError(error: unknown): void {
  feedback.error(error)
}

function save(): void {
  if (mode.value === 'add') {
    addChild.mutate(
      {
        firstName: form.firstName.trim(),
        lastName: form.lastName.trim(),
        birthDate: form.birthDate,
      },
      {
        onSuccess: () => {
          dialogVisible.value = false
          feedback.success('El menor se ha registrado a tu cargo.', 'Menor añadido')
        },
        onError: notifyError,
      },
    )
    return
  }

  const childId = editingId.value
  if (!childId) return
  updateChild.mutate(
    {
      childId,
      input: {
        firstName: form.firstName.trim(),
        lastName: form.lastName.trim(),
        birthDate: form.birthDate,
      },
    },
    {
      onSuccess: () => finishEdit(),
      onError: notifyError,
    },
  )
}

function finishEdit(): void {
  dialogVisible.value = false
  feedback.success('Los datos del menor se han guardado.', 'Menor actualizado')
}

const deleteTarget = ref<AccountChild | null>(null)

function confirmDelete(): void {
  const id = deleteTarget.value?.id
  if (!id) return
  deleteChild.mutate(id, {
    onSuccess: () => {
      deleteTarget.value = null
      feedback.success('El menor se ha eliminado de tu cuenta.', 'Menor eliminado')
    },
    onError: notifyError,
  })
}
</script>

<template>
  <section class="acc-card">
    <div class="acc-card__head">
      <h2 class="acc-card__title">Mis menores</h2>
      <BaseButton variant="primary" @click="openAdd">+ Añadir menor</BaseButton>
    </div>

    <p v-if="children.isLoading.value" class="acc-card__state">Cargando…</p>
    <p v-else-if="items.length === 0" class="acc-card__state">
      No tienes menores a tu cargo. Puedes añadir los que quieras inscribir.
    </p>

    <ul v-else class="acc-minors">
      <li v-for="child in items" :key="child.id" class="acc-minor">
        <div class="acc-minor__info">
          <span class="acc-minor__name">{{ child.firstName }} {{ child.lastName }}</span>
          <span class="acc-minor__meta">{{ formatDate(child.birthDate) }}</span>
        </div>
        <div class="acc-minor__actions">
          <BaseButton variant="ghost" @click="openEdit(child)">Editar</BaseButton>
          <BaseButton variant="link" @click="deleteTarget = child">Eliminar</BaseButton>
        </div>
      </li>
    </ul>

    <Dialog
      v-model:visible="dialogVisible"
      modal
      :header="mode === 'add' ? 'Añadir menor' : 'Editar menor'"
      :style="{ width: '90vw', maxWidth: '520px' }"
    >
      <form class="acc-form" @submit.prevent="save">
        <div class="acc-form__grid">
          <div class="acc-form__field">
            <label for="m-firstname">Nombre</label>
            <InputText id="m-firstname" v-model="form.firstName" :maxlength="120" required fluid />
          </div>
          <div class="acc-form__field">
            <label for="m-lastname">Apellidos</label>
            <InputText id="m-lastname" v-model="form.lastName" :maxlength="120" required fluid />
          </div>
          <div class="acc-form__field">
            <label for="m-dob">Fecha de nacimiento</label>
            <input
              id="m-dob"
              v-model="form.birthDate"
              type="date"
              class="acc-date"
              :min="adultThresholdIso"
              :max="maxBirthDateIso"
              required
            />
          </div>
        </div>
        <div class="acc-form__actions">
          <BaseButton variant="link" type="button" @click="dialogVisible = false">
            Cancelar
          </BaseButton>
          <BaseButton variant="primary" type="submit" :loading="saving">Guardar</BaseButton>
        </div>
      </form>
    </Dialog>

    <Dialog
      :visible="deleteTarget !== null"
      modal
      header="Eliminar menor"
      :style="{ width: '90vw', maxWidth: '420px' }"
      @update:visible="(value) => !value && (deleteTarget = null)"
    >
      <p class="acc-confirm">
        ¿Seguro que quieres eliminar a
        <b>{{ deleteTarget?.firstName }} {{ deleteTarget?.lastName }}</b
        >? Esta acción no se puede deshacer.
      </p>
      <div class="acc-form__actions">
        <BaseButton variant="link" type="button" @click="deleteTarget = null">Cancelar</BaseButton>
        <BaseButton variant="primary" :loading="deleteChild.isPending.value" @click="confirmDelete">
          Eliminar
        </BaseButton>
      </div>
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

.acc-card__state {
  color: var(--ca-text-dim);
  font-family: var(--ca-font-mono);
}

.acc-minors {
  list-style: none;
  margin: 0;
  padding: 0;
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.acc-minor {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 16px;
  background: var(--ca-surface);
  border: 1px solid var(--ca-border-soft);
  border-radius: 12px;
  padding: 14px 16px;
}

.acc-minor__info {
  display: flex;
  flex-direction: column;
  gap: 3px;
  min-width: 0;
}

.acc-minor__name {
  font-weight: 600;
  color: var(--ca-text-bright);
}

.acc-minor__meta {
  font-size: 13px;
  color: var(--ca-text-muted);
}

.acc-minor__actions {
  display: flex;
  gap: 6px;
  flex-shrink: 0;
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

.acc-confirm {
  color: var(--ca-text);
  line-height: 1.6;
  margin: 0 0 16px;
}

@media (max-width: 620px) {
  .acc-form__grid {
    grid-template-columns: 1fr;
  }
}
</style>
