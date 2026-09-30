<script setup lang="ts">
import type { SelectOption } from '@/shared/ui/column-filter'
import { ActionButton } from '@/shared/ui/action-button'

/** Opens the dialog. */
const visible = defineModel<boolean>('visible', { required: true })

/** Id of the option picked so far. */
const selected = defineModel<string | null>('selected', { required: true })

defineProps<{
  /** Dialog header, naming what changes. */
  title: string
  /** Full name of the attendee whose signup changes. */
  attendeeName: string
  /** Title of the activity of that signup. */
  activityTitle: string
  /** Label of the select. */
  label: string
  /** Id of the select, which its label points to. */
  inputId: string
  /** Shown in the select while nothing is picked. */
  placeholder: string
  /** Roles or statuses to pick from. */
  options: readonly SelectOption[]
  /** Shown instead of options when there are none; applying needs at least one. */
  unavailableText?: string
  /** The change is being saved. */
  applying: boolean
}>()

const emit = defineEmits<{
  /** Fired to apply the picked option. */
  apply: []
}>()
</script>

<template>
  <el-dialog v-model="visible" :title="title" width="min(92vw, 400px)" append-to-body>
    <p class="dialog-context">
      {{
        $t('pages.admin.eventDetail.attendees.dialogContext', {
          name: attendeeName,
          activity: activityTitle,
        })
      }}
    </p>
    <div class="form__field">
      <label :for="inputId">{{ label }}</label>
      <el-select :id="inputId" v-model="selected" :placeholder="placeholder">
        <el-option
          v-for="option in options"
          :key="option.value"
          :label="option.label"
          :value="option.value"
        />
      </el-select>
      <small v-if="unavailableText && options.length === 0" class="form__warning">
        {{ unavailableText }}
      </small>
    </div>
    <template #footer>
      <ActionButton
        :label="$t('common.cancel')"
        text
        :disabled="applying"
        @click="visible = false"
      />
      <ActionButton
        :label="$t('common.apply')"
        type="primary"
        :loading="applying"
        :disabled="!selected || options.length === 0"
        @click="emit('apply')"
      />
    </template>
  </el-dialog>
</template>

<style scoped>
.dialog-context {
  font-size: 13.5px;
  color: var(--ca-text-muted);
  margin: 0 0 14px;
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

.form__warning {
  font-size: 12.5px;
  color: var(--ca-danger-ink);
}
</style>
