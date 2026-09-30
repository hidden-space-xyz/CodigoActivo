<script setup lang="ts">
import { ref, watch } from 'vue'

import type { User } from '@/entities/user'
import { ActionButton } from '@/shared/ui/action-button'
import type { SelectOption } from '@/shared/ui/column-filter'

/** Opens the dialog; each time it opens the user's current type is preselected. */
const visible = defineModel<boolean>('visible', { required: true })

const props = defineProps<{
  /** User whose type changes. */
  user: User | null
  /** User types to choose from. */
  options: readonly SelectOption[]
  /** Shows a loading state on the apply button while the parent saves the type. */
  saving: boolean
}>()

const emit = defineEmits<{
  /** Fired with the chosen user type id. */
  submit: [userTypeId: string]
}>()

const userTypeId = ref<string | null>(null)

watch(visible, (open) => {
  if (open) userTypeId.value = props.user?.type?.id ?? null
})

function apply(): void {
  if (userTypeId.value) emit('submit', userTypeId.value)
}
</script>

<template>
  <el-dialog
    v-model="visible"
    :title="$t('pages.admin.users.typeDialog.header')"
    width="min(92vw, 420px)"
  >
    <div class="form__field">
      <label for="user-type">{{ $t('pages.admin.users.typeDialog.typeLabel') }}</label>
      <el-select
        id="user-type"
        v-model="userTypeId"
        :placeholder="$t('pages.admin.users.typeDialog.placeholder')"
        class="form__select"
      >
        <el-option
          v-for="option in options"
          :key="option.value"
          :label="option.label"
          :value="option.value"
        />
      </el-select>
    </div>
    <template #footer>
      <ActionButton :label="$t('common.cancel')" text :disabled="saving" @click="visible = false" />
      <ActionButton
        :label="$t('common.apply')"
        type="primary"
        :loading="saving"
        :disabled="!userTypeId"
        @click="apply"
      />
    </template>
  </el-dialog>
</template>

<style scoped>
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

.form__select {
  width: 100%;
}
</style>
