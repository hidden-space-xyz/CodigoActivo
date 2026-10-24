<script setup lang="ts">
import { computed } from 'vue'

import type { LegalPolicy } from '@/entities/legal-policy'
import { formatDateTime } from '@/shared/lib/date'
import { ActionButton } from '@/shared/ui/action-button'
import { DataState } from '@/shared/ui/data-state'
import { RichTextContent } from '@/shared/ui/rich-text-content'

const props = defineProps<{
  /** Number of the version shown; `null` keeps the dialog closed. */
  version: number | null
  /** The version once loaded. */
  policy: LegalPolicy | undefined
  /** Shows a spinner instead of the text. */
  loading: boolean
  /** Shows the load error instead of the text. */
  error: boolean
}>()

const emit = defineEmits<{
  /** Fired when the dialog is dismissed. */
  close: []
  /** Fired to load this version into the editor. */
  restore: [policy: LegalPolicy]
}>()

const visible = computed({
  get: () => props.version !== null,
  set: (open) => {
    if (!open) emit('close')
  },
})
</script>

<template>
  <el-dialog
    v-model="visible"
    :title="
      version === null ? '' : $t('pages.admin.legalPolicies.versions.dialogTitle', { version })
    "
    width="min(860px, 94vw)"
    append-to-body
  >
    <DataState :loading="loading" :error="error" :empty="false">
      <template v-if="policy">
        <p class="ca-field__hint">
          {{
            $t('pages.admin.legalPolicies.versions.publishedAt', {
              date: formatDateTime(policy.publishedAt),
            })
          }}
        </p>
        <RichTextContent class="version-dialog__content" :content="policy.content" />
      </template>
    </DataState>
    <template #footer>
      <ActionButton :label="$t('common.close')" text @click="emit('close')" />
      <ActionButton
        :label="$t('pages.admin.legalPolicies.versions.restore')"
        type="primary"
        :disabled="!policy"
        @click="policy && emit('restore', policy)"
      />
    </template>
  </el-dialog>
</template>

<style scoped>
.version-dialog__content {
  max-height: 60vh;
  margin-top: 12px;
  overflow-y: auto;
}
</style>
