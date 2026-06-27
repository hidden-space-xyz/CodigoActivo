<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import Button from 'primevue/button'
import Dialog from 'primevue/dialog'
import Message from 'primevue/message'
import Select from 'primevue/select'

import type {
  ActivityResponse,
  TimeOverlapResponse,
  UserResponse,
} from '@/shared/api/generated/models'

const props = defineProps<{
  visible: boolean
  activities: ActivityResponse[]
  users: UserResponse[]
  saving: boolean
  verifyOverlaps: (activityId: string, userId: string) => Promise<TimeOverlapResponse>
}>()

const emit = defineEmits<{
  'update:visible': [value: boolean]
  submit: [payload: { activityId: string; userId: string; activityRoleTypeId: string }]
}>()

const activityId = ref<string | null>(null)
const userId = ref<string | null>(null)
const roleId = ref<string | null>(null)
const overlap = ref<TimeOverlapResponse | null>(null)

const roleOptions = computed(() => {
  const activity = props.activities.find((item) => item.id === activityId.value)
  return (activity?.allowedRoleTypes ?? []).map((role) => ({
    id: role.roleTypeId,
    name: role.roleTypeName ?? '',
  }))
})

const userOptions = computed(() =>
  props.users.map((user) => ({
    id: user.id,
    name: `${user.firstName ?? ''} ${user.lastName ?? ''}`.trim() || (user.email ?? ''),
  })),
)

watch(
  () => props.visible,
  (open) => {
    if (!open) return
    activityId.value = null
    userId.value = null
    roleId.value = null
    overlap.value = null
  },
)

watch([activityId, userId], async ([activity, user]) => {
  overlap.value = null
  roleId.value = null
  if (!activity || !user) return
  try {
    overlap.value = await props.verifyOverlaps(activity, user)
  } catch {
    overlap.value = null
  }
})

function close(): void {
  emit('update:visible', false)
}

function submit(): void {
  if (!activityId.value || !userId.value || !roleId.value) return
  emit('submit', {
    activityId: activityId.value,
    userId: userId.value,
    activityRoleTypeId: roleId.value,
  })
}
</script>

<template>
  <Dialog
    :visible="visible"
    modal
    header="Asignar voluntario"
    :style="{ width: '460px' }"
    @update:visible="close"
  >
    <div class="form">
      <div class="form__field">
        <label>Actividad</label>
        <Select
          v-model="activityId"
          :options="activities"
          option-label="title"
          option-value="id"
          placeholder="Selecciona una actividad"
          fluid
        />
      </div>
      <div class="form__field">
        <label>Voluntario</label>
        <Select
          v-model="userId"
          :options="userOptions"
          option-label="name"
          option-value="id"
          placeholder="Selecciona un voluntario"
          filter
          fluid
        />
      </div>
      <div class="form__field">
        <label>Rol</label>
        <Select
          v-model="roleId"
          :options="roleOptions"
          option-label="name"
          option-value="id"
          placeholder="Selecciona un rol"
          :disabled="roleOptions.length === 0"
          fluid
        />
        <small v-if="activityId && roleOptions.length === 0" class="form__hint">
          Esta actividad no tiene roles configurados.
        </small>
      </div>

      <Message v-if="overlap?.hasOverlaps" severity="warn" :closable="false">
        El voluntario tiene actividades solapadas en ese horario.
      </Message>
    </div>

    <template #footer>
      <Button label="Cancelar" text severity="secondary" :disabled="saving" @click="close" />
      <Button
        label="Asignar"
        :loading="saving"
        :disabled="!activityId || !userId || !roleId"
        @click="submit"
      />
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

.form__hint {
  color: var(--ca-amber);
  font-size: 12.5px;
}
</style>
