<script setup lang="ts">
import { ref, watch } from 'vue'

import {
  fullName,
  STAFF_ROLES,
  staffRoleLabelKey,
  type StaffRole,
  type User,
} from '@/entities/user'

import AdminPasswordDialog from './AdminPasswordDialog.vue'

/** Opens the dialog; each time it opens the user's current staff role is preselected. */
const visible = defineModel<boolean>('visible', { required: true })

const props = defineProps<{
  /** User whose staff role changes. */
  user: User | null
  /** Shows a loading state on the confirm button while the parent saves the staff role. */
  saving: boolean
  /** Rejection reported by the parent, such as an incorrect password; empty hides it. */
  error: string
}>()

const emit = defineEmits<{
  /** Fired with the chosen staff role and the caller's password, `null` when it is `None`. */
  submit: [staffRole: StaffRole, currentPassword: string | null]
}>()

const staffRole = ref<StaffRole>('None')

watch(visible, (open) => {
  if (open) staffRole.value = props.user?.staffRole ?? 'None'
})

function confirm(currentPassword: string): void {
  emit('submit', staffRole.value, staffRole.value === 'None' ? null : currentPassword)
}
</script>

<template>
  <AdminPasswordDialog
    v-model:visible="visible"
    data-testid="staff-role-dialog"
    :title="$t('pages.admin.users.staffRole.header')"
    :message="$t('pages.admin.users.staffRole.message', { fullName: fullName(user ?? {}) })"
    :confirm-label="$t('pages.admin.users.staffRole.confirm')"
    input-id="staff-role-password"
    width="min(460px, 92vw)"
    :saving="saving"
    :error="error"
    :passwordless="staffRole === 'None'"
    @submit="confirm"
  >
    <div class="ca-field">
      <label class="ca-field__label" for="staff-role">{{
        $t('pages.admin.users.staffRole.label')
      }}</label>
      <el-select id="staff-role" v-model="staffRole" data-testid="staff-role-dialog-role">
        <el-option
          v-for="role in STAFF_ROLES"
          :key="role"
          :label="$t(staffRoleLabelKey(role))"
          :value="role"
          :data-testid="`option-${role}`"
        />
      </el-select>
    </div>
  </AdminPasswordDialog>
</template>
